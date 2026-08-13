using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

public sealed record DocumentAdminDto(
    Guid Id,
    string Title,
    string OriginalFileName,
    string Version,
    string Category,
    string Priority,
    string Visibility,
    string Status,
    string Source,
    string Responsible,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    string ContentHashSha256,
    long FileSizeBytes,
    int ChunkCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    string? FailureMessage,
    string? ExtractedText);

public sealed record UpdateDocumentRequest(
    string Title,
    string? Version,
    string Category,
    string Priority,
    string Visibility,
    string Source,
    string Responsible,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

/// <summary>
/// Operações administrativas locais: aprovar, arquivar, rejeitar, despublicar,
/// excluir, editar metadados, reindexar e consultar o estado do conhecimento.
/// </summary>
public interface IDocumentAdminService
{
    Task<IReadOnlyList<DocumentAdminDto>> ListDocumentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngestionJobDto>> ListJobsAsync(int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<DocumentAdminDto?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string?> DeleteAsync(Guid documentId, string actor, CancellationToken cancellationToken = default);
    Task<string?> ApproveAsync(Guid documentId, string actor, CancellationToken cancellationToken = default);
    Task<string?> ArchiveAsync(Guid documentId, string actor, CancellationToken cancellationToken = default);
    Task<string?> RejectAsync(Guid documentId, string actor, string? reason, CancellationToken cancellationToken = default);
    Task<string?> UnpublishAsync(Guid documentId, string actor, CancellationToken cancellationToken = default);
    Task<string?> UpdateMetadataAsync(Guid documentId, UpdateDocumentRequest request, string actor, CancellationToken cancellationToken = default);
    Task<string?> ReindexAsync(Guid documentId, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationCaseDto>> ListEvaluationCasesAsync(CancellationToken cancellationToken = default);
}

public sealed record IngestionJobDto(
    Guid Id,
    Guid DocumentId,
    string DocumentTitle,
    string Status,
    int ProgressPercent,
    IReadOnlyList<string> Messages,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int ChunkCount,
    int FailureCount);

public sealed record AuditEventDto(
    Guid Id,
    string Type,
    Guid? DocumentId,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string? Details);

public sealed record EvaluationCaseDto(
    Guid Id,
    string Question,
    string ExpectedBehavior,
    string? ExpectedSources,
    string Category,
    string Result,
    string? ExecutionNotes,
    DateTimeOffset? LastRunAtUtc);
