using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class OrderDocumentConfiguration : IEntityTypeConfiguration<OrderDocument>
{
    public void Configure(EntityTypeBuilder<OrderDocument> builder)
    {
        builder.ToTable("OrderDocuments");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(document => document.SentToLabAtUtc)
            .HasColumnType("datetime2");

        builder.Property(document => document.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(document => document.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(document => document.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(document => document.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(document => document.RowVersion)
            .IsRowVersion();

        builder.HasIndex(document => new { document.SampleId, document.TemplateId })
            .HasFilter("[IsAnnulled] = 0");

        builder.HasIndex(document => new { document.TargetBranchId, document.Status });

        builder.HasOne(document => document.Template)
            .WithMany()
            .HasForeignKey(document => document.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(document => document.TemplateVersion)
            .WithMany()
            .HasForeignKey(document => document.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(document => document.TargetBranch)
            .WithMany()
            .HasForeignKey(document => document.TargetBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(document => !document.IsAnnulled);
    }
}
