using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Kind)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(customer => customer.FullName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(customer => customer.OrganizationName)
            .HasMaxLength(300);

        builder.Property(customer => customer.ContactPhone)
            .HasMaxLength(50);

        builder.Property(customer => customer.Email)
            .HasMaxLength(256);

        builder.Property(customer => customer.Address)
            .HasMaxLength(500);

        builder.Property(customer => customer.Edrpou)
            .HasMaxLength(10);

        builder.Property(customer => customer.Rnokpp)
            .HasMaxLength(12);

        builder.Property(customer => customer.Notes)
            .HasMaxLength(2000);

        builder.Property(customer => customer.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(customer => customer.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(customer => customer.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(customer => customer.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(customer => customer.RowVersion)
            .IsRowVersion();

        builder.HasIndex(customer => customer.FullName);

        builder.HasIndex(customer => customer.OrganizationName);

        builder.HasIndex(customer => customer.ContactPhone);

        builder.HasIndex(customer => customer.Edrpou)
            .HasFilter("[Edrpou] IS NOT NULL AND [IsAnnulled] = 0");

        builder.HasIndex(customer => customer.Rnokpp)
            .HasFilter("[Rnokpp] IS NOT NULL AND [IsAnnulled] = 0");

        builder.HasMany(customer => customer.Orders)
            .WithOne(order => order.Customer)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(customer => !customer.IsAnnulled);
    }
}
