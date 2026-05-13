using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class InvestigationTypeConfiguration : IEntityTypeConfiguration<InvestigationType>
{
    public void Configure(EntityTypeBuilder<InvestigationType> builder)
    {
        builder.ToTable("InvestigationTypes");

        builder.HasKey(investigationType => investigationType.Id);

        builder.Property(investigationType => investigationType.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(investigationType => investigationType.NameUk)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(investigationType => investigationType.DescriptionUk)
            .HasMaxLength(1000);

        builder.Property(investigationType => investigationType.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(investigationType => investigationType.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(investigationType => investigationType.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(investigationType => investigationType.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(investigationType => investigationType.IsActive)
            .HasDefaultValue(true);

        builder.Property(investigationType => investigationType.RowVersion)
            .IsRowVersion();

        builder.HasIndex(investigationType => investigationType.Code)
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasMany(investigationType => investigationType.Samples)
            .WithOne(sample => sample.InvestigationType)
            .HasForeignKey(sample => sample.InvestigationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(investigationType => !investigationType.IsAnnulled);
    }
}
