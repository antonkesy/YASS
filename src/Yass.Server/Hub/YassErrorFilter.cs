using Microsoft.AspNetCore.SignalR;
using Yass.Server.Api;

namespace Yass.Server.Hub;

/// <summary>Sends a <see cref="YassException"/>'s message to the caller as a <see cref="HubException"/>.</summary>
public sealed class YassErrorFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(invocationContext).ConfigureAwait(false);
        }
        catch (YassException ex)
        {
            throw new HubException(ex.Message, ex);
        }
    }
}
