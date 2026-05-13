using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class DataFieldConfiguration : IEntityTypeConfiguration<DataField>
{
    public void Configure(EntityTypeBuilder<DataField> builder)
    {
        builder.ToTable("DataFields");

        builder.HasKey(dataField => dataField.Id);

        builder.Property(dataField => dataField.Key)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(dataField => dataField.DisplayNameUk)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(dataField => dataField.DescriptionUk)
            .HasMaxLength(1000);

        builder.Property(dataField => dataField.FieldType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(dataField => dataField.Scope)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(dataField => dataField.Unit)
            .HasMaxLength(50);

        builder.Property(dataField => dataField.Format)
            .HasMaxLength(100);

        builder.Property(dataField => dataField.ValidationRegex)
            .HasMaxLength(500);

        builder.Property(dataField => dataField.ExampleValue)
            .HasMaxLength(1000);

        builder.Property(dataField => dataField.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(dataField => dataField.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(dataField => dataField.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(dataField => dataField.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(dataField => dataField.IsActive)
            .HasDefaultValue(true);

        builder.Property(dataField => dataField.RowVersion)
            .IsRowVersion();

        builder.HasIndex(dataField => dataField.Key)
            .IsUnique();

        builder.HasQueryFilter(dataField => !dataField.IsAnnulled);
    }
}
