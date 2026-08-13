using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Domain.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Application.Admin;

/// <summary>
/// Ciclo administrativo local de documentos: aprovação, arquivamento, rejeição,
/// despublicação, exclusão, edição de metadados e reindexação. A busca pública jamais
/// consulta documentos internos, não aprovados ou vencidos.
/// </summary>
public sealed class DocumentAdminService : IDocumentAdminService
{
    private readonly IKnowledgeStore _store;
    private readonly StorageOptions _storage;
    private readonly ILogger<DocumentAdminService> _logger;

    public DocumentAdminService(
        IKnowledgeStore store,
        IOptions<StorageOptions> storage,
        ILogger<DocumentAdminService> logger)
    {
        _store = store;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentAdminDto>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _store.GetAllDocumentsAsync(cancellationToken);
        return documents.Select(ToDto).OrderByDescending(d => d.CreatedAtUtc).ToList();
    }

    public async Task<IReadOnlyList<IngestionJobDto>> ListJobsAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var jobs = await _store.GetRecentIngestionJobsAsync(take, cancellationToken);
        return jobs.Select(j => new IngestionJobDto(
            j.Id,
            j.DocumentId,
            j.Document?.Title ?? string.Empty,
            j.Status.ToString(),
            j.ProgressPercent,
            j.Messages,
            j.CreatedAtUtc,
            j.StartedAtUtc,
            j.FinishedAtUtc,
            j.ChunkCount,
            j.FailureCount)).ToList();
    }

    public async Task<IReadOnlyList<AuditEventDto>> ListAuditEventsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        var events = await _store.GetRecentAuditEventsAsync(take, cancellationToken);
        return events.Select(e => new AuditEventDto(
            e.Id, e.Type.ToString(), e.DocumentId, e.Actor, e.OccurredAtUtc, e.Details)).ToList();
    }

    public async Task<DocumentAdminDto?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(id, cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public async Task<string?> DeleteAsync(Guid documentId, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        await _store.DeleteDocumentAsync(document, cancellationToken);
        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentDeleted,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Excluído '{document.Title}' ({document.OriginalFileName})"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        var storedPath = Path.Combine(_storage.Root, "uploads", document.StoredFileName);
        if (File.Exists(storedPath))
        {
            try
            {
                File.Delete(storedPath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Documento {DocumentId} removido do banco, mas houve falha ao excluir o arquivo {StoredPath}",
                    document.Id,
                    storedPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Documento {DocumentId} removido do banco, mas faltou permissão para excluir o arquivo {StoredPath}",
                    document.Id,
                    storedPath);
            }
        }
        else
        {
            _logger.LogWarning(
                "Documento {DocumentId} removido do banco, mas o arquivo {StoredPath} não foi encontrado",
                document.Id,
                storedPath);
        }

        _logger.LogInformation("Documento {DocumentId} excluído por {Actor}", documentId, actor);
        return null;
    }

    public async Task<string?> ApproveAsync(Guid documentId, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (!DocumentRules.CanApprove(document))
        {
            return DocumentRules.RequiresValidityPeriod(document)
                ? "Documento Authoritative de pagamento, horário ou política exige data de início de vigência antes da aprovação."
                : "Documento não pode ser aprovado no estado atual.";
        }

        document.Status = DocumentStatus.Approved;
        document.PublishedAtUtc = DateTimeOffset.UtcNow;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentApproved,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Aprovado '{document.Title}' (v{document.Version})"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Documento {DocumentId} aprovado por {Actor}", documentId, actor);
        return null;
    }

    public async Task<string?> ArchiveAsync(Guid documentId, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (document.Status is DocumentStatus.Archived)
        {
            return "Documento já está arquivado.";
        }

        document.Status = DocumentStatus.Archived;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentArchived,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Arquivado '{document.Title}'"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> RejectAsync(Guid documentId, string actor, string? reason, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (document.Status is DocumentStatus.Rejected)
        {
            return "Documento já está rejeitado.";
        }

        document.Status = DocumentStatus.Rejected;
        document.FailureMessage = string.IsNullOrWhiteSpace(reason) ? "Rejeitado manualmente." : reason;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentRejected,
            DocumentId = document.Id,
            Actor = actor,
            Details = document.FailureMessage
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> UnpublishAsync(Guid documentId, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (document.Status != DocumentStatus.Approved)
        {
            return "Somente documentos aprovados podem ser despublicados.";
        }

        document.Status = DocumentStatus.Draft;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentUnpublished,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Despublicado '{document.Title}'"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> UpdateMetadataAsync(
        Guid documentId,
        UpdateDocumentRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "O título é obrigatório.";
        }

        if (!Enum.TryParse<DocumentCategory>(request.Category, true, out var category))
        {
            return "Categoria inválida.";
        }

        if (!Enum.TryParse<DocumentPriority>(request.Priority, true, out var priority))
        {
            return "Prioridade inválida.";
        }

        if (!Enum.TryParse<Visibility>(request.Visibility, true, out var visibility))
        {
            return "Visibilidade inválida.";
        }

        document.Title = request.Title.Trim();
        document.Version = string.IsNullOrWhiteSpace(request.Version) ? document.Version : request.Version.Trim();
        document.Category = category;
        document.Priority = priority;
        document.Visibility = visibility;
        document.Source = request.Source ?? string.Empty;
        document.Responsible = request.Responsible ?? string.Empty;
        document.ValidFrom = request.ValidFrom;
        document.ValidUntil = request.ValidUntil;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.MetadataUpdated,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Metadados atualizados para '{document.Title}' (categoria {category}, prioridade {priority}, visibilidade {visibility})"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<string?> ReindexAsync(Guid documentId, string actor, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return "Documento não encontrado.";
        }

        if (!DocumentRules.CanTransition(document.Status, DocumentStatus.Processing))
        {
            return "Documento não pode ser reindexado no estado atual.";
        }

        document.Status = DocumentStatus.Draft;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var job = new IngestionJob
        {
            DocumentId = document.Id,
            Status = IngestionJobStatus.Queued,
            Messages = new List<string> { "Reindexação solicitada." }
        };

        await _store.AddIngestionJobAsync(job, cancellationToken);
        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentReindexed,
            DocumentId = document.Id,
            Actor = actor,
            Details = $"Reindexação solicitada para '{document.Title}'"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<IReadOnlyList<EvaluationCaseDto>> ListEvaluationCasesAsync(CancellationToken cancellationToken = default)
    {
        var cases = await _store.GetAllEvaluationCasesAsync(cancellationToken);
        return cases.Select(c => new EvaluationCaseDto(
            c.Id,
            c.Question,
            c.ExpectedBehavior,
            c.ExpectedSources,
            c.Category.ToString(),
            c.Result.ToString(),
            c.ExecutionNotes,
            c.LastRunAtUtc)).ToList();
    }

    private static DocumentAdminDto ToDto(KnowledgeDocument d) =>
        new(
            d.Id,
            d.Title,
            d.OriginalFileName,
            d.Version,
            d.Category.ToString(),
            d.Priority.ToString(),
            d.Visibility.ToString(),
            d.Status.ToString(),
            d.Source,
            d.Responsible,
            d.ValidFrom,
            d.ValidUntil,
            d.ContentHashSha256,
            d.FileSizeBytes,
            d.Chunks.Count,
            d.CreatedAtUtc,
            d.PublishedAtUtc,
            d.FailureMessage,
            d.ExtractedText);
}
