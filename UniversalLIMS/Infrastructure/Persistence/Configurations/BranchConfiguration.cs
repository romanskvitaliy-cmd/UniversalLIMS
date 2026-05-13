using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.HasKey(branch => branch.Id);

        builder.Property(branch => branch.Code)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(branch => branch.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(branch => branch.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(branch => branch.Address)
            .HasMaxLength(500);

        builder.Property(branch => branch.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(branch => branch.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(branch => branch.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(branch => branch.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(branch => branch.IsActive)
            .HasDefaultValue(true);

        builder.Property(branch => branch.RowVersion)
            .IsRowVersion();

        builder.HasIndex(branch => branch.Code)
            .IsUnique();

        builder.HasQueryFilter(branch => !branch.IsAnnulled);
    }
}
