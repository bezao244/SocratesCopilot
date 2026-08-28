using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Enums;

namespace CompanyCopilot.UnitTests;

public class SourceCitationSelectorTests
{
    private static SearchHit Hit(
        Guid documentId,
        string title,
        string excerpt,
        string location = "doc.md#1",
        double score = 0.9,
        DocumentPriority priority = DocumentPriority.Standard,
        DocumentCategory category = DocumentCategory.Faq) =>
        new(documentId, title, "1.0", location, excerpt, score, priority, category);

    [Fact]
    public void SelectRelevant_SaudacaoSemRelacao_RetornaVazio()
    {
        var hits = new[]
        {
            Hit(
                Guid.NewGuid(),
                "Horario de Atendimento",
                "O atendimento funciona de segunda a sexta das 8h as 20h.")
        };

        var selected = SourceCitationSelector.SelectRelevant("boa noite", hits, maxSources: 6);

        Assert.Empty(selected);
    }

    [Fact]
    public void SelectRelevant_HitsNaoRelacionados_RetornaVazio()
    {
        var hits = new[]
        {
            Hit(
                Guid.NewGuid(),
                "Politica de Troca",
                "Trocas por defeito podem ser solicitadas em ate 30 dias."),
            Hit(
                Guid.NewGuid(),
                "Condicoes de Pagamento",
                "Parcelamento em ate 12 vezes sem juros para compras online.")
        };

        var selected = SourceCitationSelector.SelectRelevant(
            "assinatura premium mensal",
            hits,
            maxSources: 6);

        Assert.Empty(selected);
    }

    [Fact]
    public void SelectRelevant_OrdenaPorCorrespondenciaDaPergunta()
    {
        var lessRelatedId = Guid.NewGuid();
        var moreRelatedId = Guid.NewGuid();
        var hits = new[]
        {
            Hit(
                lessRelatedId,
                "Canal de Atendimento",
                "Nosso atendimento responde em ate 2 dias uteis.",
                score: 0.99),
            Hit(
                moreRelatedId,
                "Horario de Atendimento Telefonico",
                "O atendimento telefonico funciona de segunda a sexta das 8h as 20h.",
                score: 0.7)
        };

        var selected = SourceCitationSelector.SelectRelevant(
            "qual horario de atendimento telefonico",
            hits,
            maxSources: 6);

        Assert.Equal(2, selected.Count);
        Assert.Equal(moreRelatedId, selected[0].DocumentId);
        Assert.Equal(lessRelatedId, selected[1].DocumentId);
    }

    [Fact]
    public void SelectRelevant_RespeitaLimiteMaximo()
    {
        var hits = new[]
        {
            Hit(Guid.NewGuid(), "Horario de Atendimento", "Atendimento das 8h as 20h."),
            Hit(Guid.NewGuid(), "Atendimento por Telefone", "Atendimento por telefone em horario comercial."),
            Hit(Guid.NewGuid(), "Atendimento via Chat", "Atendimento por chat em horario comercial.")
        };

        var selected = SourceCitationSelector.SelectRelevant(
            "qual horario de atendimento",
            hits,
            maxSources: 2);

        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void BuildDisplayExcerpt_RecortaAoRedorDoMelhorMatchDaPergunta()
    {
        var prefix = string.Join(' ', Enumerable.Repeat("regra de emprestimo do acervo institucional", 20));
        var excerpt = $"{prefix} O prazo para trancar a matricula segue o calendario academico vigente.";

        var display = SourceCitationSelector.BuildDisplayExcerpt(
            "qual o prazo para trancar a matricula",
            excerpt,
            maxLength: 140);

        Assert.Contains("trancar a matricula", display, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("…", display);
    }

    [Fact]
    public void BuildDisplayExcerpt_SemMatchMantemRecorteInicial()
    {
        var excerpt = string.Join(' ', Enumerable.Repeat("texto introdutorio sem relacao com a pergunta", 20));

        var display = SourceCitationSelector.BuildDisplayExcerpt(
            "assunto inexistente nas fontes",
            excerpt,
            maxLength: 90);

        Assert.StartsWith("texto introdutorio", display, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("…", display);
    }
}
