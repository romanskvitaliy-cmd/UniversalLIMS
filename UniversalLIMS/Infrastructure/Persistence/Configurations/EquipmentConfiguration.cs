using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");

        builder.HasKey(equipment => equipment.Id);

        builder.Property(equipment => equipment.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(equipment => equipment.NameUk)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(equipment => equipment.SerialNumber)
            .HasMaxLength(100);

        builder.Property(equipment => equipment.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(equipment => equipment.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(equipment => equipment.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(equipment => equipment.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(equipment => equipment.IsActive)
            .HasDefaultValue(true);

        builder.Property(equipment => equipment.RowVersion)
            .IsRowVersion();

        builder.HasIndex(equipment => equipment.Code)
            .IsUnique();

        builder.HasOne(equipment => equipment.Branch)
            .WithMany()
            .HasForeignKey(equipment => equipment.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(equipment => !equipment.IsAnnulled);
    }
}
