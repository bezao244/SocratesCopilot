using System.Security.Cryptography;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Ingest;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Domain.Rules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Application.Ingest;

/// <summary>
/// Ingestão de documentos: valida upload, extrai texto, fragmenta em trechos
/// de ~500 tokens com sobreposição de 80, gera embeddings (pausando enquanto
/// houver chat ativo) e indexa no pgvector.
/// </summary>
public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private const int EmbeddingProgressStep = 5;

    private readonly IKnowledgeStore _store;
    private readonly IDocumentExtractor _extractor;
    private readonly IOllamaClient _ollama;
    private readonly IGenerationGate _generationGate;
    private readonly StorageOptions _storage;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IKnowledgeStore store,
        IDocumentExtractor extractor,
        IOllamaClient ollama,
        IGenerationGate generationGate,
        IOptions<StorageOptions> storage,
        ILogger<DocumentIngestionService> logger)
    {
        _store = store;
        _extractor = extractor;
        _ollama = ollama;
        _generationGate = generationGate;
        _storage = storage.Value;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        Stream fileStream,
        string originalFileName,
        string title,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !_storage.AllowedExtensions.Contains(extension))
        {
            return new ImportResult(ImportOutcome.AlreadyExists, null,
                $"Extensão '{extension}' não permitida. Extensões aceitas: {string.Join(", ", _storage.AllowedExtensions)}.");
        }

        if (fileStream.Length > _storage.MaxUploadBytes)
        {
            return new ImportResult(ImportOutcome.AlreadyExists, null,
                $"Arquivo maior que o limite de {_storage.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        await using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        var existing = await _store.GetDocumentByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            return new ImportResult(ImportOutcome.AlreadyExists, existing.Id,
                "Um documento idêntico (mesmo hash) já está cadastrado.");
        }

        var uploadsDir = Path.Combine(_storage.Root, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(uploadsDir, storedFileName);
        await File.WriteAllBytesAsync(storedPath, bytes, cancellationToken);

        var document = new KnowledgeDocument
        {
            Title = string.IsNullOrWhiteSpace(title) ? originalFileName : title,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            FileExtension = extension,
            FileSizeBytes = bytes.Length,
            ContentHashSha256 = hash,
            Status = DocumentStatus.Processing
        };

        var job = new IngestionJob
        {
            Document = document,
            DocumentId = document.Id,
            Status = IngestionJobStatus.Queued,
            ProgressPercent = 0,
            Messages = new List<string> { "Na fila de processamento." }
        };

        await _store.AddDocumentAsync(document, cancellationToken);
        await _store.AddIngestionJobAsync(job, cancellationToken);
        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentImported,
            DocumentId = document.Id,
            Actor = "admin-local",
            Details = $"Importado '{originalFileName}' ({bytes.Length} bytes)"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Documento {DocumentId} enfileirado para ingestão", document.Id);
        return new ImportResult(ImportOutcome.Imported, document.Id, null);
    }

    public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var job = (await _store.GetRecentIngestionJobsAsync(20, cancellationToken))
            .FirstOrDefault(j => j.DocumentId == documentId && j.Status == IngestionJobStatus.Queued);
        if (job is null)
        {
            return;
        }

        job.Status = IngestionJobStatus.Running;
        job.StartedAtUtc = DateTimeOffset.UtcNow;
        job.Messages = new List<string> { "Processando documento..." };
        document.Status = DocumentStatus.Processing;
        await _store.SaveChangesAsync(cancellationToken);

        var storedPath = Path.Combine(_storage.Root, "uploads", document.StoredFileName);
        if (!File.Exists(storedPath))
        {
            await FailAsync(document, job, "Arquivo original não encontrado no armazenamento.", cancellationToken);
            return;
        }

        try
        {
            job.ProgressPercent = 10;
            job.Messages = new List<string> { "Extraindo texto do arquivo..." };
            await _store.SaveChangesAsync(cancellationToken);

            var extracted = await _extractor.ExtractAsync(storedPath, document.FileExtension, cancellationToken);

            if (!string.IsNullOrEmpty(extracted.FailureMessage))
            {
                document.FailureMessage = extracted.FailureMessage;
                await RejectAsync(document, job, extracted.FailureMessage, cancellationToken);
                return;
            }

            if (extracted.Sections.Count == 0)
            {
                document.FailureMessage = "Nenhum texto extraível foi encontrado no arquivo.";
                await RejectAsync(document, job, document.FailureMessage, cancellationToken);
                return;
            }

            job.ProgressPercent = 30;
            job.Messages = new List<string> { "Fragmentando texto em trechos..." };

            var chunks = BuildChunks(extracted.Sections);
            document.ExtractedText = string.Join("\n\n", extracted.Sections.Select(s => s.Text));

            job.ProgressPercent = 40;
            job.Messages = new List<string> { $"Gerando embeddings ({chunks.Count} trechos)..." };
            await _store.SaveChangesAsync(cancellationToken);

            var embedded = new List<KnowledgeChunk>(chunks.Count);
            var failureCount = 0;

            for (var i = 0; i < chunks.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _generationGate.WaitForEmbeddingSlotAsync(cancellationToken);

                try
                {
                    var embedding = await _ollama.EmbedAsync(chunks[i].Content, cancellationToken);
                    embedded.Add(new KnowledgeChunk
                    {
                        DocumentId = document.Id,
                        Ordinal = i,
                        Content = chunks[i].Content,
                        Location = chunks[i].Location,
                        Embedding = new Pgvector.Vector(embedding),
                        IndexedAtUtc = DateTimeOffset.UtcNow
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogWarning(ex, "Falha ao gerar embedding do trecho {Ordinal} do documento {DocumentId}", i, document.Id);
                }

                if ((i + 1) % EmbeddingProgressStep == 0)
                {
                    job.ProgressPercent = 40 + (int)((i + 1) * 50.0 / chunks.Count);
                    await _store.SaveChangesAsync(cancellationToken);
                }
            }

            if (embedded.Count == 0)
            {
                await FailAsync(document, job, "Não foi possível gerar embeddings para nenhum trecho.", cancellationToken);
                return;
            }

            await _store.ReplaceChunksAsync(document.Id, embedded, cancellationToken);
            document.ExtractedText = string.Join("\n\n", extracted.Sections.Select(s => s.Text));
            document.FailureMessage = null;
            document.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _store.SaveChangesAsync(cancellationToken);

            job.Status = IngestionJobStatus.Completed;
            job.ProgressPercent = 100;
            job.FinishedAtUtc = DateTimeOffset.UtcNow;
            job.ChunkCount = embedded.Count;
            job.FailureCount = failureCount;
            job.Messages = new List<string>
            {
                $"Processamento concluído: {embedded.Count} trechos indexados"
                + (failureCount > 0 ? $" ({failureCount} falhas)" : string.Empty)
                + ". Documento pronto para aprovação."
            };
            await _store.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Documento {DocumentId} indexado com {ChunkCount} trechos ({FailureCount} falhas)",
                document.Id, embedded.Count, failureCount);
        }
        catch (OperationCanceledException)
        {
            job.Status = IngestionJobStatus.Queued;
            job.Messages = new List<string> { "Processamento interrompido; requeue para tentar novamente." };
            document.Status = DocumentStatus.Draft;
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar documento {DocumentId}", document.Id);
            await FailAsync(document, job, "Erro inesperado durante o processamento.", cancellationToken);
        }
    }

    public async Task ReindexAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _store.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        if (!DocumentRules.CanTransition(document.Status, DocumentStatus.Processing))
        {
            return;
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
            Actor = "admin-local",
            Details = $"Reindexação solicitada para '{document.Title}'"
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
    }

    private static List<TextChunk> BuildChunks(IReadOnlyList<ExtractedSection> sections)
    {
        var chunks = new List<TextChunk>();

        foreach (var section in sections)
        {
            var sectionChunks = TextChunker.ChunkText(section.Text, section.Location);
            foreach (var chunk in sectionChunks)
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private async Task RejectAsync(
        KnowledgeDocument document,
        IngestionJob job,
        string message,
        CancellationToken cancellationToken)
    {
        document.Status = DocumentStatus.Rejected;
        document.FailureMessage = message;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        job.Status = IngestionJobStatus.Failed;
        job.FinishedAtUtc = DateTimeOffset.UtcNow;
        job.FailureCount = 1;
        job.Messages = new List<string> { message };

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentFailed,
            DocumentId = document.Id,
            Actor = "worker-ingestao",
            Details = message
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("Documento {DocumentId} rejeitado: {Message}", document.Id, message);
    }

    private async Task FailAsync(
        KnowledgeDocument document,
        IngestionJob job,
        string message,
        CancellationToken cancellationToken)
    {
        document.Status = DocumentStatus.Failed;
        document.FailureMessage = message;
        document.UpdatedAtUtc = DateTimeOffset.UtcNow;

        job.Status = IngestionJobStatus.Failed;
        job.FinishedAtUtc = DateTimeOffset.UtcNow;
        job.FailureCount = 1;
        job.Messages = new List<string> { message };

        await _store.AddAuditEventAsync(new AuditEvent
        {
            Type = AuditEventType.DocumentFailed,
            DocumentId = document.Id,
            Actor = "worker-ingestao",
            Details = message
        }, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);

        _logger.LogError("Documento {DocumentId} falhou: {Message}", document.Id, message);
    }
}
