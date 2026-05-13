using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class TemplateFieldPermissionConfiguration : IEntityTypeConfiguration<TemplateFieldPermission>
{
    public void Configure(EntityTypeBuilder<TemplateFieldPermission> builder)
    {
        builder.ToTable("TemplateFieldPermissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.RoleName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(permission => permission.AccessLevel)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(permission => permission.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(permission => permission.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(permission => permission.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(permission => permission.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(permission => permission.RowVersion)
            .IsRowVersion();

        builder.HasIndex(permission => new { permission.TemplateFieldId, permission.RoleName })
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasIndex(permission => new { permission.RoleName, permission.AccessLevel });

        builder.HasQueryFilter(permission => !permission.IsAnnulled);
    }
}
