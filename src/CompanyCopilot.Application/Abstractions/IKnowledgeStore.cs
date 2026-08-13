using CompanyCopilot.Domain.Entities;

namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Acesso a dados usado pelos casos de uso (implementado com EF Core na infraestrutura).
/// </summary>
public interface IKnowledgeStore
{
    Task<KnowledgeDocument?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KnowledgeDocument?> GetDocumentByHashAsync(string hash, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task<List<KnowledgeDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task<List<IngestionJob>> GetRecentIngestionJobsAsync(int take, CancellationToken cancellationToken = default);
    Task<IngestionJob?> GetIngestionJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<IngestionJob>> GetQueuedIngestionJobsAsync(CancellationToken cancellationToken = default);
    Task AddIngestionJobAsync(IngestionJob job, CancellationToken cancellationToken = default);
    Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);
    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken = default);
    Task<List<AuditEvent>> GetRecentAuditEventsAsync(int take, CancellationToken cancellationToken = default);
    Task<List<EvaluationCase>> GetAllEvaluationCasesAsync(CancellationToken cancellationToken = default);
    Task AddEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default);
    Task UpdateEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
