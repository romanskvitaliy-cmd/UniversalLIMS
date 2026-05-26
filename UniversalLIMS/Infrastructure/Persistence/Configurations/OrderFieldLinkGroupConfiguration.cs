using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class OrderFieldLinkGroupConfiguration : IEntityTypeConfiguration<OrderFieldLinkGroup>
{
    public void Configure(EntityTypeBuilder<OrderFieldLinkGroup> builder)
    {
        builder.ToTable("OrderFieldLinkGroups");

        builder.HasKey(group => group.Id);

        builder.Property(group => group.Label)
            .HasMaxLength(500);

        builder.Property(group => group.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(group => group.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(group => group.OrderId);

        builder.HasOne(group => group.Order)
            .WithMany(order => order.FieldLinkGroups)
            .HasForeignKey(group => group.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
