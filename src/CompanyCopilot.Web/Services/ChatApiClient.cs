using System.Text.Json;

namespace CompanyCopilot.Web.Services;

public sealed record ChatStreamEvent(
    string Type,
    string? SessionId,
    string? Text,
    IReadOnlyList<ChatSourceDto>? Sources,
    string? ErrorMessage);

public sealed record ChatSourceDto(
    Guid DocumentId,
    string Title,
    string Version,
    string Location,
    string Excerpt);

public sealed record ChatRequestDto(string? SessionId, string Message);

public sealed record FeedbackRequestDto(string? SessionId, string Kind, string? Comment);

public sealed record ChatSessionSummaryDto(string Id, string Title, DateTimeOffset UpdatedAtUtc, int MessageCount);

public sealed record ChatMessageRecordDto(string Role, string Content);

/// <summary>
/// Cliente do chat público. A comunicação é sempre servidor-a-servidor:
/// o navegador nunca conhece a URL da API, do PostgreSQL ou do Ollama.
/// </summary>
public sealed class ChatApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;
    private readonly HttpClient _http;
    private readonly ILogger<ChatApiClient> _logger;

    public ChatApiClient(HttpClient http, VisitorIdProvider visitorIdProvider, ILogger<ChatApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add(VisitorIdProvider.HeaderName, visitorIdProvider.VisitorId);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequestDto request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/v1/chat/messages", request, cancellationToken);

        if ((int)response.StatusCode == 429)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = JsonSerializer.Deserialize<ErrorEnvelope>(body, JsonOptions);
            yield return new ChatStreamEvent("error", null, error?.Error ?? "Limite de perguntas atingido.", null, null);
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = JsonSerializer.Deserialize<ErrorEnvelope>(body, JsonOptions);
            yield return new ChatStreamEvent("error", null, error?.Error ?? "Falha na comunicação com o assistente.", null, null);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chatEvent = TryParseEvent(line);
            if (chatEvent is not null)
            {
                yield return chatEvent;
            }
        }
    }

    private static ChatStreamEvent? TryParseEvent(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<ChatStreamEvent>(line, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<bool> SubmitFeedbackAsync(
        FeedbackRequestDto feedback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/v1/chat/feedback", feedback, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar feedback");
            return false;
        }
    }

    public async Task<List<ChatSessionSummaryDto>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("api/v1/chat/sessions", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ChatSessionSummaryDto>();
            }

            return await response.Content.ReadFromJsonAsync<List<ChatSessionSummaryDto>>(
                JsonOptions, cancellationToken) ?? new List<ChatSessionSummaryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar sessões de chat");
            return new List<ChatSessionSummaryDto>();
        }
    }

    public async Task<List<ChatMessageRecordDto>?> GetSessionMessagesAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(
                $"api/v1/chat/sessions/{sessionId}/messages", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<ChatMessageRecordDto>>(
                JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar mensagens da sessão {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<bool> DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.DeleteAsync(
                $"api/v1/chat/sessions/{sessionId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao excluir sessão {SessionId}", sessionId);
            return false;
        }
    }

    private sealed record ErrorEnvelope(string? Error);
}
