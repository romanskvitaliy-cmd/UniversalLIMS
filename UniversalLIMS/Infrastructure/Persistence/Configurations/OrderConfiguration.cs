using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(order => order.ReferralNumber)
            .HasMaxLength(64);

        builder.Property(order => order.RegisteredAtUtc)
            .HasColumnType("datetime2");

        builder.Property(order => order.Notes)
            .HasMaxLength(2000);

        builder.Property(order => order.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(order => order.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(order => order.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(order => order.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(order => order.RowVersion)
            .IsRowVersion();

        builder.HasIndex(order => order.ReferralNumber)
            .HasFilter("[ReferralNumber] IS NOT NULL AND [IsAnnulled] = 0");

        builder.HasIndex(order => new { order.BranchId, order.Status });

        builder.HasOne(order => order.Customer)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.Branch)
            .WithMany()
            .HasForeignKey(order => order.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Samples)
            .WithOne(sample => sample.Order)
            .HasForeignKey(sample => sample.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.OrderDocuments)
            .WithOne(document => document.Order)
            .HasForeignKey(document => document.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.FieldValues)
            .WithOne(fieldValue => fieldValue.Order)
            .HasForeignKey(fieldValue => fieldValue.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(order => !order.IsAnnulled);
    }
}
