using Microsoft.AspNetCore.SignalR;
using Yass.Core.Hub;

namespace Yass.Server.Hub;

/// <summary>Broadcasts to every SignalR connection on <see cref="YassHub"/>.</summary>
public sealed class SignalRNotifier(IHubContext<YassHub, IYassClient> hub) : IClientNotifier
{
    public IYassClient All => hub.Clients.All;
}
