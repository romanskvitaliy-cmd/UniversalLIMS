using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Audit;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.UserId)
            .HasMaxLength(450);

        builder.Property(auditLog => auditLog.UserName)
            .HasMaxLength(256);

        builder.Property(auditLog => auditLog.UserFullName)
            .HasMaxLength(200);

        builder.Property(auditLog => auditLog.Action)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasMaxLength(100);

        builder.Property(auditLog => auditLog.ChangedProperties)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(auditLog => auditLog.OldValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(auditLog => auditLog.NewValues)
            .HasColumnType("nvarchar(max)");

        builder.Property(auditLog => auditLog.Reason)
            .HasMaxLength(1000);

        builder.Property(auditLog => auditLog.CorrelationId)
            .HasMaxLength(100);

        builder.Property(auditLog => auditLog.IpAddress)
            .HasMaxLength(64);

        builder.Property(auditLog => auditLog.UserAgent)
            .HasMaxLength(512);

        builder.Property(auditLog => auditLog.TimestampUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(auditLog => auditLog.TimestampUtc);
        builder.HasIndex(auditLog => new { auditLog.EntityName, auditLog.EntityId });
        builder.HasIndex(auditLog => auditLog.UserId);
        builder.HasIndex(auditLog => auditLog.BranchId);
    }
}
