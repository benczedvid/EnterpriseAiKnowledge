using EnterpriseAI.Domain.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAI.Infrastructure.Persistence.Configurations;

public sealed class IndexingRunConfiguration : IEntityTypeConfiguration<IndexingRun>
{
    public void Configure(EntityTypeBuilder<IndexingRun> builder)
    {
        builder.ToTable("IndexingRuns");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.SourceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(run => run.FolderPath)
            .HasMaxLength(2048);

        builder.Property(
                run => run.RequestedMaximumDocuments)
            .IsRequired();

        builder.Property(run => run.ForceReindex)
            .IsRequired();

        builder.Property(run => run.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(run => run.StartedAt)
            .IsRequired();

        builder.Property(run => run.CompletedAt);

        builder.Property(run => run.DiscoveredCount)
            .IsRequired();

        builder.Property(run => run.ProcessedCount)
            .IsRequired();

        builder.Property(run => run.IndexedCount)
            .IsRequired();

        builder.Property(
                run => run.SkippedUnchangedCount)
            .IsRequired();

        builder.Property(
                run => run.SkippedUnsupportedCount)
            .IsRequired();

        builder.Property(run => run.RequiresOcrCount)
            .IsRequired();

        builder.Property(run => run.FailedCount)
            .IsRequired();

        builder.Property(run => run.NotFoundCount)
            .IsRequired();

        builder.Property(run => run.TotalChunkCount)
            .IsRequired();

        builder.Property(run => run.FailureMessage)
            .HasMaxLength(2000);

        builder.Ignore(run => run.Duration);

        builder.HasIndex(run => run.StartedAt);

        builder.HasIndex(run => run.Status);

        builder.HasIndex(
            run => new
            {
                run.SourceType,
                run.StartedAt
            });

        builder.HasMany(run => run.Items)
            .WithOne()
            .HasForeignKey(item => item.IndexingRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(run => run.Items)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
