using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class OrderFieldValueConfiguration : IEntityTypeConfiguration<OrderFieldValue>
{
    public void Configure(EntityTypeBuilder<OrderFieldValue> builder)
    {
        builder.ToTable("OrderFieldValues");

        builder.HasKey(fieldValue => fieldValue.Id);

        builder.Property(fieldValue => fieldValue.StoredValue)
            .HasMaxLength(4000);

        builder.Property(fieldValue => fieldValue.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(fieldValue => fieldValue.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(fieldValue => fieldValue.RowVersion)
            .IsRowVersion();

        builder.HasIndex(fieldValue => new { fieldValue.OrderId, fieldValue.SampleId, fieldValue.DataFieldId })
            .IsUnique()
            .HasFilter("[SampleId] IS NOT NULL");

        builder.HasIndex(fieldValue => new { fieldValue.OrderId, fieldValue.DataFieldId })
            .IsUnique()
            .HasFilter("[SampleId] IS NULL");

        builder.HasOne(fieldValue => fieldValue.DataField)
            .WithMany()
            .HasForeignKey(fieldValue => fieldValue.DataFieldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
