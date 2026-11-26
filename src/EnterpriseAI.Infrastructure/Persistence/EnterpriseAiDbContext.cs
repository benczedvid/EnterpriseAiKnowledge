using EnterpriseAI.Domain.Documents;
using EnterpriseAI.Domain.Indexing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAI.Infrastructure.Persistence;

public sealed class EnterpriseAiDbContext : DbContext
{
    public EnterpriseAiDbContext(DbContextOptions<EnterpriseAiDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IndexingRun> IndexingRuns => Set<IndexingRun>();
    public DbSet<IndexingRunItem> IndexingRunItems => Set<IndexingRunItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnterpriseAiDbContext).Assembly);
    }
}