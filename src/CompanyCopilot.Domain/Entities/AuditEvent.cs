using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AuditEventType Type { get; set; }
    public Guid? DocumentId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Details { get; set; }
}
