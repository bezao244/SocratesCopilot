using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Entities;

public class IngestionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;
    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Queued;
    public int ProgressPercent { get; set; }
    public List<string> Messages { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public int ChunkCount { get; set; }
    public int FailureCount { get; set; }
}
