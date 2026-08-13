using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CompanyCopilot.Infrastructure.Search;

/// <summary>
/// Busca híbrida: similaridade cosseno (pgvector) + busca textual PostgreSQL
/// configurada para português, com fusão RRF (Reciprocal Rank Fusion).
/// Consulta somente documentos Public, Approved e vigentes na data informada.
/// </summary>
public sealed class KnowledgeSearchService : IKnowledgeSearch
{
    private readonly CopilotDbContext _db;
    private readonly VectorSearchOptions _options;

    public KnowledgeSearchService(CopilotDbContext db, IOptions<VectorSearchOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var today = request.Today;

        var searchable = _db.KnowledgeChunks.Where(c =>
            c.Document.Status == DocumentStatus.Approved
            && c.Document.Visibility == Visibility.Public
            && (!c.Document.ValidFrom.HasValue || c.Document.ValidFrom.Value <= today)
            && (!c.Document.ValidUntil.HasValue || c.Document.ValidUntil.Value >= today));

        var vectorHits = await VectorSearchAsync(searchable, request.QuestionEmbedding, cancellationToken);
        var textHits = await TextSearchAsync(searchable, request.QuestionText, cancellationToken);

        return Fuse(vectorHits, textHits);
    }

    private Task<List<SearchHit>> VectorSearchAsync(
        IQueryable<KnowledgeChunk> searchable,
        float[] questionEmbedding,
        CancellationToken cancellationToken) =>
        searchable
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(new Vector(questionEmbedding)))
            .Take(_options.VectorSearchTake)
            .Select(c => new SearchHit(
                c.Document.Id,
                c.Document.Title,
                c.Document.Version,
                c.Location,
                c.Content,
                1.0,
                c.Document.Priority,
                c.Document.Category))
            .ToListAsync(cancellationToken);

    private Task<List<SearchHit>> TextSearchAsync(
        IQueryable<KnowledgeChunk> searchable,
        string question,
        CancellationToken cancellationToken) =>
        searchable
            .Where(c => c.TextVector != null
                && c.TextVector.Matches(EF.Functions.PlainToTsQuery("portuguese", question)))
            .OrderByDescending(c => c.TextVector!.Rank(EF.Functions.PlainToTsQuery("portuguese", question)))
            .ThenBy(c => c.DocumentId)
            .Take(_options.TextSearchTake)
            .Select(c => new SearchHit(
                c.Document.Id,
                c.Document.Title,
                c.Document.Version,
                c.Location,
                c.Content,
                1.0,
                c.Document.Priority,
                c.Document.Category))
            .ToListAsync(cancellationToken);

    private IReadOnlyList<SearchHit> Fuse(
        IReadOnlyList<SearchHit> vectorHits,
        IReadOnlyList<SearchHit> textHits)
    {
        var scores = new Dictionary<(Guid DocumentId, string Location), (SearchHit Hit, double Score)>();

        AddRanked(vectorHits, scores);
        AddRanked(textHits, scores);

        return scores.Values
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Hit.Priority)
            .Select(x => x.Hit with { Score = x.Score })
            .ToList();
    }

    private void AddRanked(
        IReadOnlyList<SearchHit> hits,
        IDictionary<(Guid DocumentId, string Location), (SearchHit Hit, double Score)> scores)
    {
        for (var i = 0; i < hits.Count; i++)
        {
            var key = (hits[i].DocumentId, hits[i].Location);
            var contribution = 1.0 / (_options.RrfK + i + 1);

            if (scores.TryGetValue(key, out var existing))
            {
                scores[key] = (existing.Hit, existing.Score + contribution);
            }
            else
            {
                scores[key] = (hits[i], contribution);
            }
        }
    }
}
