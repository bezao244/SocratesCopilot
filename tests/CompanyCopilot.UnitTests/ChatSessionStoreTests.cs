using CompanyCopilot.Application.Chat;
using CompanyCopilot.Application.Configuration;
using Microsoft.Extensions.Options;

namespace CompanyCopilot.UnitTests;

public class ChatSessionStoreTests
{
    private const string OwnerA = "visitor-a";
    private const string OwnerB = "visitor-b";

    private static ChatSessionStore CreateStore(int idleMinutes = 20) =>
        new(Options.Create(new ChatOptions { SessionIdleMinutes = idleMinutes }));

    [Fact]
    public void CreateSession_ReturnsUniqueIds()
    {
        var store = CreateStore();

        var a = store.CreateSession(OwnerA);
        var b = store.CreateSession(OwnerA);

        Assert.NotEqual(a, b);
        Assert.True(store.Exists(OwnerA, a));
        Assert.True(store.Exists(OwnerA, b));
    }

    [Fact]
    public void GetLastTurns_SessaoDesconhecida_ReturnsEmpty()
    {
        var store = CreateStore();
        Assert.Empty(store.GetLastTurns(OwnerA, "inexistente", 6));
    }

    [Fact]
    public void GetLastTurns_RespeitaMaximoDeTurnos()
    {
        var store = CreateStore();
        var sessionId = store.CreateSession(OwnerA);

        for (var i = 0; i < 10; i++)
        {
            store.AddTurn(OwnerA, sessionId, $"pergunta {i}", $"resposta {i}");
        }

        var turns = store.GetLastTurns(OwnerA, sessionId, 6);

        Assert.Equal(6, turns.Count);
        Assert.Equal("pergunta 4", turns[0].User);
        Assert.Equal("pergunta 9", turns[^1].User);
    }

    [Fact]
    public void AddTurn_SessaoDesconhecida_NoOp()
    {
        var store = CreateStore();
        store.AddTurn(OwnerA, "inexistente", "pergunta", "resposta");

        Assert.Empty(store.GetLastTurns(OwnerA, "inexistente", 6));
    }

    [Fact]
    public void SessaoDeOutroDono_NaoEhAcessivel()
    {
        var store = CreateStore();
        var sessionId = store.CreateSession(OwnerA);
        store.AddTurn(OwnerA, sessionId, "pergunta", "resposta");

        Assert.False(store.Exists(OwnerB, sessionId));
        Assert.Empty(store.GetLastTurns(OwnerB, sessionId, 6));

        store.AddTurn(OwnerB, sessionId, "intrusão", "ignorada");
        Assert.Empty(store.GetLastTurns(OwnerB, sessionId, 6));
        Assert.Single(store.GetLastTurns(OwnerA, sessionId, 6));
    }

    [Fact]
    public void RemoveExpiredSessions_RemoveSessaoInativa()
    {
        var store = CreateStore();
        var sessionId = store.CreateSession(OwnerA);

        store.RemoveExpiredSessions(
            TimeSpan.FromMinutes(20),
            DateTimeOffset.UtcNow.AddMinutes(21));

        Assert.False(store.Exists(OwnerA, sessionId));
    }

    [Fact]
    public void RemoveExpiredSessions_SessaoAtiva_Permanece()
    {
        var store = CreateStore();
        var sessionId = store.CreateSession(OwnerA);
        store.Touch(OwnerA, sessionId);

        store.RemoveExpiredSessions(
            TimeSpan.FromMinutes(20),
            DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True(store.Exists(OwnerA, sessionId));
    }
}
