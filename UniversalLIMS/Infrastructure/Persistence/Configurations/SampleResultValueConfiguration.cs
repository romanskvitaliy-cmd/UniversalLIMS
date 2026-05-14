using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class SampleResultValueConfiguration : IEntityTypeConfiguration<SampleResultValue>
{
    public void Configure(EntityTypeBuilder<SampleResultValue> builder)
    {
        builder.ToTable("SampleResultValues");

        builder.HasKey(resultValue => resultValue.Id);

        builder.Property(resultValue => resultValue.StoredValue)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(resultValue => resultValue.Unit)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(resultValue => resultValue.Uncertainty)
            .HasPrecision(18, 6);

        builder.Property(resultValue => resultValue.EnteredAtUtc)
            .HasColumnType("datetime2");

        builder.Property(resultValue => resultValue.EnteredByUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(resultValue => resultValue.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(resultValue => resultValue.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(resultValue => resultValue.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(resultValue => resultValue.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(resultValue => resultValue.RowVersion)
            .IsRowVersion();

        builder.HasIndex(resultValue => new { resultValue.SampleId, resultValue.DataFieldId })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasOne(resultValue => resultValue.Sample)
            .WithMany(sample => sample.ResultValues)
            .HasForeignKey(resultValue => resultValue.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(resultValue => resultValue.DataField)
            .WithMany()
            .HasForeignKey(resultValue => resultValue.DataFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(resultValue => resultValue.Equipment)
            .WithMany()
            .HasForeignKey(resultValue => resultValue.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(resultValue => !resultValue.IsAnnulled);
    }
}
