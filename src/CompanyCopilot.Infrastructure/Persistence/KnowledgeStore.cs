using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CompanyCopilot.Infrastructure.Persistence;

public sealed class KnowledgeStore : IKnowledgeStore
{
    private readonly CopilotDbContext _db;

    public KnowledgeStore(CopilotDbContext db)
    {
        _db = db;
    }

    public Task<KnowledgeDocument?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.KnowledgeDocuments
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<KnowledgeDocument?> GetDocumentByHashAsync(string hash, CancellationToken cancellationToken = default) =>
        _db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.ContentHashSha256 == hash, cancellationToken);

    public async Task AddDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        await _db.KnowledgeDocuments.AddAsync(document, cancellationToken);
    }

    public Task DeleteDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        _db.KnowledgeDocuments.Remove(document);
        return Task.CompletedTask;
    }

    public Task<List<KnowledgeDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default) =>
        _db.KnowledgeDocuments
            .Include(d => d.Chunks)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<List<IngestionJob>> GetRecentIngestionJobsAsync(int take, CancellationToken cancellationToken = default) =>
        _db.IngestionJobs
            .Include(j => j.Document)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<IngestionJob?> GetIngestionJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.IngestionJobs
            .Include(j => j.Document)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<List<IngestionJob>> GetQueuedIngestionJobsAsync(CancellationToken cancellationToken = default) =>
        _db.IngestionJobs
            .Include(j => j.Document)
            .Where(j => j.Status == IngestionJobStatus.Queued)
            .OrderBy(j => j.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

    public async Task AddIngestionJobAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        await _db.IngestionJobs.AddAsync(job, cancellationToken);
    }

    public async Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.KnowledgeChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        _db.KnowledgeChunks.RemoveRange(existing);
        await _db.KnowledgeChunks.AddRangeAsync(chunks, cancellationToken);
    }

    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await _db.AuditEvents.AddAsync(auditEvent, cancellationToken);
    }

    public async Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken = default)
    {
        await _db.UserFeedbacks.AddAsync(feedback, cancellationToken);
    }

    public Task<List<AuditEvent>> GetRecentAuditEventsAsync(int take, CancellationToken cancellationToken = default) =>
        _db.AuditEvents
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<List<EvaluationCase>> GetAllEvaluationCasesAsync(CancellationToken cancellationToken = default) =>
        _db.EvaluationCases
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task AddEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default)
    {
        await _db.EvaluationCases.AddAsync(evaluationCase, cancellationToken);
    }

    public void UpdateEvaluationCase(EvaluationCase evaluationCase)
    {
        _db.EvaluationCases.Update(evaluationCase);
    }

    public Task UpdateEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default)
    {
        _db.EvaluationCases.Update(evaluationCase);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
