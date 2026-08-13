using System.Net;

namespace CompanyCopilot.Web.Services;

/// <summary>
/// Sessão ativa do visitante no servidor Blazor (um GUID por circuito).
/// O histórico é mantido na API (persistido) e hidratado no prompt quando
/// a conversa é reaberta.
/// </summary>
public sealed class ChatSessionService
{
    private string? _sessionId;

    public string SessionId => _sessionId ??= Guid.NewGuid().ToString("N");

    public void SetSessionId(string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessionId = sessionId;
        }
    }

    public void StartNewSession() => _sessionId = Guid.NewGuid().ToString("N");
}
