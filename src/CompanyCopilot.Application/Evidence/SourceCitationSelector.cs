using System.Globalization;
using System.Text;
using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Evidence;

/// <summary>
/// Filtra e ordena as fontes exibidas ao usuario para evitar citacoes sem relacao
/// com a pergunta.
/// </summary>
public static class SourceCitationSelector
{
    private const double MinRelevanceScore = 0.25;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "ao", "aos", "as", "com", "como", "da", "das", "de", "do", "dos",
        "e", "em", "essa", "esse", "esta", "estou", "eu", "foi", "ha", "isso",
        "me", "meu", "minha", "na", "nas", "no", "nos", "o", "obrigada", "obrigado",
        "oi", "ola", "os", "para", "pode", "por", "qual", "quais", "que", "sem",
        "ser", "sobre", "sua", "seu", "tchau", "tem", "tenho", "uma", "um", "voce",
        "boa", "bom", "dia", "noite", "tarde"
    };

    public static IReadOnlyList<SearchHit> SelectRelevant(
        string question,
        IReadOnlyList<SearchHit> hits,
        int maxSources)
    {
        if (hits.Count == 0 || maxSources <= 0)
        {
            return Array.Empty<SearchHit>();
        }

        var questionTokens = TokenizeKeywords(question);
        if (questionTokens.Count == 0)
        {
            return Array.Empty<SearchHit>();
        }

        return hits
            .Select((hit, index) => new RankedHit(hit, index, ComputeRelevance(questionTokens, hit)))
            .Where(x => x.Relevance >= MinRelevanceScore)
            .OrderByDescending(x => x.Relevance)
            .ThenByDescending(x => x.Hit.Score)
            .ThenByDescending(x => x.Hit.Priority)
            .ThenBy(x => x.OriginalIndex)
            .Take(maxSources)
            .Select(x => x.Hit)
            .ToList();
    }

    public static string BuildDisplayExcerpt(string question, string excerpt, int maxLength = 240)
    {
        if (string.IsNullOrWhiteSpace(excerpt) || maxLength <= 0)
        {
            return string.Empty;
        }

        if (excerpt.Length <= maxLength)
        {
            return excerpt;
        }

        var questionTokens = TokenizeKeywords(question);
        if (questionTokens.Count == 0)
        {
            return TruncateWithEllipsis(excerpt, start: 0, maxLength);
        }

        var normalizedExcerpt = RemoveDiacritics(excerpt.ToLowerInvariant());
        var bestMatchIndex = FindBestTokenMatchIndex(questionTokens, normalizedExcerpt);
        if (bestMatchIndex < 0)
        {
            return TruncateWithEllipsis(excerpt, start: 0, maxLength);
        }

        var start = Math.Max(0, bestMatchIndex - (maxLength / 3));
        start = MoveToWordStart(excerpt, start);
        return TruncateWithEllipsis(excerpt, start, maxLength);
    }

    private static double ComputeRelevance(IReadOnlyList<string> questionTokens, SearchHit hit)
    {
        var metadataTokens = TokenizeKeywords($"{hit.Title} {hit.Location}");
        var excerptTokens = TokenizeKeywords(hit.Excerpt);
        var combinedTokens = metadataTokens
            .Concat(excerptTokens)
            .ToHashSet(StringComparer.Ordinal);

        var combinedCoverage = Coverage(questionTokens, combinedTokens);
        var metadataCoverage = Coverage(questionTokens, metadataTokens);
        var excerptCoverage = Coverage(questionTokens, excerptTokens);

        return (combinedCoverage * 0.5)
            + (metadataCoverage * 0.35)
            + (excerptCoverage * 0.15);
    }

    private static double Coverage(IReadOnlyList<string> questionTokens, IEnumerable<string> candidateTokens)
    {
        var candidates = candidateTokens.ToHashSet(StringComparer.Ordinal);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var matches = questionTokens.Count(candidates.Contains);
        return (double)matches / questionTokens.Count;
    }

    private static int FindBestTokenMatchIndex(
        IReadOnlyList<string> questionTokens,
        string normalizedExcerpt)
    {
        var bestIndex = -1;
        var bestTokenLength = -1;

        foreach (var token in questionTokens)
        {
            var index = normalizedExcerpt.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            if (token.Length > bestTokenLength
                || token.Length == bestTokenLength && (bestIndex < 0 || index < bestIndex))
            {
                bestTokenLength = token.Length;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static int MoveToWordStart(string excerpt, int start)
    {
        var clamped = Math.Clamp(start, 0, excerpt.Length);
        while (clamped > 0 && !char.IsWhiteSpace(excerpt[clamped - 1]))
        {
            clamped--;
        }

        return clamped;
    }

    private static string TruncateWithEllipsis(string text, int start, int maxLength)
    {
        if (text.Length == 0 || maxLength <= 0)
        {
            return string.Empty;
        }

        var safeStart = Math.Clamp(start, 0, text.Length - 1);
        var length = Math.Min(maxLength, text.Length - safeStart);
        var snippet = text.Substring(safeStart, length).Trim();

        if (safeStart > 0)
        {
            snippet = "…" + snippet;
        }

        if (safeStart + length < text.Length)
        {
            snippet += "…";
        }

        return snippet;
    }

    private static List<string> TokenizeKeywords(string text)
    {
        var normalized = RemoveDiacritics(text.ToLowerInvariant());
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record RankedHit(SearchHit Hit, int OriginalIndex, double Relevance);
}
