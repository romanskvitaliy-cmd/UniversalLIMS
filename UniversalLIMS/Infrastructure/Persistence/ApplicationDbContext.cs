using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Domain.Audit;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<DataField> DataFields => Set<DataField>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();

    public DbSet<TemplateFieldSegment> TemplateFieldSegments => Set<TemplateFieldSegment>();

    public DbSet<TemplateFieldPermission> TemplateFieldPermissions => Set<TemplateFieldPermission>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<InvestigationType> InvestigationTypes => Set<InvestigationType>();

    public DbSet<InvestigationTypeTemplate> InvestigationTypeTemplates => Set<InvestigationTypeTemplate>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Sample> Samples => Set<Sample>();

    public DbSet<OrderDocument> OrderDocuments => Set<OrderDocument>();

    public DbSet<OrderFieldValue> OrderFieldValues => Set<OrderFieldValue>();

    public DbSet<OrderFieldLinkGroup> OrderFieldLinkGroups => Set<OrderFieldLinkGroup>();

    public DbSet<OrderFieldLinkMember> OrderFieldLinkMembers => Set<OrderFieldLinkMember>();

    public DbSet<FieldTextLibraryEntry> FieldTextLibraryEntries => Set<FieldTextLibraryEntry>();

    public DbSet<SampleResultValue> SampleResultValues => Set<SampleResultValue>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ConfigureDeleteBehavior(builder);
    }

    private static void ConfigureDeleteBehavior(ModelBuilder builder)
    {
        foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
        {
            if (!foreignKey.IsOwnership)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
