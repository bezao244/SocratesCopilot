using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Entities;

public class EvaluationCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Question { get; set; } = string.Empty;
    public string ExpectedBehavior { get; set; } = string.Empty;
    public string? ExpectedSources { get; set; }
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public EvaluationResult Result { get; set; } = EvaluationResult.Pending;
    public string? ExecutionNotes { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
}
