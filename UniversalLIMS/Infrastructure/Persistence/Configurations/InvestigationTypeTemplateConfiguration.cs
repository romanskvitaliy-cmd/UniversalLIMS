using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class InvestigationTypeTemplateConfiguration : IEntityTypeConfiguration<InvestigationTypeTemplate>
{
    public void Configure(EntityTypeBuilder<InvestigationTypeTemplate> builder)
    {
        builder.ToTable("InvestigationTypeTemplates");

        builder.HasKey(join => new { join.InvestigationTypeId, join.TemplateId });

        builder.Property(join => join.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(join => join.InvestigationType)
            .WithMany(investigationType => investigationType.InvestigationTypeTemplates)
            .HasForeignKey(join => join.InvestigationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(join => join.Template)
            .WithMany()
            .HasForeignKey(join => join.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(join => new { join.InvestigationTypeId, join.SortOrder });
    }
}
