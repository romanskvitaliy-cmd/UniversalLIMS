using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("Samples");

        builder.HasKey(sample => sample.Id);

        builder.Property(sample => sample.Number)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(sample => sample.RegisteredAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(sample => sample.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sample => sample.DeliveryStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(sample => sample.ReadyForPickupAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.IssuedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.IssuedByUserId)
            .HasMaxLength(450);

        builder.Property(sample => sample.RoutedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.ResultsEnteredAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.Notes)
            .HasMaxLength(2000);

        builder.Property(sample => sample.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sample => sample.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(sample => sample.RowVersion)
            .IsRowVersion();

        builder.HasIndex(sample => sample.Number)
            .HasFilter("[IsAnnulled] = 0");

        builder.HasIndex(sample => new { sample.OrderId, sample.Number })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasMany(sample => sample.OrderDocuments)
            .WithOne(document => document.Sample)
            .HasForeignKey(document => document.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(sample => sample.FieldValues)
            .WithOne(fieldValue => fieldValue.Sample)
            .HasForeignKey(fieldValue => fieldValue.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(sample => !sample.IsAnnulled);
    }
}
