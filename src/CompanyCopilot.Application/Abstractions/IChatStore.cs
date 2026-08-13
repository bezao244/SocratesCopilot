using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Persistência das conversas de chat (sessões e mensagens) para a sidebar
/// de chats recentes. O histórico que alimenta o prompt continua no
/// IChatSessionStore (em memória); este store guarda o registro definitivo.
/// Toda sessão é vinculada ao OwnerId (visitante anônimo) que a criou: a
/// listagem, a leitura e a exclusão são sempre restritas ao proprietário.
/// </summary>
public interface IChatStore
{
    /// <summary>Verdadeiro se a sessão existe E pertence ao proprietário.</summary>
    Task<bool> ExistsForOwnerAsync(string sessionId, string ownerId, CancellationToken cancellationToken = default);
    /// <summary>Verdadeiro se a sessão existe no banco (independente do dono). Uso interno.</summary>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task AppendTurnAsync(
        string ownerId,
        string sessionId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken = default);
    Task<List<ChatSessionSummary>> GetRecentSessionsAsync(
        string ownerId,
        int take,
        CancellationToken cancellationToken = default);
    Task<List<ChatMessageRecord>> GetMessagesAsync(string sessionId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string sessionId, string ownerId, CancellationToken cancellationToken = default);
}
