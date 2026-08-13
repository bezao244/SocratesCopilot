using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.Application.Models;

/// <summary>
/// Resultado de recuperação de um trecho (chunk) para a resposta do agente.
/// </summary>
public sealed record SearchHit(
    Guid DocumentId,
    string Title,
    string Version,
    string Location,
    string Excerpt,
    double Score,
    DocumentPriority Priority,
    DocumentCategory Category);

public sealed record SearchRequest(float[] QuestionEmbedding, string QuestionText, DateOnly Today);

public interface IKnowledgeSearch
{
    /// <summary>
    /// Busca híbrida: similaridade cosseno (pgvector) + busca textual PostgreSQL
    /// configurada para português, com fusão RRF dos resultados.
    /// Consulta somente documentos Public, Approved e vigentes.
    /// </summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
