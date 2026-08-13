using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Admin;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Domain.Entities;
using CompanyCopilot.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.UnitTests;

public sealed class DocumentAdminServiceTests
{
    [Fact]
    public async Task DeleteAsync_DocumentoInexistente_RetornaErro()
    {
        var store = new FakeKnowledgeStore();
        var service = CreateService(store, Path.GetTempPath());

        var error = await service.DeleteAsync(Guid.NewGuid(), "admin-local");

        Assert.Equal("Documento não encontrado.", error);
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Fact]
    public async Task DeleteAsync_DocumentoExistente_RemoveDocumentoEArquivo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"companycopilot-delete-{Guid.NewGuid():N}");
        var uploads = Path.Combine(root, "uploads");
        Directory.CreateDirectory(uploads);

        try
        {
            var document = new KnowledgeDocument
            {
                Title = "Política de Férias",
                OriginalFileName = "ferias.md",
                StoredFileName = "ferias-store.md",
                FileExtension = ".md",
                ContentHashSha256 = "ABC123",
                Status = DocumentStatus.Approved
            };

            var storedPath = Path.Combine(uploads, document.StoredFileName);
            await File.WriteAllTextAsync(storedPath, "conteudo");

            var store = new FakeKnowledgeStore();
            store.Documents[document.Id] = document;

            var service = CreateService(store, root);
            var error = await service.DeleteAsync(document.Id, "admin-local");

            Assert.Null(error);
            Assert.Empty(store.Documents);
            Assert.False(File.Exists(storedPath));
            Assert.Equal(1, store.SaveChangesCalls);
            Assert.Single(store.AuditEvents);
            Assert.Equal(AuditEventType.DocumentDeleted, store.AuditEvents[0].Type);
            Assert.Equal(document.Id, store.AuditEvents[0].DocumentId);
            Assert.Equal("admin-local", store.AuditEvents[0].Actor);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static DocumentAdminService CreateService(FakeKnowledgeStore store, string root) =>
        new(
            store,
            Options.Create(new StorageOptions { Root = root }),
            NullLogger<DocumentAdminService>.Instance);

    private sealed class FakeKnowledgeStore : IKnowledgeStore
    {
        public Dictionary<Guid, KnowledgeDocument> Documents { get; } = new();
        public List<AuditEvent> AuditEvents { get; } = new();
        public int SaveChangesCalls { get; private set; }

        public Task<KnowledgeDocument?> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.TryGetValue(id, out var document) ? document : null);

        public Task<KnowledgeDocument?> GetDocumentByHashAsync(string hash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.Values.FirstOrDefault(d => d.ContentHashSha256 == hash));

        public Task AddDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
        {
            Documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task DeleteDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Remove(document.Id);
            return Task.CompletedTask;
        }

        public Task<List<KnowledgeDocument>> GetAllDocumentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Documents.Values.ToList());

        public Task<List<IngestionJob>> GetRecentIngestionJobsAsync(int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<IngestionJob>());

        public Task<IngestionJob?> GetIngestionJobAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IngestionJob?>(null);

        public Task<List<IngestionJob>> GetQueuedIngestionJobsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<IngestionJob>());

        public Task AddIngestionJobAsync(IngestionJob job, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            AuditEvents.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<List<AuditEvent>> GetRecentAuditEventsAsync(int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuditEvents.Take(take).ToList());

        public Task<List<EvaluationCase>> GetAllEvaluationCasesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<EvaluationCase>());

        public Task AddEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateEvaluationCaseAsync(EvaluationCase evaluationCase, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }
}
