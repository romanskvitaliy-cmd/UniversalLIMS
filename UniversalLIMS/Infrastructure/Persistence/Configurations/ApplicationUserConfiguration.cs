using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Identity;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.Position)
            .HasMaxLength(200);

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(user => user.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(user => user.Branch)
            .WithMany()
            .HasForeignKey(user => user.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(user => user.BranchId);
    }
}
