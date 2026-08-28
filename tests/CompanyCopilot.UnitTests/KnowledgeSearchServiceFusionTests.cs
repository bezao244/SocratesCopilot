using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Enums;
using CompanyCopilot.Infrastructure.Search;

namespace CompanyCopilot.UnitTests;

public class KnowledgeSearchServiceFusionTests
{
    private static SearchHit Hit(
        Guid documentId,
        string location,
        string excerpt,
        double score = 1.0,
        DocumentPriority priority = DocumentPriority.Standard,
        DocumentCategory category = DocumentCategory.Faq) =>
        new(documentId, "Documento", "1.0", location, excerpt, score, priority, category);

    [Fact]
    public void Fuse_ChunksDistintosMesmaLocation_NaoColapsaExcertosDiferentes()
    {
        var documentId = Guid.NewGuid();
        var vectorHits = new[]
        {
            Hit(documentId, "pagina 35", "Trecho A sobre biblioteca."),
            Hit(documentId, "pagina 35", "Trecho B sobre trancamento de matricula.")
        };

        var fused = KnowledgeSearchService.Fuse(vectorHits, Array.Empty<SearchHit>(), rrfK: 60);

        Assert.Equal(2, fused.Count);
        Assert.Contains(fused, x => x.Excerpt.Contains("Trecho A", StringComparison.Ordinal));
        Assert.Contains(fused, x => x.Excerpt.Contains("Trecho B", StringComparison.Ordinal));
    }

    [Fact]
    public void Fuse_MesmoTrechoEmDoisModos_AgregaScoreSemDuplicar()
    {
        var hit = Hit(Guid.NewGuid(), "pagina 12", "Trecho unico sobre prazo de trancamento.");
        var fused = KnowledgeSearchService.Fuse(new[] { hit }, new[] { hit }, rrfK: 60);

        Assert.Single(fused);
        Assert.Equal(2.0 / 61.0, fused[0].Score, precision: 10);
    }
}
