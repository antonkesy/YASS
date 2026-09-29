using Microsoft.AspNetCore.SignalR;
using Yass.Core.Hub;
using Yass.Core.Naming;
using Yass.Fake;

namespace Yass.Server.Tests.Hub;

/// <summary>
/// Naming over the wire. These run on machines with and without the <c>tesseract</c> binary, so
/// nothing here assumes text can actually be read — what is asserted is the contract around it.
/// </summary>
public sealed class NamingTests
{
    [Fact]
    public async Task Suggests_the_template_when_nothing_is_configured()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(2));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        NameSuggestionsDto result = await hub.SuggestNames(done.Id);

        // Off by default: no OCR, no model, and still a usable name rather than an empty list.
        Assert.False(result.CanRead);
        Assert.Equal(NameSource.Template, Assert.Single(result.Suggestions).Source);
        Assert.NotEmpty(Assert.Single(result.Suggestions).Name);
    }

    [Fact]
    public async Task The_advertised_capability_matches_what_a_call_reports()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);
        SessionDto done = await hub.ScanToEnd();

        ScanOptionsDto options = await hub.GetEffectiveOptions();

        // The browser decides whether to offer Find name from the cheap call, and then lives
        // with what the expensive one says. They have to agree, binary present or not.
        Assert.Equal((await hub.SuggestNames(done.Id)).CanRead, options.CanReadPageText);
    }

    [Fact]
    public async Task Says_how_many_pages_would_be_read()
    {
        await using var factory = new YassFactory(
            FakeScenario.Pages(1),
            c => c with { Naming = c.Naming with { OcrPages = 5 } });
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        Assert.Equal(5, (await hub.GetEffectiveOptions()).PageTextPages);
    }

    [Fact]
    public async Task Unknown_session_is_rejected()
    {
        await using var factory = new YassFactory(FakeScenario.Pages(1));
        await using HubClient hub = await HubClient.ConnectAsync(factory);

        await Assert.ThrowsAsync<HubException>(() => hub.SuggestNames("nope"));
    }
}
