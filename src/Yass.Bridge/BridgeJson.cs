using System.Text.Json;

namespace Yass.Bridge;

/// <summary>Shaped like the SignalR MessagePack wire the client already reads: PascalCase names, enums as numbers, nulls written.</summary>
public static class BridgeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false,
    };
}
