using CompanyCopilot.Application.Ingest;

namespace CompanyCopilot.UnitTests;

public class TextChunkerTests
{
    [Fact]
    public void EstimateTokens_EmptyText_ReturnsZero()
    {
        Assert.Equal(0, TextChunker.EstimateTokens(string.Empty));
        Assert.Equal(0, TextChunker.EstimateTokens(null!));
    }

    [Fact]
    public void EstimateTokens_FourCharacters_ReturnsOne()
    {
        Assert.Equal(1, TextChunker.EstimateTokens("abcd"));
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        Assert.Equal("a b c", TextChunker.Normalize("a\n\n  b\t\t c"));
    }

    [Fact]
    public void Normalize_TrimsEdges()
    {
        Assert.Equal("abc", TextChunker.Normalize("   abc   "));
    }

    [Fact]
    public void ChunkText_EmptyText_ReturnsNoChunks()
    {
        Assert.Empty(TextChunker.ChunkText("   ", "doc.md"));
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        var chunks = TextChunker.ChunkText("Ola, tudo bem?", "doc.md");
        Assert.Single(chunks);
        Assert.Equal("Ola, tudo bem?", chunks[0].Content);
        Assert.Equal("doc.md", chunks[0].Location);
    }

    [Fact]
    public void ChunkText_LongText_ProducesMultipleChunksWithOverlap()
    {
        var sentences = Enumerable
            .Range(0, 60)
            .Select(i => $"Frase numero {i} sobre o atendimento da empresa.")
            .ToArray();
        var text = string.Join(" ", sentences);

        var chunks = TextChunker.ChunkText(text, "doc.md");

        Assert.True(chunks.Count >= 2);
        var overlap = chunks[1].Content.Substring(0, Math.Min(80, chunks[1].Content.Length));
        Assert.Contains(overlap, chunks[0].Content);
        Assert.All(chunks, c => Assert.Equal("doc.md", c.Location));
    }

    [Fact]
    public void ChunkText_DoesNotSplitWords()
    {
        var sentence = "PalavraCompridaDeTesteRepetidaVariasVezesParaNaoQuebrar " +
                       "PalavraCompridaDeTesteRepetidaVariasVezesParaNaoQuebrar " +
                       "PalavraCompridaDeTesteRepetidaVariasVezesParaNaoQuebrar " +
                       "PalavraCompridaDeTesteRepetidaVariasVezesParaNaoQuebrar ";
        var chunk = TextChunker.ChunkText(sentence, "doc.md");

        Assert.All(chunk, c => Assert.Contains("PalavraCompridaDeTesteRepetidaVariasVezesParaNaoQuebrar", c.Content));
    }
}
