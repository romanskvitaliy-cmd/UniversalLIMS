using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("Templates");

        builder.HasKey(template => template.Id);

        builder.Property(template => template.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(template => template.NameUk)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(template => template.DescriptionUk)
            .HasMaxLength(1000);

        builder.Property(template => template.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(template => template.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(template => template.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(template => template.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(template => template.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(template => template.RowVersion)
            .IsRowVersion();

        builder.HasIndex(template => template.Code)
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasOne(template => template.CurrentPublishedVersion)
            .WithMany()
            .HasForeignKey(template => template.CurrentPublishedVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(template => template.Versions)
            .WithOne(version => version.Template)
            .HasForeignKey(version => version.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(template => !template.IsAnnulled);
    }
}
