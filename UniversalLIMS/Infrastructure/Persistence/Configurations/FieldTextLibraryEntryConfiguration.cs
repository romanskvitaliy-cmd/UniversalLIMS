using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class FieldTextLibraryEntryConfiguration : IEntityTypeConfiguration<FieldTextLibraryEntry>
{
    public void Configure(EntityTypeBuilder<FieldTextLibraryEntry> builder)
    {
        builder.ToTable("FieldTextLibraryEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.NormalizedTag)
            .HasMaxLength(256);

        builder.Property(entry => entry.Body)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(entry => entry.NormalizedBodyHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entry => entry.ShortLabel)
            .HasMaxLength(200);

        builder.Property(entry => entry.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(entry => entry.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(entry => entry.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(entry => entry.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(entry => entry.RowVersion)
            .IsRowVersion();

        builder.HasIndex(entry => new { entry.BranchId, entry.DataFieldId, entry.NormalizedBodyHash })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL AND [TemplateVersionId] IS NULL");

        builder.HasIndex(entry => new { entry.BranchId, entry.DataFieldId, entry.TemplateVersionId, entry.NormalizedBodyHash })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0 AND [DataFieldId] IS NOT NULL AND [TemplateVersionId] IS NOT NULL");

        builder.HasIndex(entry => new { entry.BranchId, entry.NormalizedTag, entry.NormalizedBodyHash })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL AND [TemplateVersionId] IS NULL");

        builder.HasIndex(entry => new { entry.BranchId, entry.NormalizedTag, entry.TemplateVersionId, entry.NormalizedBodyHash })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0 AND [DataFieldId] IS NULL AND [NormalizedTag] IS NOT NULL AND [TemplateVersionId] IS NOT NULL");

        builder.HasIndex(entry => new { entry.BranchId, entry.DataFieldId, entry.UsageCount });

        builder.HasOne(entry => entry.Branch)
            .WithMany()
            .HasForeignKey(entry => entry.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.DataField)
            .WithMany()
            .HasForeignKey(entry => entry.DataFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.TemplateVersion)
            .WithMany()
            .HasForeignKey(entry => entry.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entry => new { entry.BranchId, entry.TemplateVersionId, entry.DataFieldId });

        builder.HasQueryFilter(entry => !entry.IsAnnulled);
    }
}
