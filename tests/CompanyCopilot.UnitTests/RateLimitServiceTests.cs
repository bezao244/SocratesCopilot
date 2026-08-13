using CompanyCopilot.Application.Configuration;
using CompanyCopilot.Application.RateLimiting;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.UnitTests;

public class RateLimitServiceTests
{
    private static ChatOptions DefaultOptions => new()
    {
        MaxQuestionsPerWindow = 8,
        RateLimitWindowMinutes = 10,
        MaxQueuedGenerations = 5
    };

    [Fact]
    public void TryConsume_RespeitaLimiteDaJanela()
    {
        var service = new RateLimitService(Options.Create(DefaultOptions));

        for (var i = 0; i < 8; i++)
        {
            Assert.True(service.TryConsume("10.0.0.1"));
        }

        Assert.False(service.TryConsume("10.0.0.1"));
    }

    [Fact]
    public void TryConsume_IpsDiferentes_TemJanelasIndependentes()
    {
        var service = new RateLimitService(Options.Create(DefaultOptions));

        for (var i = 0; i < 8; i++)
        {
            service.TryConsume("10.0.0.1");
        }

        Assert.True(service.TryConsume("10.0.0.2"));
    }

    [Fact]
    public void TryEnqueue_UmaPorVez_SempreTrue()
    {
        var service = new RateLimitService(Options.Create(DefaultOptions));

        Assert.True(service.TryEnqueue());
        service.Release();
        Assert.True(service.TryEnqueue());
        service.Release();
    }

    [Fact]
    public void TryEnqueue_ComSlotOcupado_AceitaFila()
    {
        var service = new RateLimitService(Options.Create(DefaultOptions));

        Assert.True(service.TryEnqueue());
        for (var i = 0; i < 5; i++)
        {
            Assert.True(service.TryEnqueue());
        }
    }

    [Fact]
    public void TryEnqueue_FilaCheia_Rejeita()
    {
        var service = new RateLimitService(Options.Create(DefaultOptions));

        Assert.True(service.TryEnqueue());
        for (var i = 0; i < 5; i++)
        {
            service.TryEnqueue();
        }

        Assert.False(service.TryEnqueue());
    }
}
