namespace CompanyCopilot.Application.Models;

public sealed record ChatRequest(string? SessionId, string Message, string? OwnerId = null);

public sealed record ChatSourceDto(Guid DocumentId, string Title, string Version, string Location, string Excerpt);

public enum ChatEventType
{
    Session,
    Delta,
    Sources,
    Refusal,
    Error,
    Done
}

public sealed record ChatEvent(
    ChatEventType Type,
    string SessionId,
    string? Text = null,
    IReadOnlyList<ChatSourceDto>? Sources = null,
    string? ErrorMessage = null,
    bool IsFinal = false);

public sealed record ChatSessionSummary(string Id, string Title, DateTimeOffset UpdatedAtUtc, int MessageCount);

public sealed record ChatMessageRecord(string Role, string Content);
