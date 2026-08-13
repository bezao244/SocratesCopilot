using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Domain.Rules;

namespace CompanyCopilot.Application.Evidence;

public enum EvidenceVerdict
{
    Supported,
    NoEvidence,
    Conflict
}

public sealed record EvidenceResult(
    EvidenceVerdict Verdict,
    IReadOnlyList<SearchHit> SelectedHits,
    string? RefusalMessage = null);

/// <summary>
/// Verifica se há evidência suficiente nos trechos recuperados e detecta
/// conflitos entre fontes de mesma prioridade. Sem evidência ou com conflito,
/// o agente não chama o modelo para inventar resposta.
/// </summary>
public sealed class EvidenceEvaluator
{
    private readonly VectorSearchOptions _options;

    public EvidenceEvaluator(VectorSearchOptions options)
    {
        _options = options;
    }

    public EvidenceResult Evaluate(IReadOnlyList<SearchHit> hits, int maxChunks)
    {
        if (hits.Count == 0)
        {
            return new EvidenceResult(EvidenceVerdict.NoEvidence, Array.Empty<SearchHit>());
        }

        var ranked = hits
            .Select(h => (Hit: h, Tier: ComputeTier(h)))
            .OrderByDescending(x => x.Tier.CategoryRank)
            .ThenByDescending(x => x.Tier.Priority)
            .ThenByDescending(x => x.Hit.Score)
            .ToList();

        var topTier = ranked
            .Where(x => x.Tier == ranked[0].Tier)
            .Select(x => x.Hit)
            .ToList();

        if (topTier.Count >= 2)
        {
            for (var i = 0; i < topTier.Count - 1; i++)
            {
                for (var j = i + 1; j < topTier.Count; j++)
                {
                    if (topTier[i].DocumentId == topTier[j].DocumentId)
                    {
                        continue;
                    }

                    if (ConflictDetector.IsConflict(
                            topTier[i].Excerpt,
                            topTier[j].Excerpt,
                            _options.ConflictSimilarityMin,
                            _options.ConflictSimilarityMax))
                    {
                        return new EvidenceResult(
                            EvidenceVerdict.Conflict,
                            Array.Empty<SearchHit>(),
                            ChatMessages.ConflictRefusal);
                    }
                }
            }
        }

        var selected = ranked.Select(x => x.Hit).Take(maxChunks).ToList();
        return new EvidenceResult(EvidenceVerdict.Supported, selected);
    }

    private static (int CategoryRank, DocumentPriority Priority) ComputeTier(SearchHit hit)
    {
        var categoryRank = DocumentRules.IsAuthoritativeCategory(hit.Category) ? 1 : 0;
        return (categoryRank, hit.Priority);
    }
}
