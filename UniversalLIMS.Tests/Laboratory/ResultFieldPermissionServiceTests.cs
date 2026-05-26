using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class ResultFieldPermissionServiceTests
{
    [Fact]
    public async Task CanWriteAsync_RegistrarRole_ReturnsFalse()
    {
        var branchId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedSampleWithTemplateFieldAsync(context, branchId, sampleId, dataFieldId);

        var service = CreateService(context, LimsRoles.Registrar);
        var canWrite = await service.CanWriteAsync(sampleId, dataFieldId);

        Assert.False(canWrite);
    }

    [Fact]
    public async Task CanWriteAsync_LaboratoryTechnician_WithWritePermission_ReturnsTrue()
    {
        var branchId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        await using var context = CreateContext();
        var investigationTypeId = await SeedSampleWithTemplateFieldAsync(
            context,
            branchId,
            sampleId,
            dataFieldId,
            fieldId);

        context.TemplateFieldPermissions.Add(new TemplateFieldPermission
        {
            TemplateFieldId = fieldId,
            RoleName = LimsRoles.LaboratoryTechnician,
            AccessLevel = FieldAccessLevel.Write
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, LimsRoles.LaboratoryTechnician);
        var canWrite = await service.CanWriteAsync(sampleId, dataFieldId);

        Assert.True(canWrite);
    }

    [Fact]
    public async Task CanWriteAsync_LaboratoryTechnician_WithReadPermission_ReturnsFalse()
    {
        var branchId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedSampleWithTemplateFieldAsync(
            context,
            branchId,
            sampleId,
            dataFieldId,
            fieldId);

        context.TemplateFieldPermissions.Add(new TemplateFieldPermission
        {
            TemplateFieldId = fieldId,
            RoleName = LimsRoles.LaboratoryTechnician,
            AccessLevel = FieldAccessLevel.Read
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, LimsRoles.LaboratoryTechnician);
        var canWrite = await service.CanWriteAsync(sampleId, dataFieldId);

        Assert.False(canWrite);
    }

    [Fact]
    public async Task CanWriteAsync_FieldNotMappedInPublishedTemplate_ReturnsFalse()
    {
        var branchId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var mappedDataFieldId = Guid.NewGuid();
        var unknownDataFieldId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedSampleWithTemplateFieldAsync(
            context,
            branchId,
            sampleId,
            mappedDataFieldId,
            fieldId);

        context.DataFields.Add(new DataField
        {
            Id = unknownDataFieldId,
            Key = "Result.Unknown",
            DisplayNameUk = "Невідомий показник",
            Scope = DataFieldScope.Result,
            FieldType = DataFieldType.Number,
            IsActive = true
        });

        context.TemplateFieldPermissions.Add(new TemplateFieldPermission
        {
            TemplateFieldId = fieldId,
            RoleName = LimsRoles.LaboratoryTechnician,
            AccessLevel = FieldAccessLevel.Write
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, LimsRoles.LaboratoryTechnician);
        var canWrite = await service.CanWriteAsync(sampleId, unknownDataFieldId);

        Assert.False(canWrite);
    }

    private static async Task<Guid> SeedSampleWithTemplateFieldAsync(
        ApplicationDbContext context,
        Guid branchId,
        Guid sampleId,
        Guid dataFieldId,
        Guid? templateFieldId = null)
    {
        var investigationTypeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = templateFieldId ?? Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.Branches.Add(new Domain.Organization.Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            IsActive = true
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "AIR",
            NameUk = "Повітря",
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-LAB",
            NameUk = "Шаблон",
            Status = TemplateStatus.Active,
            CurrentPublishedVersionId = versionId
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "t.pdf",
            StorageKey = "k",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Result.Test",
            DisplayNameUk = "Тест",
            Scope = DataFieldScope.Result,
            FieldType = DataFieldType.Number,
            IsActive = true
        });

        context.TemplateFields.Add(new TemplateField
        {
            Id = fieldId,
            TemplateVersionId = versionId,
            Tag = "Result.Test",
            Title = "Тест",
            SortOrder = 1,
            DataFieldId = dataFieldId
        });

        context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
        {
            InvestigationTypeId = investigationTypeId,
            TemplateId = templateId,
            IsActive = true,
            SortOrder = 1
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = "Клієнт",
            IsAnnulled = false
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Registered
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-01",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        });

        await context.SaveChangesAsync();
        return investigationTypeId;
    }

    private static ResultFieldPermissionService CreateService(ApplicationDbContext context, string role)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Role, role)
            ],
            "test"))
        };

        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new ResultFieldPermissionService(context, new FixedActiveRoleService(role), accessor);
    }

    private sealed class FixedActiveRoleService(string role) : IActiveLimsRoleService
    {
        public string? GetActiveRole() => role;

        public void SetActiveRole(string roleCode) { }

        public void ClearActiveRole() { }

        public string? ResolveActiveRole(ClaimsPrincipal user) => role;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
