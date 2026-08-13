using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.UnitTests;

public class EvidenceEvaluatorTests
{
    private static readonly VectorSearchOptions Options = new();
    private static readonly Guid DocumentA = Guid.NewGuid();
    private static readonly Guid DocumentB = Guid.NewGuid();

    private static SearchHit Hit(
        Guid documentId,
        string excerpt,
        DocumentPriority priority = DocumentPriority.Standard,
        DocumentCategory category = DocumentCategory.Faq,
        double score = 0.9) =>
        new(documentId, "Titulo", "1.0", "doc.md#1", excerpt, score, priority, category);

    [Fact]
    public void Evaluate_SemTrechos_RetornaNoEvidence()
    {
        var result = new EvidenceEvaluator(Options).Evaluate(Array.Empty<SearchHit>(), maxChunks: 6);

        Assert.Equal(EvidenceVerdict.NoEvidence, result.Verdict);
        Assert.Empty(result.SelectedHits);
    }

    [Fact]
    public void Evaluate_UnicoTrecho_RetornaSupported()
    {
        var hits = new[] { Hit(DocumentA, "O boleto vence em até 3 dias úteis.") };
        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 6);

        Assert.Equal(EvidenceVerdict.Supported, result.Verdict);
        Assert.Single(result.SelectedHits);
    }

    [Fact]
    public void Evaluate_ConflitoEntreFontesDistintasDaMesmaPrioridade_RetornaConflict()
    {
        var hits = new[]
        {
            Hit(DocumentA, "O prazo de troca por defeito é de 30 dias após o recebimento.", category: DocumentCategory.Policy, priority: DocumentPriority.Authoritative),
            Hit(DocumentB, "O prazo de troca por defeito é de 60 dias após o recebimento.", category: DocumentCategory.Policy, priority: DocumentPriority.Authoritative)
        };

        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 6);

        Assert.Equal(EvidenceVerdict.Conflict, result.Verdict);
        Assert.Equal(ChatMessages.ConflictRefusal, result.RefusalMessage);
        Assert.Empty(result.SelectedHits);
    }

    [Fact]
    public void Evaluate_TrechosDoMesmoDocumento_NaoSaoConflito()
    {
        var hits = new[]
        {
            Hit(DocumentA, "O prazo de troca por defeito é de 30 dias após o recebimento."),
            Hit(DocumentA, "O prazo de troca por defeito é de 60 dias após o recebimento.")
        };

        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 6);

        Assert.Equal(EvidenceVerdict.Supported, result.Verdict);
    }

    [Fact]
    public void Evaluate_PriorizaCategoriaAutoritativaSobreFaq()
    {
        var hits = new[]
        {
            Hit(DocumentA, "Resposta genérica de FAQ.", category: DocumentCategory.Faq, priority: DocumentPriority.Standard, score: 0.99),
            Hit(DocumentB, "O reembolso é creditado em até 7 dias úteis.", category: DocumentCategory.PaymentConditions, priority: DocumentPriority.Authoritative, score: 0.8)
        };

        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 6);

        Assert.Equal(EvidenceVerdict.Supported, result.Verdict);
        Assert.Equal(2, result.SelectedHits.Count);
        Assert.Equal(DocumentB, result.SelectedHits[0].DocumentId);
    }

    [Fact]
    public void Evaluate_RespeitaLimiteDeTrechos()
    {
        var unique = new[]
        {
            "alpha beta gamma delta", "epsilon zeta eta theta", "iota kappa lambda mu",
            "nu xi omicron pi", "rho sigma tau upsilon", "phi chi psi omega",
            "abaco baquelite cimento dureza", "esmeril fonte gesso hulha",
            "iglu jacare kiwi lobo", "manga nave onca puma"
        };
        var hits = unique
            .Select((words, i) => Hit(Guid.NewGuid(), words, category: DocumentCategory.Faq))
            .ToArray();

        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 3);

        Assert.Equal(EvidenceVerdict.Supported, result.Verdict);
        Assert.Equal(3, result.SelectedHits.Count);
    }

    [Fact]
    public void Evaluate_TrechosSemelhantesDePrioridadesDiferentes_NaoSaoConflito()
    {
        var hits = new[]
        {
            Hit(DocumentA, "O prazo de troca por defeito é de 30 dias após o recebimento.", priority: DocumentPriority.Authoritative, category: DocumentCategory.Policy),
            Hit(DocumentB, "O prazo de troca por defeito é de 60 dias após o recebimento.", priority: DocumentPriority.Standard, category: DocumentCategory.Faq)
        };

        var result = new EvidenceEvaluator(Options).Evaluate(hits, maxChunks: 6);

        Assert.Equal(EvidenceVerdict.Supported, result.Verdict);
    }
}
