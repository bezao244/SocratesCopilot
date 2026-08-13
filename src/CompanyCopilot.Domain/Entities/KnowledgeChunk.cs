using NpgsqlTypes;
using Pgvector;

namespace CompanyCopilot.Domain.Entities;

public class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;
    public int Ordinal { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
    public NpgsqlTsVector? TextVector { get; set; }
    public DateTimeOffset IndexedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
