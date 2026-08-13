using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

public sealed record ImportResult(ImportOutcome Outcome, Guid? DocumentId, string? Error);

public enum ImportOutcome
{
    Imported,
    AlreadyExists
}

/// <summary>
/// Ciclo de ingestão: validação de upload (extensão, tamanho, hash), extração,
/// fragmentação, embeddings (pausados durante geração de chat) e indexação.
/// </summary>
public interface IDocumentIngestionService
{
    Task<ImportResult> ImportAsync(
        Stream fileStream,
        string originalFileName,
        string title,
        CancellationToken cancellationToken = default);

    Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task ReindexAsync(Guid documentId, CancellationToken cancellationToken = default);
}
