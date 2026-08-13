using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CompanyCopilot.Api;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Chat;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Application.RateLimiting;
using CompanyCopilot.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.IntegrationTests;

public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    public IReadOnlyList<SearchHit> Hits { get; set; } = Array.Empty<SearchHit>();
    public Func<ChatMessage, string> StreamStep { get; set; } = _ => "Resposta de teste.";
    public bool FailEmbedding { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Copilot",
            "Host=127.0.0.1;Port=5432;Database=company_copilot;Username=copilot;Password=change-me");
        builder.UseSetting("Chat:MaxQuestionsPerWindow", "100000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IOllamaClient>();
            services.AddScoped<IOllamaClient>(_ => new FakeOllamaClient(this));

            services.RemoveAll<IKnowledgeSearch>();
            services.AddScoped<IKnowledgeSearch>(_ => new FakeSearch(this));
        });
    }

    private sealed class FakeOllamaClient : IOllamaClient
    {
        private readonly ChatApiFactory _factory;

        public FakeOllamaClient(ChatApiFactory factory) => _factory = factory;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            if (_factory.FailEmbedding)
            {
                throw new HttpRequestException("embedding indisponível");
            }

            var embedding = new float[768];
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (text.Length + i) % 13 * 0.01f;
            }

            return Task.FromResult(embedding);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            IReadOnlyList<ChatMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var text = _factory.StreamStep(messages[0]);

            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            foreach (var chunk in text.Split(' '))
            {
                await Task.Delay(5, cancellationToken);
                yield return chunk + " ";
            }
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> HasModelAsync(string model, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeSearch : IKnowledgeSearch
    {
        private readonly ChatApiFactory _factory;

        public FakeSearch(ChatApiFactory factory) => _factory = factory;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            SearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(_factory.Hits);
    }
}

public sealed class ChatEndpointTests : IClassFixture<ChatApiFactory>
{
    private readonly ChatApiFactory _factory;

    public ChatEndpointTests(ChatApiFactory factory) => _factory = factory;

    private static HttpRequestMessage ChatRequest(string message, string? sessionId = null) =>
        new(HttpMethod.Post, "/api/v1/chat/messages")
        {
            Content = JsonContent.Create(new { message, sessionId })
        };

