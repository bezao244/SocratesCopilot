using System.Globalization;
using System.Text;

namespace CompanyCopilot.Application.Evidence;

/// <summary>
/// Detecção determinística de conflito entre trechos: duas fontes da mesma
/// prioridade com conteúdo lexicamente semelhante, porém não idêntico,
/// indicam afirmações incompatíveis — nesse caso o agente deve recusar.
/// </summary>
public static class ConflictDetector
{
    /// <summary>
    /// Similaridade de Jaccard por palavras normalizadas (minúsculas, sem acentos,
    /// pontuação e cifrões), tolerando diferenças de redação.
    /// </summary>
    public static double LexicalSimilarity(string a, string b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);

        if (tokensA.Count == 0 || tokensB.Count == 0)
        {
            return 0.0;
        }

        var setA = tokensA.ToHashSet();
        var setB = tokensB.ToHashSet();

        var intersection = setA.Count(t => setB.Contains(t));
        var union = setA.Count + setB.Count - intersection;

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// Textos praticamente idênticos indicam fontes consistentes entre si.
    /// </summary>
    public static bool IsConsistent(string a, string b, double identicalThreshold = 0.97) =>
        LexicalSimilarity(a, b) >= identicalThreshold;

    /// <summary>
    /// Textos semelhantes, mas com diferenças (ex.: horários, valores), são conflito.
    /// </summary>
    public static bool IsConflict(string a, string b, double minSimilarity, double maxSimilarity)
    {
        var similarity = LexicalSimilarity(a, b);
        return similarity >= minSimilarity && similarity < maxSimilarity;
    }

    private static List<string> Tokenize(string text)
    {
        var normalized = RemoveDiacritics(text.ToLowerInvariant());
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
