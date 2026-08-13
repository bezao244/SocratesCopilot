using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Entities;

public class UserFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public FeedbackKind Kind { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
