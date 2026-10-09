using Yass.Core.Hub;

namespace Yass.App.Api;

/// <summary>
/// Where server pushes go. Services broadcast through this rather than a transport, so the
/// same scan, edit and export code serves whichever host carries the events.
/// </summary>
public interface IClientNotifier
{
    /// <summary>Every connected client.</summary>
    IYassClient All { get; }
}
