using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yass.App.Api;
using Yass.Core.Hub;

namespace Yass.Desktop;

/// <summary>
/// The client → host half of the message bridge. A call is <c>{"id": n, "method": "Name", "args": [...]}</c>;
/// the answer is <c>{"id": n, "result": ...}</c> or <c>{"id": n, "error": "message"}</c>. Every
/// <see cref="IYassHub"/> method is callable, served by <see cref="YassApi"/>'s method of the same name,
/// plus the public methods of any extra targets (the desktop's own, such as its file dialogs).
/// A trailing <see cref="CancellationToken"/> parameter gets the app's shutdown token.
/// </summary>
public sealed class BridgeDispatcher
{
    private readonly Dictionary<string, (MethodInfo Method, object Target)> _methods = new(StringComparer.Ordinal);
    private readonly Action<string> _send;
    private readonly CancellationToken _stopping;
    private readonly ILogger _logger;

    public BridgeDispatcher(YassApi api, Action<string> send, ILogger logger, IEnumerable<object> extras, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(extras);
        _send = send;
        _stopping = stopping;
        _logger = logger;

        foreach (MethodInfo contract in typeof(IYassHub).GetMethods())
        {
            Type[] parameters = [.. contract.GetParameters().Select(p => p.ParameterType)];
            MethodInfo method = typeof(YassApi).GetMethod(contract.Name, parameters)
                ?? typeof(YassApi).GetMethod(contract.Name, [.. parameters, typeof(CancellationToken)])
                ?? throw new InvalidOperationException($"YassApi has no method matching IYassHub.{contract.Name}.");
            _methods[contract.Name] = (method, api);
        }

        foreach (object extra in extras)
        {
            foreach (MethodInfo method in extra.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                _methods.Add(method.Name, (method, extra));
            }
        }
    }

    /// <summary>The names a client may call.</summary>
    public IReadOnlyCollection<string> Methods => _methods.Keys;

    /// <summary>
    /// Takes a message from the webview, which calls this on its UI thread, and answers it from
    /// the thread pool: no call ever blocks the window.
    /// </summary>
    public void Receive(string message) =>
        _ = Task.Run(async () =>
        {
            string? answer = await HandleAsync(message).ConfigureAwait(false);
            if (answer is not null)
            {
                _send(answer);
            }
        });

    /// <summary>Runs one call; the answer to send back, or <c>null</c> for a message that is not a call.</summary>
    public async Task<string?> HandleAsync(string message)
    {
        Call? call;
        try
        {
            call = JsonSerializer.Deserialize<Call>(message, BridgeJson.Options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Ignoring a bridge message that is not JSON: {Message}", ex.Message);
            return null;
        }

        if (call is not { Id: { } id, Method: { } name })
        {
            return null;
        }

        try
        {
            object? result = await InvokeAsync(name, call.Args).ConfigureAwait(false);
            return JsonSerializer.Serialize(new Answer(id, result, null), BridgeJson.Options);
        }
        catch (Exception ex) when (ex is YassException or BridgeException or JsonException or OperationCanceledException)
        {
            return JsonSerializer.Serialize(new Answer(id, null, ex.Message), BridgeJson.Options);
        }
#pragma warning disable CA1031 // Any failure goes back to the caller as an error, as SignalR's detailed errors do.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Bridge call {Method} failed", name);
            return JsonSerializer.Serialize(new Answer(id, null, ex.Message), BridgeJson.Options);
        }
    }

    private async Task<object?> InvokeAsync(string name, JsonElement[]? args)
    {
        if (!_methods.TryGetValue(name, out (MethodInfo Method, object Target) entry))
        {
            throw new BridgeException($"Unknown method {name}.");
        }

        ParameterInfo[] parameters = entry.Method.GetParameters();
        object?[] values = new object?[parameters.Length];
        args ??= [];
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                values[i] = _stopping;
            }
            else if (i < args.Length && args[i].ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                values[i] = args[i].Deserialize(parameter.ParameterType, BridgeJson.Options);
            }
            else if (parameter.HasDefaultValue)
            {
                values[i] = parameter.DefaultValue;
            }
            else if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null)
            {
                // Reflection would quietly pass default(T): a rescan without its page would rescan page 0.
                throw new BridgeException($"{name} needs its {parameter.Name} argument.");
            }
        }

        object? returned = entry.Method.Invoke(entry.Target, BindingFlags.DoNotWrapExceptions, binder: null, values, culture: null);

        if (returned is not Task task)
        {
            return returned;
        }

        await task.ConfigureAwait(false);
        Type type = entry.Method.ReturnType;
        return type.IsGenericType ? type.GetProperty(nameof(Task<object>.Result))!.GetValue(task) : null;
    }

    private sealed record Call(
        [property: JsonPropertyName("id")] long? Id,
        [property: JsonPropertyName("method")] string? Method,
        [property: JsonPropertyName("args")] JsonElement[]? Args);

    private sealed record Answer(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("result")] object? Result,
        [property: JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Error);
}

/// <summary>A call the bridge cannot make: an unknown method or arguments that do not fit.</summary>
public sealed class BridgeException : Exception
{
    public BridgeException()
    {
    }

    public BridgeException(string message)
        : base(message)
    {
    }

    public BridgeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
