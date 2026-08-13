using System.Text;
using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Ingest;

/// <summary>
/// Fragmentação de texto em trechos de aproximadamente 500 tokens
/// com sobreposição de 80 tokens, quebrando em limites de frase quando possível
/// e preservando a localização de origem.
/// Estimativa de tokens: ~4 caracteres por token (heurística determinística).
/// </summary>
public static class TextChunker
{
    public const int DefaultMaxTokens = 500;
    public const int DefaultOverlapTokens = 80;

    public static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    public static List<TextChunk> ChunkText(
        string text,
        string location,
        int maxTokens = DefaultMaxTokens,
        int overlapTokens = DefaultOverlapTokens)
    {
        var result = new List<TextChunk>();
        var normalized = Normalize(text);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return result;
        }

        var maxChars = Math.Max(64, maxTokens * 4);
        var overlapChars = Math.Max(0, overlapTokens * 4);

        if (EstimateTokens(normalized) <= maxTokens)
        {
            result.Add(new TextChunk(normalized.Trim(), location));
            return result;
        }

        var sentences = SplitSentences(normalized);
        var current = new StringBuilder();
        var ordinal = 0;

        foreach (var sentence in sentences)
        {
            if (current.Length + sentence.Length + 1 > maxChars && current.Length > 0)
            {
                result.Add(new TextChunk(current.ToString().Trim(), location));
                ordinal++;
                current.Clear();

                if (result.Count > 0)
                {
                    var previous = result[^1].Content;
                    current.Append(previous.Substring(Math.Max(0, previous.Length - overlapChars)));
                }
            }

            current.Append(' ').Append(sentence);
        }

        if (current.Length > 0)
        {
            result.Add(new TextChunk(current.ToString().Trim(), location));
        }

        return result;
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasWhitespace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    builder.Append(' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(c);
                lastWasWhitespace = false;
            }
        }

        return builder.ToString().Trim();
    }

    private static List<string> SplitSentences(string text)
    {
        var sentences = new List<string>();
        var current = new StringBuilder();

        foreach (var c in text)
        {
            current.Append(c);

            if (c is '.' or '!' or '?' or '\u2026')
            {
                sentences.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            sentences.Add(current.ToString());
        }

        return sentences;
    }
}
