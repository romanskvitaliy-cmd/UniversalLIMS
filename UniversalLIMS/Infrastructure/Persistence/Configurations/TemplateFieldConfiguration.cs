using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    public void Configure(EntityTypeBuilder<TemplateField> builder)
    {
        builder.ToTable("TemplateFields");

        builder.HasKey(field => field.Id);

        builder.Property(field => field.Tag)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(field => field.NormalizedTag)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(field => field.Title)
            .HasMaxLength(200);

        builder.Property(field => field.WordControlType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(field => field.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(field => field.FieldType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(field => field.OverflowPolicy)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(field => field.DetectedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(field => field.LastMappedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(field => field.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(field => field.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(field => field.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(field => field.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(field => field.RowVersion)
            .IsRowVersion();

        builder.HasIndex(field => new { field.TemplateVersionId, field.NormalizedTag })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasIndex(field => field.DataFieldId);

        builder.HasOne(field => field.DataField)
            .WithMany()
            .HasForeignKey(field => field.DataFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(field => field.Permissions)
            .WithOne(permission => permission.TemplateField)
            .HasForeignKey(permission => permission.TemplateFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(field => field.Segments)
            .WithOne(segment => segment.TemplateField)
            .HasForeignKey(segment => segment.TemplateFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(field => !field.IsAnnulled);
    }
}
