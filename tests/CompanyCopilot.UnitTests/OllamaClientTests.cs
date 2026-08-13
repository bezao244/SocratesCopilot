using System.Net;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Infrastructure.Ollama;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.UnitTests;

/// <summary>
/// O servidor Ollama responde com chaves em minúsculas (embeddings, message, done).
/// A desserialização deve ser case-insensitive — regressão que deixaria
/// embedding e chat mudos sem aviso.
/// </summary>
public class OllamaClientTests
{
    private static IOllamaClient CreateClient(HttpMessageHandler handler) =>
        new OllamaClient(
            new HttpClient(handler),
            Options.Create(new OllamaOptions { BaseUrl = "http://127.0.0.1:11434" }),
            NullLogger<OllamaClient>.Instance);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task EmbedAsync_RespostaMinuscula_RetornaVetor()
    {
        var embeddings = Enumerable.Range(0, 4).Select(i => (float)i).ToArray();
        var client = CreateClient(new StubHandler(_ =>
            JsonResponse($"{{\"embeddings\":[[{string.Join(",", embeddings)}]]}}")));

        var result = await client.EmbedAsync("texto");

        Assert.Equal(embeddings, result);
    }

    [Fact]
    public async Task EmbedAsync_EnviaInputComoArray()
    {
        float[]? received = null;
        var client = CreateClient(new StubHandler(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var input = doc.RootElement.GetProperty("input");
            received = input.EnumerateArray().Select(e => (float)(e.GetString()?.Length ?? 0)).ToArray();
            return JsonResponse("{\"embeddings\":[[1,2,3]]}");
        }));

        await client.EmbedAsync("texto");

        Assert.Single(received!);
    }

    [Fact]
    public async Task ChatStreamAsync_RespostaMinuscula_FluxaDeltas()
    {
        var client = CreateClient(new StubHandler(_ =>
        {
            var frames = "{\"message\":{\"role\":\"assistant\",\"content\":\"ola\"},\"done\":false}\n" +
                         "{\"message\":{\"role\":\"assistant\",\"content\":\" mundo\"},\"done\":false}\n" +
                         "{\"done\":true}\n";
            return new HttpResponseMessage
            {
                Content = new StringContent(frames, System.Text.Encoding.UTF8, "application/x-ndjson")
            };
        }));

        var deltas = new List<string>();
        await foreach (var delta in client.ChatStreamAsync(
                           new[] { new ChatMessage("user", "oi") }))
        {
            deltas.Add(delta);
        }

        Assert.Equal(new[] { "ola", " mundo" }, deltas);
    }

    [Fact]
    public async Task ChatStreamAsync_AceitaLinhasComPrefixoData()
    {
        var client = CreateClient(new StubHandler(_ =>
        {
            var frames = "data: {\"message\":{\"role\":\"assistant\",\"content\":\"SSE\"},\"done\":false}\n" +
                         "{\"message\":{\"role\":\"assistant\",\"content\":\" + puro\"},\"done\":false}\n" +
                         "data: {\"done\":true}\n";
            return new HttpResponseMessage
            {
                Content = new StringContent(frames, System.Text.Encoding.UTF8, "application/x-ndjson")
            };
        }));

        var deltas = new List<string>();
        await foreach (var delta in client.ChatStreamAsync(
                           new[] { new ChatMessage("user", "oi") }))
        {
            deltas.Add(delta);
        }

        Assert.Equal(new[] { "SSE", " + puro" }, deltas);
    }

    [Fact]
    public async Task ChatStreamAsync_ErroDoServidor_Lanca()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            "{\"error\":\"model not found\"}", HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.ChatStreamAsync(new[] { new ChatMessage("user", "oi") }))
            {
            }
        });
    }

    [Fact]
    public async Task HasModelAsync_NomesMinusculos_RetornaTrue()
    {
        var client = CreateClient(new StubHandler(_ =>
            JsonResponse("{\"models\":[{\"name\":\"empresa-copiloto:v1\"}]}")));

        Assert.True(await client.HasModelAsync("empresa-copiloto:v1"));
        Assert.False(await client.HasModelAsync("empresa-copiloto"));
    }
}
