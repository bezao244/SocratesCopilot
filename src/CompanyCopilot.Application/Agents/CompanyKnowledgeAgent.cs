using System.Text;
using System.Threading.Channels;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Agents;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Evidence;
using CompanyCopilot.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Application.Agents;

/// <summary>
/// Orquestração RAG conforme o plano:
/// 1. validar entrada; 2. histórico limitado; 3. embedding; 4. busca híbrida;
/// 5. evidência e conflitos; 6. prompt restrito; 7. streaming PT-BR com fontes;
/// 8. normalização e erros. Determinístico: nenhuma ferramenta externa.
/// </summary>
public sealed class CompanyKnowledgeAgent : ICompanyKnowledgeAgent
{
    private readonly IOllamaClient _ollama;
    private readonly IKnowledgeSearch _search;
    private readonly IChatSessionStore _sessions;
    private readonly IChatStore _chatStore;
    private readonly IGenerationGate _generationGate;
    private readonly EvidenceEvaluator _evidenceEvaluator;
    private readonly ChatOptions _options;
    private readonly ILogger<CompanyKnowledgeAgent> _logger;

    public CompanyKnowledgeAgent(
        IOllamaClient ollama,
        IKnowledgeSearch search,
        IChatSessionStore sessions,
        IChatStore chatStore,
        IGenerationGate generationGate,
        EvidenceEvaluator evidenceEvaluator,
        IOptions<ChatOptions> options,
        ILogger<CompanyKnowledgeAgent> logger)
    {
        _ollama = ollama;
        _search = search;
        _sessions = sessions;
        _chatStore = chatStore;
        _generationGate = generationGate;
        _evidenceEvaluator = evidenceEvaluator;
        _options = options.Value;
        _logger = logger;
    }

