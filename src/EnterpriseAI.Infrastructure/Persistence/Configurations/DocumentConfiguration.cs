using EnterpriseAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAI.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration
    : IEntityTypeConfiguration<Document>
{
    public void Configure(
        EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.SourceType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.SourceId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(document => document.SourceUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(document => document.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(document => document.Title)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(document => document.MimeType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(document => document.FolderPath)
            .HasMaxLength(2048);

        builder.Property(document => document.ETag)
            .HasMaxLength(500);

        builder.Property(document => document.ProcessingStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(document => document.FailureReason)
            .HasMaxLength(2000);

        builder.HasIndex(
                document => new
                {
                    document.SourceType,
                    document.SourceId
                })
            .IsUnique();

        builder.HasIndex(document => document.ProcessingStatus);

        builder.HasIndex(document => document.IsDeleted);

        builder.HasMany(document => document.Chunks)
            .WithOne()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(document => document.Chunks)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}