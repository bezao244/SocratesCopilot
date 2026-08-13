using CompanyCopilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace CompanyCopilot.Infrastructure.Persistence;

public sealed class CopilotDbContext : DbContext
{
    public CopilotDbContext(DbContextOptions<CopilotDbContext> options)
        : base(options)
    {
    }

    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<EvaluationCase> EvaluationCases => Set<EvaluationCase>();
    public DbSet<UserFeedback> UserFeedbacks => Set<UserFeedback>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<StoredChatMessage> ChatMessages => Set<StoredChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeDocument>(builder =>
        {
            builder.ToTable("knowledge_documents");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Title).HasMaxLength(500).IsRequired();
            builder.Property(d => d.OriginalFileName).HasMaxLength(500).IsRequired();
            builder.Property(d => d.StoredFileName).HasMaxLength(200).IsRequired();
            builder.Property(d => d.FileExtension).HasMaxLength(16).IsRequired();
            builder.Property(d => d.ContentHashSha256).HasMaxLength(64).IsRequired();
            builder.Property(d => d.Version).HasMaxLength(32).IsRequired();
            builder.Property(d => d.Source).HasMaxLength(500);
            builder.Property(d => d.Responsible).HasMaxLength(200);
            builder.Property(d => d.FailureMessage).HasMaxLength(2000);

            builder.HasIndex(d => d.Status);
            builder.HasIndex(d => d.Visibility);
            builder.HasIndex(d => d.Category);
            builder.HasIndex(d => new { d.ValidFrom, d.ValidUntil });
            builder.HasIndex(d => d.ContentHashSha256).IsUnique();

            builder.HasMany(d => d.Chunks)
                .WithOne(c => c.Document)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeChunk>(builder =>
        {
            builder.ToTable("knowledge_chunks");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Content).HasColumnType("text").IsRequired();
            builder.Property(c => c.Location).HasMaxLength(300).IsRequired();
            builder.Property(c => c.Embedding).HasColumnType("vector(768)");
            builder.Property(c => c.TextVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql("to_tsvector('portuguese', \"Content\")", stored: true);

            builder.HasIndex(c => new { c.DocumentId, c.Ordinal });
            builder.HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);
            builder.HasIndex(c => c.TextVector).HasMethod("gin");
        });

        modelBuilder.Entity<IngestionJob>(builder =>
        {
            builder.ToTable("ingestion_jobs");
            builder.HasKey(j => j.Id);
            builder.Property(j => j.Messages).HasColumnType("jsonb");
            builder.HasIndex(j => j.Status);
            builder.HasIndex(j => j.CreatedAtUtc);

            builder.HasOne(j => j.Document)
                .WithMany(d => d.IngestionJobs)
                .HasForeignKey(j => j.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEvent>(builder =>
        {
            builder.ToTable("audit_events");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Actor).HasMaxLength(200).IsRequired();
            builder.Property(e => e.Details).HasMaxLength(2000);
            builder.HasIndex(e => e.OccurredAtUtc);
        });

        modelBuilder.Entity<EvaluationCase>(builder =>
        {
            builder.ToTable("evaluation_cases");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Question).IsRequired();
            builder.Property(e => e.ExpectedBehavior).IsRequired();
            builder.Property(e => e.ExpectedSources).HasMaxLength(2000);
            builder.Property(e => e.ExecutionNotes).HasMaxLength(4000);
        });

        modelBuilder.Entity<UserFeedback>(builder =>
        {
            builder.ToTable("user_feedbacks");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.SessionId).HasMaxLength(64).IsRequired();
            builder.Property(f => f.Comment).HasMaxLength(2000);
            builder.HasIndex(f => f.CreatedAtUtc);
        });

        modelBuilder.Entity<ChatSession>(builder =>
        {
            builder.ToTable("chat_sessions");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.SessionId).HasMaxLength(64).IsRequired();
            builder.Property(s => s.OwnerId).HasMaxLength(64);
            builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
            builder.HasIndex(s => s.SessionId).IsUnique();
            builder.HasIndex(s => new { s.OwnerId, s.UpdatedAtUtc });
        });

        modelBuilder.Entity<StoredChatMessage>(builder =>
        {
            builder.ToTable("chat_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.SessionId).HasMaxLength(64).IsRequired();
            builder.Property(m => m.Role).HasMaxLength(16).IsRequired();
            builder.Property(m => m.Content).HasColumnType("text").IsRequired();
            builder.HasIndex(m => new { m.SessionId, m.Ordinal });

            builder.HasOne<ChatSession>()
                .WithMany()
                .HasForeignKey(m => m.SessionId)
                .HasPrincipalKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
