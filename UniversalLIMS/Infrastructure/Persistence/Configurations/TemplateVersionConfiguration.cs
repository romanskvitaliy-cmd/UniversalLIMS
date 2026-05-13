using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("TemplateVersions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(version => version.DocumentFormat)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(version => version.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(version => version.StorageKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(version => version.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(version => version.Sha256Hash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(version => version.PublicationNotesUk)
            .HasMaxLength(1000);

        builder.Property(version => version.UploadedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(version => version.PublishedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(version => version.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(version => version.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(version => version.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(version => version.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(version => version.RowVersion)
            .IsRowVersion();

        builder.HasIndex(version => new { version.TemplateId, version.VersionNumber })
            .IsUnique();

        builder.HasIndex(version => new { version.TemplateId, version.Status });

        builder.HasIndex(version => version.BasedOnTemplateVersionId);

        builder.HasOne(version => version.BasedOnTemplateVersion)
            .WithMany()
            .HasForeignKey(version => version.BasedOnTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(version => version.Fields)
            .WithOne(field => field.TemplateVersion)
            .HasForeignKey(field => field.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(version => !version.IsAnnulled);
    }
}
