using System.Collections.Concurrent;
using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.Application.RateLimiting;

/// <summary>
/// Janela fixa por IP: oito perguntas a cada dez minutos.
/// Uma geração concorrente e fila máxima de cinco; acima disso, rejeita (429).
/// </summary>
public sealed class RateLimitService : IRateLimitService
{
    private sealed record Window(DateTimeOffset StartedAtUtc, int Count);

    private readonly ChatOptions _options;
    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private readonly SemaphoreSlim _generationSlot = new(1, 1);
    private int _queued;

    public RateLimitService(IOptions<ChatOptions> options)
    {
        _options = options.Value;
    }

    public bool TryConsume(string clientIp)
    {
        var now = DateTimeOffset.UtcNow;
        var windowLength = TimeSpan.FromMinutes(_options.RateLimitWindowMinutes);

        while (true)
        {
            var current = _windows.GetOrAdd(clientIp, _ => new Window(now, 0));

            if (now - current.StartedAtUtc > windowLength)
            {
                var fresh = new Window(now, 0);
                if (_windows.TryUpdate(clientIp, fresh, current))
                {
                    current = fresh;
                }

                continue;
            }

            if (current.Count >= _options.MaxQuestionsPerWindow)
            {
                return false;
            }

            if (_windows.TryUpdate(clientIp, new Window(current.StartedAtUtc, current.Count + 1), current))
            {
                return true;
            }
        }
    }

    public bool TryEnqueue()
    {
        if (_generationSlot.Wait(0))
        {
            return true;
        }

        var queued = Interlocked.Increment(ref _queued);
        if (queued > _options.MaxQueuedGenerations)
        {
            Interlocked.Decrement(ref _queued);
            return false;
        }

        return true;
    }

    public void Release()
    {
        var queued = Interlocked.Decrement(ref _queued);
        _generationSlot.Release();

        if (queued < 0)
        {
            Interlocked.Exchange(ref _queued, 0);
        }
    }
}
