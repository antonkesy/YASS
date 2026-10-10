using MessagePack;
using MessagePack.Resolvers;

namespace Yass.Server.Hub;

internal static class HubProtocol
{
    /// <summary>Contractless, so the DTOs need no attributes; hardened, since the bytes come off the network.</summary>
    public static MessagePackSerializerOptions Options { get; } = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithSecurity(MessagePackSecurity.UntrustedData);
}
