using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.Desktop.Tests;

/// <summary>The bridge as the webview drives it: JSON in, JSON out, against the real app on the fake scanner.</summary>
public sealed class BridgeDispatcherTests : IAsyncLifetime
{
    private static readonly int[] FirstPage = [0];

    private DesktopHarness _desktop = null!;
    private BridgeDispatcher _bridge = null!;

    public ValueTask InitializeAsync()
    {
        _desktop = new DesktopHarness();
        _bridge = new BridgeDispatcher(_desktop.Api, _desktop.Record, NullLogger.Instance, [], CancellationToken.None);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _desktop.DisposeAsync();

    private async Task<JsonElement> CallAsync(string method, params object?[] args)
    {
        string request = JsonSerializer.Serialize(new { id = 7, method, args });
        string answer = Assert.IsType<string>(await _bridge.HandleAsync(request));
        JsonElement root = JsonDocument.Parse(answer).RootElement;
        Assert.Equal(7, root.GetProperty("id").GetInt32());
        return root;
    }

    private async Task<JsonElement> ResultAsync(string method, params object?[] args)
    {
        JsonElement root = await CallAsync(method, args);
        Assert.False(root.TryGetProperty("error", out JsonElement error), error.ToString());
        return root.GetProperty("result");
    }

    private async Task<string> ErrorAsync(string method, params object?[] args) =>
        (await CallAsync(method, args)).GetProperty("error").GetString()!;

    [Fact]
    public void Every_hub_method_is_callable() =>
        Assert.All(typeof(IYassHub).GetMethods(), m => Assert.Contains(m.Name, _bridge.Methods));

    [Fact]
    public async Task Calls_return_the_dto_with_its_declared_property_names()
    {
        JsonElement status = await ResultAsync(nameof(IYassHub.GetScannerStatus));
        Assert.True(status.TryGetProperty(nameof(ScannerStatusDto.State), out _));

        JsonElement options = await ResultAsync(nameof(IYassHub.GetEffectiveOptions));
        Assert.Equal(JsonValueKind.Number, options.GetProperty(nameof(ScanOptionsDto.Dpi)).ValueKind);

        Assert.Equal(JsonValueKind.Array, (await ResultAsync(nameof(IYassHub.ListSessions))).ValueKind);
        Assert.Equal("", (await ResultAsync(nameof(IYassHub.GetServerFolder))).GetString());
    }

    [Fact]
    public async Task A_scan_edit_and_export_round_trip_as_the_client_sends_them()
    {
        SessionDto scanned = await _desktop.ScanAsync();

        // An edit as the TypeScript client builds it: only the fields that matter, enums as numbers.
        JsonElement edited = await ResultAsync(
            nameof(IYassHub.ApplyEdits),
            scanned.Id,
            new object[] { new { Op = (int)PageEditOp.Rotate, Targets = FirstPage, Degrees = 90 } });
        Assert.Equal((int)Rotation.R90, edited.GetProperty(nameof(SessionDto.Pages))[0].GetProperty(nameof(PageDto.Rotation)).GetInt32());

        JsonElement exported = await ResultAsync(nameof(IYassHub.Export), scanned.Id, new { Format = 0, ToServer = false });
        string path = exported.GetProperty(nameof(ExportResult.Documents))[0].GetProperty(nameof(ExportedDocumentDto.DownloadPath)).GetString()!;
        Assert.StartsWith($"/export/{scanned.Id}/", path, StringComparison.Ordinal);
        Assert.NotNull(_desktop.Scheme.Resolve(new Uri(_desktop.Scheme.Origin.Root, path)));
    }

    [Fact]
    public async Task Missing_and_null_arguments_take_the_parameter_default()
    {
        JsonElement started = await ResultAsync(nameof(IYassHub.StartScan));
        Assert.Equal(JsonValueKind.String, started.GetProperty(nameof(SessionDto.Id)).ValueKind);
    }

    [Fact]
    public async Task Calls_without_a_result_answer_null()
    {
        SessionDto scanned = await _desktop.ScanAsync();
        Assert.Equal(JsonValueKind.Null, (await ResultAsync(nameof(IYassHub.DeleteSession), scanned.Id)).ValueKind);
    }

    [Fact]
    public async Task Failures_come_back_as_their_message()
    {
        Assert.Contains("not found", await ErrorAsync(nameof(IYassHub.GetSession), "nope"), StringComparison.Ordinal);
        Assert.Contains("Unknown method", await ErrorAsync("Shutdown"), StringComparison.Ordinal);
        Assert.NotEmpty(await ErrorAsync(nameof(IYassHub.ApplyEdits), "x", "not an array"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"method\":\"ListSessions\"}")]
    [InlineData("{\"id\":1}")]
    [InlineData("{\"event\":\"PageAdded\",\"args\":[]}")]
    public async Task Messages_that_are_not_calls_get_no_answer(string message) =>
        Assert.Null(await _bridge.HandleAsync(message));

    [Fact]
    public async Task Receive_answers_through_the_window()
    {
        int before = _desktop.Sent.Count;
        _bridge.Receive("{\"id\":42,\"method\":\"ListSessions\",\"args\":[]}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!_desktop.Sent.Skip(before).Any(m => m.StartsWith("{\"id\":42,", StringComparison.Ordinal)))
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    [Fact]
    public async Task Events_are_posted_with_their_name_and_arguments()
    {
        SessionDto scanned = await _desktop.ScanAsync();

        JsonElement[] events = [.. _desktop.Sent
            .Select(m => JsonDocument.Parse(m).RootElement)
            .Where(e => e.TryGetProperty("event", out _))];

        JsonElement added = events.First(e => e.GetProperty("event").GetString() == nameof(IYassClient.PageAdded));
        Assert.Equal(scanned.Id, added.GetProperty("args")[0].GetString());
        Assert.True(added.GetProperty("args")[1].TryGetProperty(nameof(PageDto.Hash), out _));

        Assert.Contains(events, e => e.GetProperty("event").GetString() == nameof(IYassClient.SessionChanged));
        Assert.Contains(events, e => e.GetProperty("event").GetString() == nameof(IYassClient.PageUpdated));
    }

    [Fact]
    public void Extra_targets_add_their_own_methods()
    {
        var bridge = new BridgeDispatcher(_desktop.Api, _ => { }, NullLogger.Instance, [new Extra()], CancellationToken.None);
        Assert.Contains(nameof(Extra.Echo), bridge.Methods);
    }

    [Fact]
    public async Task Extra_targets_get_the_shutdown_token_and_may_return_plain_values()
    {
        using var stopping = new CancellationTokenSource();
        var bridge = new BridgeDispatcher(_desktop.Api, _ => { }, NullLogger.Instance, [new Extra()], stopping.Token);

        string answer = (await bridge.HandleAsync("{\"id\":1,\"method\":\"Echo\",\"args\":[\"hi\"]}"))!;
        Assert.Equal("hi", JsonDocument.Parse(answer).RootElement.GetProperty("result").GetString());

        await stopping.CancelAsync();
        string cancelled = (await bridge.HandleAsync("{\"id\":2,\"method\":\"Wait\",\"args\":[]}"))!;
        Assert.True(JsonDocument.Parse(cancelled).RootElement.TryGetProperty("error", out _));
    }

#pragma warning disable CA1822 // The bridge calls instance methods, as the desktop's own target has.
    private sealed class Extra
    {
        public string Echo(string text) => text;

        public Task Wait(CancellationToken cancellationToken) => Task.Delay(Timeout.Infinite, cancellationToken);
    }
#pragma warning restore CA1822
}
