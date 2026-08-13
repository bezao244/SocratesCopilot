using System.Collections.Concurrent;
using CompanyCopilot.Application.Abstractions;
using Microsoft.Extensions.Options;
using CompanyCopilot.Application.Configuration;

namespace CompanyCopilot.Application.Chat;

/// <summary>
/// Armazenamento em memória de sessões anônimas de chat, isolado por OwnerId:
/// uma sessão só pode ser acessada pelo dono que a criou.
/// Limpeza de expirados é feita por demanda (lazy) a cada acesso.
/// </summary>
public sealed class ChatSessionStore : IChatSessionStore
{
    private sealed record Session
    {
        public required string OwnerId { get; init; }
        public List<(string User, string Assistant)> Turns { get; } = new();
        public DateTimeOffset LastActivityUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private readonly ChatOptions _options;
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public ChatSessionStore(IOptions<ChatOptions> options)
    {
        _options = options.Value;
    }

    public string CreateSession(string ownerId)
    {
        var id = Guid.NewGuid().ToString("N");
        _sessions.TryAdd(id, new Session { OwnerId = ownerId });
        return id;
    }

    public bool Exists(string ownerId, string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) && session.OwnerId == ownerId;

    public void Adopt(string ownerId, string sessionId, IReadOnlyList<(string User, string Assistant)> turns)
    {
        var session = _sessions.GetOrAdd(sessionId, _ => new Session { OwnerId = ownerId });
        session.Turns.Clear();
        session.Turns.AddRange(turns);
        session.LastActivityUtc = DateTimeOffset.UtcNow;
    }

    public void AddTurn(string ownerId, string sessionId, string userMessage, string assistantMessage)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.OwnerId != ownerId)
        {
            return;
        }

        session.Turns.Add((userMessage, assistantMessage));
        session.LastActivityUtc = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<(string User, string Assistant)> GetLastTurns(string ownerId, string sessionId, int maxTurns)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.OwnerId != ownerId)
        {
            return Array.Empty<(string, string)>();
        }

        session.LastActivityUtc = DateTimeOffset.UtcNow;
        var skip = Math.Max(0, session.Turns.Count - maxTurns);
        return session.Turns.Skip(skip).ToList();
    }

    public void Touch(string ownerId, string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session) && session.OwnerId == ownerId)
        {
            session.LastActivityUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RemoveExpiredSessions(TimeSpan idleTimeout, DateTimeOffset now)
    {
        foreach (var (id, session) in _sessions)
        {
            if (now - session.LastActivityUtc > idleTimeout)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }
}
