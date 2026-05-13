using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class TemplateFieldSegmentConfiguration : IEntityTypeConfiguration<TemplateFieldSegment>
{
    public void Configure(EntityTypeBuilder<TemplateFieldSegment> builder)
    {
        builder.ToTable("TemplateFieldSegments");

        builder.HasKey(segment => segment.Id);

        builder.Property(segment => segment.TextAlignment)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(segment => segment.PositionX)
            .HasColumnType("decimal(12,4)");

        builder.Property(segment => segment.PositionY)
            .HasColumnType("decimal(12,4)");

        builder.Property(segment => segment.Width)
            .HasColumnType("decimal(12,4)");

        builder.Property(segment => segment.Height)
            .HasColumnType("decimal(12,4)");

        builder.Property(segment => segment.FontName)
            .HasMaxLength(128);

        builder.Property(segment => segment.FontSize)
            .HasColumnType("decimal(8,2)");

        builder.Property(segment => segment.LineHeight)
            .HasColumnType("decimal(8,2)");

        builder.Property(segment => segment.SvgPathData)
            .HasColumnType("nvarchar(max)");

        builder.Property(segment => segment.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(segment => segment.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(segment => segment.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(segment => segment.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(segment => segment.RowVersion)
            .IsRowVersion();

        builder.HasIndex(segment => new { segment.TemplateFieldId, segment.Sequence })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasOne(segment => segment.TemplateField)
            .WithMany(field => field.Segments)
            .HasForeignKey(segment => segment.TemplateFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(segment => !segment.IsAnnulled);
    }
}
