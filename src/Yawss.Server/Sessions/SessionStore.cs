using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yawss.Core.Domain;
using Yawss.Server.Config;

namespace Yawss.Server.Sessions;

/// <summary>Sessions on disk (<c>session.json</c> per directory) mirrored in memory. Writes are atomic (temp + rename).</summary>
public sealed class SessionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly ConfigStore _config;
    private readonly ILogger<SessionStore> _logger;

    public SessionStore(ConfigStore config, ILogger<SessionStore> logger)
    {
        _config = config;
        _logger = logger;
        LoadAll();
    }

    public string Root => _config.Current.Sessions.Directory;

    public Session Create(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Directory.CreateDirectory(SessionPaths.Directory(Root, session.Id));
        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException($"Session {session.Id} already exists.");
        }

        Persist(session);
        return session;
    }

    public bool TryGet(string id, out Session session) => _sessions.TryGetValue(id, out session!);

    public Session Get(string id) =>
        TryGet(id, out Session session) ? session : throw new KeyNotFoundException($"Session {id} not found.");

    public IReadOnlyList<Session> List() => _sessions.Values.OrderByDescending(s => s.CreatedAt).ToArray();

    /// <summary>Applies <paramref name="update"/> atomically with respect to other updates of the same session and persists.</summary>
    public Session Update(string id, Func<Session, Session> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Session updated = _sessions.AddOrUpdate(
            id,
            _ => throw new KeyNotFoundException($"Session {id} not found."),
            (_, current) => update(current));
        Persist(updated);
        return updated;
    }

    public bool Delete(string id)
    {
        if (!_sessions.TryRemove(id, out _))
        {
            return false;
        }

        string dir = SessionPaths.Directory(Root, id);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        return true;
    }

    private void Persist(Session session)
    {
        string path = SessionPaths.Manifest(Root, session.Id);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(session, Json));
        File.Move(temp, path, overwrite: true);
    }

    private void LoadAll()
    {
        if (!Directory.Exists(Root))
        {
            Directory.CreateDirectory(Root);
            return;
        }

        foreach (string dir in Directory.EnumerateDirectories(Root))
        {
            string manifest = Path.Combine(dir, SessionPaths.ManifestName);
            if (!File.Exists(manifest))
            {
                continue;
            }

            try
            {
                Session? session = JsonSerializer.Deserialize<Session>(File.ReadAllText(manifest), Json);
                if (session is null)
                {
                    continue;
                }

                // A scan or export that was interrupted by a restart cannot resume.
                if (session.State is SessionState.Scanning or SessionState.Exporting)
                {
                    session = session with { State = session.Pages.Count > 0 ? SessionState.Review : SessionState.Failed, Error = session.Pages.Count > 0 ? null : "Interrupted by restart." };
                    Persist(session);
                }

                _sessions[session.Id] = session;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring unreadable session manifest {Manifest}", manifest);
            }
        }

        _logger.LogInformation("Loaded {Count} session(s) from {Root}", _sessions.Count, Root);
    }
}
