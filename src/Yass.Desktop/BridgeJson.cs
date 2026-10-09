using System.Text.Json;

namespace Yass.Desktop;

/// <summary>
/// JSON for the message bridge, shaped like the SignalR MessagePack wire the client already reads:
/// property names as declared (PascalCase), enums as numbers, nulls written.
/// </summary>
public static class BridgeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false,
    };
}
