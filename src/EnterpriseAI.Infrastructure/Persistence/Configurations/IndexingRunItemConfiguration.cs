using EnterpriseAI.Domain.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAI.Infrastructure.Persistence.Configurations;

public sealed class IndexingRunItemConfiguration : IEntityTypeConfiguration<IndexingRunItem>
{
    public void Configure(EntityTypeBuilder<IndexingRunItem> builder)
    {
        builder.ToTable("IndexingRunItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.IndexingRunId)
            .IsRequired();

        builder.Property(item => item.SourceId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(item => item.FileName)
            .HasMaxLength(500);

        builder.Property(item => item.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(item => item.Message)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(item => item.DocumentId);

        builder.Property(item => item.ChunkCount)
            .IsRequired();

        builder.Property(item => item.CharacterCount)
            .IsRequired();

        builder.Property(item => item.IndexedAt);

        builder.Property(item => item.CreatedAt)
            .IsRequired();

        builder.HasIndex(
                item => new
                {
                    item.IndexingRunId,
                    item.SourceId
                })
            .IsUnique();

        builder.HasIndex(item => item.Status);

        builder.HasIndex(item => item.DocumentId);
    }
}