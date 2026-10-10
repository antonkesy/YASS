using Microsoft.AspNetCore.SignalR;
using Yass.App.Api;

namespace Yass.Server.Hub;

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
