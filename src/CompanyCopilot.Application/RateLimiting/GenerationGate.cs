using CompanyCopilot.Application.Abstractions;

namespace CompanyCopilot.Application.RateLimiting;

/// <summary>
/// Controle de prioridade de VRAM: embeddings aguardam enquanto o chat estiver gerando.
/// </summary>
public sealed class GenerationGate : IGenerationGate
{
    private readonly object _lock = new();
    private int _activeGenerations;

    public IDisposable EnterGeneration()
    {
        lock (_lock)
        {
            _activeGenerations++;
        }

        return new Disposable(() =>
        {
            lock (_lock)
            {
                _activeGenerations--;
            }
        });
    }

    public Task WaitForEmbeddingSlotAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lock)
            {
                if (_activeGenerations == 0)
                {
                    return Task.CompletedTask;
                }
            }

            Thread.Sleep(250);
        }
    }

    private sealed class Disposable : IDisposable
    {
        private Action? _action;

        public Disposable(Action action) => _action = action;

        public void Dispose()
        {
            Interlocked.Exchange(ref _action, null)?.Invoke();
        }
    }
}
