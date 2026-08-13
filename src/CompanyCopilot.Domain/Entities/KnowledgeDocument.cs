using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Domain.Entities;

public class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentHashSha256 { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public DocumentPriority Priority { get; set; } = DocumentPriority.Standard;
    public Visibility Visibility { get; set; } = Visibility.Public;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public string Source { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? ExtractedText { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
    public ICollection<IngestionJob> IngestionJobs { get; set; } = new List<IngestionJob>();
}
