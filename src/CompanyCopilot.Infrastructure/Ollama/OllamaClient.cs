using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Infrastructure.Ollama;

/// <summary>
/// Cliente HTTP do Ollama local (127.0.0.1:11434):
/// geração em /api/chat (SSE) e embeddings em /api/embed.
/// Mensagens de erro não vazam detalhes internos.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    private const string ContentTypeJson = "application/json";
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient http, IOptions<OllamaOptions> options, ILogger<OllamaClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = _options.Timeout;
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ContentTypeJson));
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = _options.EmbeddingModel,
            input = new[] { text }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/embed")
        {
            Content = new StringContent(payload, Encoding.UTF8, ContentTypeJson)
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Embedding falhou com status {Status}", response.StatusCode);
            throw new InvalidOperationException("Serviço de embeddings indisponível.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<EmbedResponse>(stream, JsonOptions, cancellationToken);

        if (result?.Embeddings is null || result.Embeddings.Length == 0)
        {
            throw new InvalidOperationException("Resposta de embeddings vazia.");
        }

        return result.Embeddings[0];
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = _options.ChatModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            think = false,
            options = new
            {
                temperature = 0.1,
                num_predict = 512,
                num_ctx = _options.ContextLength
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = new StringContent(payload, Encoding.UTF8, ContentTypeJson)
        };

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Chat falhou com status {Status}", response.StatusCode);
            throw new InvalidOperationException("Serviço de geração indisponível.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var data = line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? line[5..].Trim()
                : line.Trim();

            if (string.IsNullOrEmpty(data))
            {
                continue;
            }

            ChatStreamFrame? frame;
            try
            {
                frame = JsonSerializer.Deserialize<ChatStreamFrame>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (frame?.Message?.Content is { Length: > 0 } content)
            {
                yield return content;
            }

            if (frame?.Done == true)
            {
                yield break;
            }
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await _http.GetAsync("api/version", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> HasModelAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await _http.GetAsync("api/tags", cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var tags = await JsonSerializer.DeserializeAsync<TagsResponse>(stream, JsonOptions, cancellationToken: cts.Token);
            return tags?.Models?.Any(m => m.Name == model || m.Name == $"{model}:latest") ?? false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed class EmbedResponse
    {
        public float[][]? Embeddings { get; set; }
    }

    private sealed class ChatStreamFrame
    {
        public ChatMessageFrame? Message { get; set; }
        public bool Done { get; set; }
    }

    private sealed class ChatMessageFrame
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    private sealed class TagsResponse
    {
        public List<ModelTag>? Models { get; set; }
    }

    private sealed class ModelTag
    {
        public string Name { get; set; } = string.Empty;
    }
}
