using CompanyCopilot.Application.Models;

namespace CompanyCopilot.Application.Abstractions;

/// <summary>
/// Cliente do Ollama para geração de chat e embeddings.
/// Usa http://127.0.0.1:11434/api/chat e /api/embed (nunca exposto publicamente).
/// </summary>
public interface IOllamaClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<bool> HasModelAsync(string model, CancellationToken cancellationToken = default);
}
