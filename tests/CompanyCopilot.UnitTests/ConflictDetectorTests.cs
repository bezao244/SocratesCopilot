using CompanyCopilot.Application.Evidence;

namespace CompanyCopilot.UnitTests;

public class ConflictDetectorTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("", "abc")]
    [InlineData("abc", "")]
    [InlineData("   ", " x ")]
    public void LexicalSimilarity_EmptyInputs_ReturnsZero(string a, string b)
    {
        Assert.Equal(0.0, ConflictDetector.LexicalSimilarity(a, b));
    }

    [Fact]
    public void LexicalSimilarity_IdenticalTexts_ReturnsOne()
    {
        Assert.Equal(1.0, ConflictDetector.LexicalSimilarity(
            "Atendimento das 8h as 18h de segunda a sexta.",
            "Atendimento das 8h as 18h de segunda a sexta."));
    }

    [Fact]
    public void LexicalSimilarity_IsAccentAndCaseInsensitive()
    {
        var similarity = ConflictDetector.LexicalSimilarity(
            "Condições de pagamento em até 12 vezes sem juros",
            "CONDICOES de pagamento em ate 12 vezes sem juros");

        Assert.Equal(1.0, similarity);
    }

    [Fact]
    public void LexicalSimilarity_DisjointTexts_ReturnsZero()
    {
        Assert.Equal(0.0, ConflictDetector.LexicalSimilarity(
            "atendimento presencial matriz segunda sexta",
            "reembolso pix cartao credito boleto"));
    }

    [Fact]
    public void IsConsistent_NearlyIdentical_ReturnsTrue()
    {
        Assert.True(ConflictDetector.IsConsistent(
            "O boleto vence em até 3 dias úteis após a emissão.",
            "O boleto vence em até 3 dias úteis após a emissão."));
    }

    [Fact]
    public void IsConflict_SemelhanteComValorDiferente_ReturnsTrue()
    {
        Assert.True(ConflictDetector.IsConflict(
            "O prazo de troca por defeito é de 30 dias após o recebimento.",
            "O prazo de troca por defeito é de 60 dias após o recebimento.",
            minSimilarity: 0.55,
            maxSimilarity: 0.97));
    }

    [Fact]
    public void IsConflict_TextosDiferentes_ReturnsFalse()
    {
        Assert.False(ConflictDetector.IsConflict(
            "O horário de atendimento é das 8h às 18h.",
            "O reembolso é creditado em até 7 dias úteis.",
            minSimilarity: 0.55,
            maxSimilarity: 0.97));
    }

    [Fact]
    public void IsConflict_TextosIdênticos_ReturnsFalse()
    {
        const string text = "O atendimento telefônico funciona das 8h às 20h.";
        Assert.False(ConflictDetector.IsConflict(text, text, minSimilarity: 0.55, maxSimilarity: 0.97));
    }
}
