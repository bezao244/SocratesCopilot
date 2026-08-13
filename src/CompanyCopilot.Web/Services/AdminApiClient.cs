using System.Net.Http.Json;
using System.Text.Json;

namespace CompanyCopilot.Web.Services;

public sealed record ApiResult(bool Ok, string? Error, JsonElement? Payload = null)
{
    public bool TryGetPayloadProperty(string property, out JsonElement value)
    {
        value = default;
        if (Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return payload.TryGetProperty(property, out value);
    }
}

/// <summary>
/// Cliente da área administrativa local com validação antiforgery:
/// obtém um token via GET /antiforgery (cookie + token) e envia o token
/// no cabeçalho X-CSRF-TOKEN em todas as operações administrativas.
/// </summary>
public sealed class AdminApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AdminApiClient> _logger;
    private string? _requestToken;

    public AdminApiClient(HttpClient http, ILogger<AdminApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-CSRF-TOKEN", _requestToken);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} retornou {Status}", url, response.StatusCode);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no GET {Url}", url);
            return default;
        }
    }

    public async Task<ApiResult> SendAsync(
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-CSRF-TOKEN", _requestToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await ExecuteAsync(request, cancellationToken);
    }

    public async Task<ApiResult> UploadAsync(
        string url,
        string fileName,
        Stream fileStream,
        string? title,
        CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(cancellationToken);

        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "file", fileName);
        if (!string.IsNullOrWhiteSpace(title))
        {
            content.Add(new StringContent(title), "title");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("X-CSRF-TOKEN", _requestToken);

        return await ExecuteAsync(request, cancellationToken);
    }

    private async Task<ApiResult> ExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            JsonElement? payload = null;
            if (response.Content.Headers.ContentLength is not 0 && response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                try
                {
                    payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                }
                catch (JsonException)
                {
                    payload = null;
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return new ApiResult(true, null, payload);
            }

            var error = ExtractError(payload);
            return new ApiResult(false, error ?? $"Falha na operação ({response.StatusCode}).", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na chamada administrativa {Method} {Url}", request.Method, request.RequestUri);
            return new ApiResult(false, "Não foi possível comunicar com a API interna.");
        }
    }

    private static string? ExtractError(JsonElement? payload)
    {
        if (payload is { } element
            && element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String)
        {
            return error.GetString();
        }

        return null;
    }

    private async Task EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_requestToken))
        {
            return;
        }

        using var response = await _http.GetAsync("api/v1/admin/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<TokenEnvelope>(cancellationToken);
        _requestToken = envelope?.Token;
    }

    private sealed record TokenEnvelope(string? Token);
}