    private static async Task<List<JsonElement>> ReadEventsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();
    }

    private void ResetFactory()
    {
        _factory.Hits = Array.Empty<SearchHit>();
        _factory.FailEmbedding = false;
        _factory.StreamStep = _ => "Resposta de teste.";
    }

    private static HttpClient CreateClient(ChatApiFactory factory, string? ownerId = null)
    {
        var client = factory.CreateClient();
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            client.DefaultRequestHeaders.Add("X-Copilot-Visitor-Id", ownerId);
        }

        return client;
    }

    [Fact]
    public async Task HealthLive_Returns200()
    {
        ResetFactory();
        var response = await _factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Chat_EmptyMessage_Returns400()
    {
        ResetFactory();
        var response = await _factory.CreateClient().SendAsync(ChatRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonDocument.Parse(body).RootElement.GetProperty("error").GetString();
        Assert.Contains("não pode ser vazia", error);
    }

    [Fact]
    public async Task Chat_SemEvidencia_RecusaSemChamarOModelo()
    {
        ResetFactory();

        var response = await _factory.CreateClient().SendAsync(ChatRequest("Qual o horário de atendimento?"));
        var events = await ReadEventsAsync(response);
        var types = events.Select(e => e.GetProperty("type").GetString()).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("refusal", types.First(t => t != "session"));
        Assert.Equal(ChatMessages.NoEvidenceRefusal,
            events.First(e => e.GetProperty("type").GetString() == "refusal").GetProperty("text").GetString());
        Assert.Equal("done", types[^1]);
        Assert.DoesNotContain(types, t => t == "delta");
    }

    [Fact]
    public async Task Chat_ComEvidencia_StreamaDeltasEFontes()
    {
        ResetFactory();
        var documentId = Guid.NewGuid();
        _factory.Hits = new[]
        {
            new SearchHit(
                documentId,
                "Horário de Atendimento",
                "1.0",
                "horario-atendimento.md#3",
                "O atendimento telefônico funciona de segunda a sexta das 8h às 20h.",
                0.92,
                DocumentPriority.Authoritative,
                DocumentCategory.Hours)
        };

        var response = await _factory.CreateClient().SendAsync(ChatRequest("Qual o horário de atendimento?"));
        var events = await ReadEventsAsync(response);
        var types = events.Select(e => e.GetProperty("type").GetString()).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("session", types);
        Assert.Contains("delta", types);
        Assert.Equal("done", types[^1]);
        Assert.Contains("sources", types);

        var sourcesEvent = events.First(e => e.GetProperty("type").GetString() == "sources");
        var sources = sourcesEvent.GetProperty("sources").EnumerateArray().ToList();
        Assert.Single(sources);
        Assert.Equal(documentId.ToString(), sources[0].GetProperty("documentId").GetString());

        var sessionEvent = events.First(e => e.GetProperty("type").GetString() == "session");
        var sessionId = sessionEvent.GetProperty("text").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var text = string.Concat(events
            .Where(e => e.GetProperty("type").GetString() == "delta")
            .Select(e => e.GetProperty("text").GetString()));
        Assert.Contains("Resposta de teste.", text);
    }

    [Fact]
    public async Task Chat_ConflitoEntreFontes_Recusa()
    {
        ResetFactory();
        _factory.Hits = new[]
        {
            new SearchHit(Guid.NewGuid(), "Doc A", "1.0", "a.md#1",
                "O prazo de troca por defeito é de 30 dias após o recebimento.",
                0.9, DocumentPriority.Authoritative, DocumentCategory.Policy),
            new SearchHit(Guid.NewGuid(), "Doc B", "1.0", "b.md#1",
                "O prazo de troca por defeito é de 60 dias após o recebimento.",
                0.9, DocumentPriority.Authoritative, DocumentCategory.Policy)
        };

        var response = await _factory.CreateClient().SendAsync(ChatRequest("Qual o prazo de troca?"));
        var events = await ReadEventsAsync(response);
        var types = events.Select(e => e.GetProperty("type").GetString()).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("refusal", types.First(t => t != "session"));
        Assert.Equal(ChatMessages.ConflictRefusal,
            events.First(e => e.GetProperty("type").GetString() == "refusal").GetProperty("text").GetString());
        Assert.Equal("done", types[^1]);
    }

    [Fact]
    public async Task Chat_EmbeddingIndisponivel_RetornaErroPadrao()
    {
        ResetFactory();
        _factory.FailEmbedding = true;
        var response = await _factory.CreateClient().SendAsync(ChatRequest("Qualquer pergunta aqui."));
        var events = await ReadEventsAsync(response);
        var types = events.Select(e => e.GetProperty("type").GetString()).ToList();

        Assert.Equal("error", types.First(t => t != "session"));
        Assert.Equal(ChatMessages.Unavailable,
            events.First(e => e.GetProperty("type").GetString() == "error").GetProperty("text").GetString());
        Assert.Equal("done", types[^1]);
    }

    [Fact]
    public async Task Admin_PostSemTokenAntiforgery_Retorna400()
    {
        ResetFactory();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/v1/admin/documents", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_AntiforgeryToken_Emitido()
    {
        ResetFactory();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/v1/admin/antiforgery");
        var body = await response.Content.ReadAsStringAsync();
        var token = JsonDocument.Parse(body).RootElement.GetProperty("token").GetString();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    private SearchHit[] EvidenceHits() => new[]
    {
        new SearchHit(Guid.NewGuid(), "Horário de Atendimento", "1.0", "horario-atendimento.md#3",
            "O atendimento telefônico funciona de segunda a sexta das 8h às 20h.",
            0.92, DocumentPriority.Authoritative, DocumentCategory.Hours)
    };

    private static async Task<string> RunChatAsync(
        HttpClient client, string message, string sessionId)
    {
        var response = await client.SendAsync(ChatRequest(message, sessionId));
        var events = await ReadEventsAsync(response);
        var text = string.Concat(events
            .Where(e => e.GetProperty("type").GetString() == "delta")
            .Select(e => e.GetProperty("text").GetString()));
        return text;
    }

    [Fact]
    public async Task Sessions_AposConversa_PersisteComTituloDaPergunta()
    {
        ResetFactory();
        _factory.Hits = EvidenceHits();
        var client = CreateClient(_factory, "visitor-a");
        var sessionId = Guid.NewGuid().ToString("N");
        var question = "Qual o horário de atendimento telefônico?";

        await RunChatAsync(client, question, sessionId);

        var sessions = await client.GetFromJsonAsync<List<JsonElement>>("api/v1/chat/sessions");
        var session = sessions!.Single(s => s.GetProperty("id").GetString() == sessionId);
        Assert.Equal(question, session.GetProperty("title").GetString());
        Assert.Equal(2, session.GetProperty("messageCount").GetInt32());
    }

    [Fact]
    public async Task Sessions_GetMessages_RetornaTurnoUserEAssistant()
    {
        ResetFactory();
        _factory.Hits = EvidenceHits();
        var client = CreateClient(_factory, "visitor-a");
        var sessionId = Guid.NewGuid().ToString("N");

        await RunChatAsync(client, "Qual o horário de atendimento?", sessionId);

        var messages = await client.GetFromJsonAsync<List<JsonElement>>(
            $"api/v1/chat/sessions/{sessionId}/messages");
        Assert.NotNull(messages);
        Assert.Equal(2, messages!.Count);
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Qual o horário de atendimento?", messages[0].GetProperty("content").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Contains("Resposta de teste.", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Sessions_Delete_RemoveSessaoEConteudo()
    {
        ResetFactory();
        _factory.Hits = EvidenceHits();
        var client = CreateClient(_factory, "visitor-a");
        var sessionId = Guid.NewGuid().ToString("N");

        await RunChatAsync(client, "Qual o horário de atendimento?", sessionId);

        var delete = await client.DeleteAsync($"api/v1/chat/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var messages = await client.GetAsync($"api/v1/chat/sessions/{sessionId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, messages.StatusCode);
    }

    [Fact]
    public async Task Sessions_MensagensDeSessaoInexistente_Retorna404()
    {
        ResetFactory();
        var client = CreateClient(_factory, "visitor-a");

        var response = await client.GetAsync($"api/v1/chat/sessions/{Guid.NewGuid():N}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_OutroProprietario_NaoListaNemLeNemDeleta()
    {
        ResetFactory();
        _factory.Hits = EvidenceHits();
        var ownerAId = $"visitor-a-{Guid.NewGuid():N}";
        var ownerBId = $"visitor-b-{Guid.NewGuid():N}";
        var ownerA = CreateClient(_factory, ownerAId);
        var ownerB = CreateClient(_factory, ownerBId);
        var sessionId = Guid.NewGuid().ToString("N");

        await RunChatAsync(ownerA, "Qual o horário de atendimento?", sessionId);

        // B não vê o histórico de A.
        var sessionsB = await ownerB.GetFromJsonAsync<List<JsonElement>>("api/v1/chat/sessions");
        Assert.Empty(sessionsB!);

        // B não consegue ler nem excluir o chat de A manipulando o sessionId.
        var readB = await ownerB.GetAsync($"api/v1/chat/sessions/{sessionId}/messages");
        Assert.Equal(HttpStatusCode.NotFound, readB.StatusCode);

        var deleteB = await ownerB.DeleteAsync($"api/v1/chat/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteB.StatusCode);

        // A continua com o chat intacto.
        var sessionsA = await ownerA.GetFromJsonAsync<List<JsonElement>>("api/v1/chat/sessions");
        Assert.Single(sessionsA!);
        Assert.Equal(sessionId, sessionsA![0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Sessions_ProprietarioSemChat_NaoVeChatsDeOutros()
    {
        ResetFactory();
        _factory.Hits = EvidenceHits();
        var ownerA = CreateClient(_factory, $"visitor-a-{Guid.NewGuid():N}");
        var ownerB = CreateClient(_factory, $"visitor-b-{Guid.NewGuid():N}");

        await RunChatAsync(ownerA, "Qual o horário de atendimento?", Guid.NewGuid().ToString("N"));

        // B nunca criou chat: recebe histórico vazio.
        var sessionsB = await ownerB.GetFromJsonAsync<List<JsonElement>>("api/v1/chat/sessions");
        Assert.Empty(sessionsB!);
    }
}

public sealed class AgentCancellationTests
{
    [Fact]
    public async Task AnswerAsync_CancelamentoDuranteStream_PropagaCancelamento()
    {
        var documentId = Guid.NewGuid();
        var hits = new[]
        {
            new SearchHit(documentId, "Titulo", "1.0", "doc.md#1",
                "Trecho único e suficiente para responder.",
                0.9, DocumentPriority.Standard, DocumentCategory.Faq)
        };

        var ollama = new SlowOllamaClient();
        var search = new StubSearch(hits);
        var options = Options.Create(new ChatOptions());
        var agent = new CompanyCopilot.Application.Agents.CompanyKnowledgeAgent(
            ollama,
            search,
            new ChatSessionStore(options),
            new StubChatStore(),
            new GenerationGate(),
            new EvidenceEvaluator(new VectorSearchOptions()),
            options,
            NullLogger<CompanyCopilot.Application.Agents.CompanyKnowledgeAgent>.Instance);

        using var cts = new CancellationTokenSource();

        var events = new List<ChatEvent>();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var chatEvent in agent.AnswerAsync(new ChatRequest(null, "Pergunta"), cts.Token))
            {
                events.Add(chatEvent);
                if (chatEvent.Type == ChatEventType.Delta)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.NotNull(exception);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Contains(events, e => e.Type == ChatEventType.Delta);
    }

    private sealed class SlowOllamaClient : IOllamaClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            var embedding = new float[768];
            return Task.FromResult(embedding);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            IReadOnlyList<ChatMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                yield return $"delta {i} ";
            }
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> HasModelAsync(string model, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubSearch : IKnowledgeSearch
    {
        private readonly IReadOnlyList<SearchHit> _hits;

        public StubSearch(IReadOnlyList<SearchHit> hits) => _hits = hits;

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            SearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(_hits);
    }

    private sealed class StubChatStore : IChatStore
    {
        private readonly List<(string User, string Assistant)> _turns = new();

        public Task<bool> ExistsForOwnerAsync(
            string sessionId, string ownerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_turns.Count > 0);

        public Task<bool> ExistsAsync(
            string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_turns.Count > 0);

        public Task AppendTurnAsync(
            string ownerId,
            string sessionId,
            string userMessage,
            string assistantMessage,
            CancellationToken cancellationToken = default)
        {
            _turns.Add((userMessage, assistantMessage));
            return Task.CompletedTask;
        }

        public Task<List<ChatSessionSummary>> GetRecentSessionsAsync(
            string ownerId, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ChatSessionSummary>());

        public Task<List<ChatMessageRecord>> GetMessagesAsync(
            string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ChatMessageRecord>());

        public Task DeleteAsync(string sessionId, string ownerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
