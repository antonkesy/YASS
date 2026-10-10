using Microsoft.AspNetCore.SignalR;
using Yass.App.Api;
using Yass.Core.Hub;

namespace Yass.Server.Hub;

public sealed class SignalRNotifier(IHubContext<YassHub, IYassClient> hub) : IClientNotifier
{
    public IYassClient All => hub.Clients.All;
}
