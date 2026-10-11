using Yass.App.Api;
using Yass.Core.Domain;
using Yass.Core.Hub;

namespace Yass.App.Sessions;

/// <summary>Session history operations and their notifications, shared by both hosts.</summary>
public sealed class SessionService(
    SessionStore sessions,
    SessionMapper mapper,
    IClientNotifier clients,
    TimeProvider time,
    ILogger<SessionService> logger)
{
    public Session Get(string sessionId) =>
        SessionPaths.IsValidId(sessionId) && sessions.TryGet(sessionId, out Session session)
            ? session
            : throw new KeyNotFoundException($"Session {sessionId} not found.");

    public SessionDto[] List() => [.. sessions.List().Select(mapper.ToDto)];

    public async Task DeleteSession(string sessionId)
    {
        Session session = Get(sessionId);
        if (session.State == SessionState.Scanning)
        {
            throw new InvalidOperationException("Stop the scan before deleting the session.");
        }

        if (session.State == SessionState.Exporting)
        {
            throw new InvalidOperationException("Wait for the export to finish before deleting the session.");
        }

        sessions.Delete(sessionId);
        await clients.All.SessionChanged(mapper.Removed(session, SessionMapper.Deleted)).ConfigureAwait(false);
    }

    public async Task<SessionDto> RenameSession(string sessionId, string? name)
    {
        Get(sessionId);
        string? trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        if (trimmed is { Length: > 200 })
        {
            throw new InvalidOperationException("Name is too long (200 characters maximum).");
        }

        SessionDto dto = mapper.ToDto(sessions.Update(sessionId, s => s with { Name = trimmed }));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<SessionDto> DiscardSession(string sessionId)
    {
        Get(sessionId);
        var discarded = new SessionEvent(SessionEventKind.Discarded, time.GetUtcNow());
        SessionDto dto = mapper.ToDto(sessions.Discard(sessionId, discarded));
        await clients.All.SessionChanged(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<int> ClearSessions()
    {
        Session[] removable = [.. sessions.List().Where(s => s.State is not (SessionState.Scanning or SessionState.Exporting))];
        foreach (Session session in removable)
        {
            sessions.Delete(session.Id);
            await clients.All.SessionChanged(mapper.Removed(session, SessionMapper.Deleted)).ConfigureAwait(false);
        }

        logger.LogInformation("Cleared {Count} session(s)", removable.Length);
        return removable.Length;
    }
}