    public IAsyncEnumerable<ChatEvent> AnswerAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<ChatEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _ = ProduceAsync(request, channel.Writer, cancellationToken);
        return ReadAllAsync(channel, cancellationToken);
    }

    private static async IAsyncEnumerable<ChatEvent> ReadAllAsync(
        Channel<ChatEvent> channel,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chatEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chatEvent;
        }
    }

    private async Task ProduceAsync(
        ChatRequest request,
        ChannelWriter<ChatEvent> writer,
        CancellationToken cancellationToken)
    {
        var sessionId = await ResolveSessionAsync(request.SessionId, request.OwnerId, cancellationToken);

        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                await writer.WriteAsync(ErrorEvent(sessionId, "A pergunta não pode ser vazia."), cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            if (request.Message.Length > _options.MaxQuestionCharacters)
            {
                await writer.WriteAsync(ErrorEvent(sessionId, ChatMessages.MessageTooLong), cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            if (SessionWasCreated)
            {
                await writer.WriteAsync(
                    new ChatEvent(ChatEventType.Session, sessionId, sessionId), cancellationToken);
            }

            using var gate = _generationGate.EnterGeneration();

            float[] embedding;
            try
            {
                embedding = await _ollama.EmbedAsync(request.Message, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding indisponível para a pergunta");
                await writer.WriteAsync(ErrorEvent(sessionId, ChatMessages.Unavailable), cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            IReadOnlyList<SearchHit> hits;
            try
            {
                hits = await _search.SearchAsync(
                    new SearchRequest(embedding, request.Message, DateOnly.FromDateTime(DateTime.Now)),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na busca híbrida");
                await writer.WriteAsync(ErrorEvent(sessionId, ChatMessages.Unavailable), cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            var evidence = _evidenceEvaluator.Evaluate(hits, _options.MaxRetrievedChunks);

            if (evidence.Verdict == EvidenceVerdict.NoEvidence)
            {
                await writer.WriteAsync(new ChatEvent(
                    ChatEventType.Refusal, sessionId, ChatMessages.NoEvidenceRefusal, IsFinal: true), cancellationToken);
                await SaveTurnAsync(request.OwnerId, sessionId, request.Message, ChatMessages.NoEvidenceRefusal, cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            if (evidence.Verdict == EvidenceVerdict.Conflict)
            {
                await writer.WriteAsync(new ChatEvent(
                    ChatEventType.Refusal, sessionId, ChatMessages.ConflictRefusal, IsFinal: true), cancellationToken);
                await SaveTurnAsync(request.OwnerId, sessionId, request.Message, ChatMessages.ConflictRefusal, cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            var history = _sessions.GetLastTurns(request.OwnerId, sessionId, _options.MaxHistoryTurns);
            var prompt = PromptBuilder.BuildUserPrompt(request.Message, history, evidence.SelectedHits);
            var messages = new[] { new ChatMessage("user", prompt) };

            var fullAnswer = new StringBuilder();

            try
            {
                await foreach (var delta in _ollama.ChatStreamAsync(messages, cancellationToken))
                {
                    fullAnswer.Append(delta);
                    await writer.WriteAsync(new ChatEvent(ChatEventType.Delta, sessionId, delta), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Geração cancelada pelo usuário para a sessão {SessionId}", sessionId);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na geração de resposta para a sessão {SessionId}", sessionId);
                await writer.WriteAsync(ErrorEvent(sessionId, ChatMessages.Unavailable), cancellationToken);
                await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
                return;
            }

            var sourceHits = SourceCitationSelector.SelectRelevant(
                request.Message, evidence.SelectedHits, _options.MaxRetrievedChunks);

            if (sourceHits.Count > 0)
            {
                var sources = sourceHits
                    .Select(h => new ChatSourceDto(
                        h.DocumentId,
                        h.Title,
                        h.Version,
                        h.Location,
                        h.Excerpt.Length > 240 ? h.Excerpt[..240] + "…" : h.Excerpt))
                    .ToList();

                await writer.WriteAsync(
                    new ChatEvent(ChatEventType.Sources, sessionId, Sources: sources),
                    cancellationToken);
            }

            if (fullAnswer.Length > 0)
            {
                await SaveTurnAsync(request.OwnerId, sessionId, request.Message, fullAnswer.ToString(), cancellationToken);
            }

            await writer.WriteAsync(DoneEvent(sessionId), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sessão {SessionId} encerrada pelo cliente", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no agente para a sessão {SessionId}", sessionId);
            try
            {
                await writer.WriteAsync(ErrorEvent(sessionId, ChatMessages.Unavailable), CancellationToken.None);
                await writer.WriteAsync(DoneEvent(sessionId), CancellationToken.None);
            }
            catch (ChannelClosedException)
            {
                // cliente já encerrou o stream
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<string> ResolveSessionAsync(
        string? requestedSessionId,
        string? ownerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            // Sem dono identificável: nenhuma sessão persistida é adotada.
            SessionWasCreated = true;
            return _sessions.CreateSession(ownerId ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(requestedSessionId))
        {
            var belongsToOwner = false;
            try
            {
                belongsToOwner = await _chatStore.ExistsForOwnerAsync(
                    requestedSessionId, ownerId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao verificar posse da sessão {SessionId}", requestedSessionId);
            }

            if (belongsToOwner)
            {
                if (_sessions.Exists(ownerId, requestedSessionId))
                {
                    _sessions.Touch(ownerId, requestedSessionId);
                    return requestedSessionId;
                }

                try
                {
                    var stored = await _chatStore.GetMessagesAsync(requestedSessionId, cancellationToken);
                    _sessions.Adopt(ownerId, requestedSessionId, ToTurns(stored));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao carregar sessão persistida {SessionId}", requestedSessionId);
                    _sessions.Adopt(ownerId, requestedSessionId, Array.Empty<(string, string)>());
                }

                return requestedSessionId;
            }

            // Sessão em memória deste processo ainda não persistida (sem turno salvo
            // no banco): pertence ao dono atual; mantém o contexto da conversa.
            if (_sessions.Exists(ownerId, requestedSessionId))
            {
                _sessions.Touch(ownerId, requestedSessionId);
                return requestedSessionId;
            }

            var existsElsewhere = false;
            try
            {
                existsElsewhere = await _chatStore.ExistsAsync(requestedSessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao verificar existência da sessão {SessionId}", requestedSessionId);
            }

            if (existsElsewhere)
            {
                _logger.LogWarning(
                    "Tentativa de acesso a sessão alheia {SessionId} pelo dono {OwnerId}",
                    requestedSessionId, ownerId);
                SessionWasCreated = true;
                return _sessions.CreateSession(ownerId);
            }

            // Sessão nova: o cliente escolheu o id; adota como conversa do dono.
            _sessions.Adopt(ownerId, requestedSessionId, Array.Empty<(string, string)>());
            return requestedSessionId;
        }

        SessionWasCreated = true;
        return _sessions.CreateSession(ownerId);
    }

    private async Task SaveTurnAsync(
        string ownerId,
        string sessionId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken)
    {
        _sessions.AddTurn(ownerId, sessionId, userMessage, assistantMessage);

        try
        {
            await _chatStore.AppendTurnAsync(ownerId, sessionId, userMessage, assistantMessage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir turno da sessão {SessionId}", sessionId);
        }
    }

    private static (string User, string Assistant)[] ToTurns(IReadOnlyList<ChatMessageRecord> messages)
    {
        var turns = new List<(string, string)>();
        string? pendingUser = null;

        foreach (var message in messages)
        {
            if (message.Role == "user")
            {
                pendingUser = message.Content;
            }
            else if (message.Role == "assistant" && pendingUser is not null)
            {
                turns.Add((pendingUser, message.Content));
                pendingUser = null;
            }
        }

        return turns.ToArray();
    }

    private bool SessionWasCreated { get; set; }

    private static ChatEvent ErrorEvent(string sessionId, string message) =>
        new(ChatEventType.Error, sessionId, message);

    private static ChatEvent DoneEvent(string sessionId) =>
        new(ChatEventType.Done, sessionId, IsFinal: true);
}
