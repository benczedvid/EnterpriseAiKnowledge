using EnterpriseAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace EnterpriseAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration
    : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(
        EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.DocumentId)
            .IsRequired();

        builder.Property(chunk => chunk.Content)
            .IsRequired();

        builder.Property(chunk => chunk.SectionTitle)
            .HasMaxLength(1000);

        builder.Property(chunk => chunk.PageNumber);

        builder.Property(chunk => chunk.ChunkIndex)
            .IsRequired();

        builder.Property(chunk => chunk.TokenCount)
            .IsRequired();

        var embeddingConverter =
            new ValueConverter<float[], Vector>(
                embedding => new Vector(embedding),
                vector => vector.ToArray());

        var embeddingComparer =
            new ValueComparer<float[]>(
                (left, right) =>
                    left != null
                    && right != null
                    && left.SequenceEqual(right),
                embedding =>
                    embedding.Aggregate(
                        0,
                        (hash, value) =>
                            HashCode.Combine(hash, value)),
                embedding => embedding.ToArray());

        builder.Property(chunk => chunk.Embedding)
            .HasConversion(embeddingConverter)
            .Metadata.SetValueComparer(embeddingComparer);

        builder.Property(chunk => chunk.Embedding)
            .HasColumnType(
                $"vector({DocumentChunk.EmbeddingDimensions})")
            .IsRequired();

        builder.HasIndex(
                chunk => new
                {
                    chunk.DocumentId,
                    chunk.ChunkIndex
                })
            .IsUnique();

        builder.HasIndex(chunk => chunk.DocumentId);
    }
}