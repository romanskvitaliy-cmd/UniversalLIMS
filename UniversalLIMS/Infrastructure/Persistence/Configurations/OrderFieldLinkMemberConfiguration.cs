using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class OrderFieldLinkMemberConfiguration : IEntityTypeConfiguration<OrderFieldLinkMember>
{
    public void Configure(EntityTypeBuilder<OrderFieldLinkMember> builder)
    {
        builder.ToTable("OrderFieldLinkMembers");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(member => member.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(member => new { member.GroupId, member.TemplateFieldId })
            .IsUnique();

        builder.HasIndex(member => member.TemplateVersionId);

        builder.HasOne(member => member.Group)
            .WithMany(group => group.Members)
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.TemplateVersion)
            .WithMany()
            .HasForeignKey(member => member.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(member => member.TemplateField)
            .WithMany()
            .HasForeignKey(member => member.TemplateFieldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
