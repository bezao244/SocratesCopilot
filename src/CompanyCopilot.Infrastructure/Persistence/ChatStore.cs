using CompanyCopilot.Application.Abstractions;
using CompanyCopilot.Application.Models;
using CompanyCopilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompanyCopilot.Infrastructure.Persistence;

public sealed class ChatStore : IChatStore
{
    private const int TitleMaxLength = 200;

    private readonly CopilotDbContext _db;

    public ChatStore(CopilotDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsForOwnerAsync(
        string sessionId,
        string ownerId,
        CancellationToken cancellationToken = default) =>
        _db.ChatSessions
            .AsNoTracking()
            .AnyAsync(
                s => s.SessionId == sessionId && s.OwnerId == ownerId,
                cancellationToken);

    public Task<bool> ExistsAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        _db.ChatSessions
            .AsNoTracking()
            .AnyAsync(s => s.SessionId == sessionId, cancellationToken);

    public async Task AppendTurnAsync(
        string ownerId,
        string sessionId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var session = await _db.ChatSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            session = new ChatSession
            {
                SessionId = sessionId,
                OwnerId = ownerId,
                Title = Truncate(userMessage, TitleMaxLength)
            };
            _db.ChatSessions.Add(session);
        }

        session.UpdatedAtUtc = now;

        var ordinal = (await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Ordinal)
            .Select(m => (int?)m.Ordinal)
            .FirstOrDefaultAsync(cancellationToken)) ?? 0;

        _db.ChatMessages.Add(new StoredChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = userMessage,
            Ordinal = ordinal + 1,
            CreatedAtUtc = now
        });

        _db.ChatMessages.Add(new StoredChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = assistantMessage,
            Ordinal = ordinal + 2,
            CreatedAtUtc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ChatSessionSummary>> GetRecentSessionsAsync(
        string ownerId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Task.FromResult(new List<ChatSessionSummary>());
        }

        return _db.ChatSessions
            .AsNoTracking()
            .Where(s => s.OwnerId == ownerId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Take(take)
            .Select(s => new ChatSessionSummary(
                s.SessionId,
                s.Title,
                s.UpdatedAtUtc,
                _db.ChatMessages.Count(m => m.SessionId == s.SessionId)))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ChatMessageRecord>> GetMessagesAsync(
        string sessionId,
        CancellationToken cancellationToken = default) =>
        _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Ordinal)
            .Select(m => new ChatMessageRecord(m.Role, m.Content))
            .ToListAsync(cancellationToken);

    public async Task DeleteAsync(string sessionId, string ownerId, CancellationToken cancellationToken = default)
    {
        var session = await _db.ChatSessions
            .FirstOrDefaultAsync(
                s => s.SessionId == sessionId && s.OwnerId == ownerId,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
