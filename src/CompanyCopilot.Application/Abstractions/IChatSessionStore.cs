namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Histórico da sessão anônima em memória: no máximo seis turnos anteriores,
/// com expiração por inatividade. Sessões reabertas da persistência são
/// hidratadas via Adopt. Cada sessão pertence a um único OwnerId (visitante
/// anônimo): todas as operações validam a posse do dono.
/// </summary>
public interface IChatSessionStore
{
    string CreateSession(string ownerId);
    bool Exists(string ownerId, string sessionId);
    void Adopt(string ownerId, string sessionId, IReadOnlyList<(string User, string Assistant)> turns);
    void AddTurn(string ownerId, string sessionId, string userMessage, string assistantMessage);
    IReadOnlyList<(string User, string Assistant)> GetLastTurns(string ownerId, string sessionId, int maxTurns);
    void Touch(string ownerId, string sessionId);
    void RemoveExpiredSessions(TimeSpan idleTimeout, DateTimeOffset now);
}
