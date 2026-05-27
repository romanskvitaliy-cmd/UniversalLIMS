using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class ResultEntryServiceTests
{
    [Fact]
    public async Task SaveResultValuesAsync_PersistsSampleResultValue_NotOrderFieldValue()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Тестовий Клієнт");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.TestValue", "Тестовий показник");
        var sampleId = await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "SMP-RES-00001");

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var save = await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "12.5",
                        Uncertainty = 0.1m,
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.True(save.Success);
        Assert.Equal(1, save.SavedCount);
        Assert.Single(context.SampleResultValues.Where(value => value.SampleId == sampleId));
        Assert.DoesNotContain(context.OrderFieldValues, fieldValue => fieldValue.OrderId != Guid.Empty);

        var sample = await context.Samples.FindAsync(sampleId);
        Assert.Equal(SampleStatus.InProgress, sample!.Status);
    }

    [Fact]
    public async Task SaveResultValuesAsync_MarkComplete_UpdatesOrderDocumentStatus()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт документ");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.DocFlow", "Показник");
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-DOC"
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-DOC-01",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        });

        context.OrderDocuments.Add(new OrderDocument
        {
            Id = documentId,
            OrderId = orderId,
            SampleId = sampleId,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab,
            SentToLabAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var save = await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                MarkResultsComplete = true,
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "42",
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.True(save.Success);

        var document = await context.OrderDocuments.FindAsync(documentId);
        var sample = await context.Samples.FindAsync(sampleId);

        Assert.Equal(OrderDocumentStatus.ResultsEntered, document!.Status);
        Assert.Equal(SampleStatus.ResultsEntered, sample!.Status);
    }

    [Fact]
    public async Task SaveResultValuesAsync_MarkComplete_DoesNotUpdateOtherSampleDocumentsInSameOrder()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт multi-sample");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.MultiSample", "Показник");
        var orderId = Guid.NewGuid();
        var sampleAId = Guid.NewGuid();
        var sampleBId = Guid.NewGuid();
        var documentAId = Guid.NewGuid();
        var documentBId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-MULTI"
        });

        context.Samples.AddRange(
            new Sample
            {
                Id = sampleAId,
                OrderId = orderId,
                InvestigationTypeId = investigationTypeId,
                Number = "SMP-MULTI-01",
                RegisteredAt = DateTime.UtcNow,
                Status = SampleStatus.Routed
            },
            new Sample
            {
                Id = sampleBId,
                OrderId = orderId,
                InvestigationTypeId = investigationTypeId,
                Number = "SMP-MULTI-02",
                RegisteredAt = DateTime.UtcNow,
                Status = SampleStatus.Routed
            });

        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                Id = documentAId,
                OrderId = orderId,
                SampleId = sampleAId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            },
            new OrderDocument
            {
                Id = documentBId,
                OrderId = orderId,
                SampleId = sampleBId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var save = await service.SaveResultValuesAsync(
            sampleAId,
            new SaveResultEntryRequest
            {
                MarkResultsComplete = true,
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "42",
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.True(save.Success);

        var sampleA = await context.Samples.FindAsync(sampleAId);
        var sampleB = await context.Samples.FindAsync(sampleBId);
        var documentA = await context.OrderDocuments.FindAsync(documentAId);
        var documentB = await context.OrderDocuments.FindAsync(documentBId);

        Assert.Equal(SampleStatus.ResultsEntered, sampleA!.Status);
        Assert.Equal(OrderDocumentStatus.ResultsEntered, documentA!.Status);
        Assert.Equal(SampleStatus.Routed, sampleB!.Status);
        Assert.Equal(OrderDocumentStatus.SentToLab, documentB!.Status);
        Assert.DoesNotContain(context.SampleResultValues, value => value.SampleId == sampleBId);
    }

    [Fact]
    public async Task SaveResultValuesAsync_MarkComplete_WithDocumentId_DoesNotUpdateSiblingDocumentsInSameSample()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт multi-document");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.MultiDocument", "Показник");
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var documentAId = Guid.NewGuid();
        var documentBId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-MULTI-DOC"
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-MULTI-DOC-01",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        });

        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                Id = documentAId,
                OrderId = orderId,
                SampleId = sampleId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            },
            new OrderDocument
            {
                Id = documentBId,
                OrderId = orderId,
                SampleId = sampleId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var save = await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                MarkResultsComplete = true,
                OrderDocumentId = documentAId,
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "42",
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.True(save.Success);

        var sample = await context.Samples.FindAsync(sampleId);
        var documentA = await context.OrderDocuments.FindAsync(documentAId);
        var documentB = await context.OrderDocuments.FindAsync(documentBId);

        Assert.Equal(SampleStatus.InProgress, sample!.Status);
        Assert.Equal(OrderDocumentStatus.ResultsEntered, documentA!.Status);
        Assert.Equal(OrderDocumentStatus.SentToLab, documentB!.Status);
    }

    [Fact]
    public async Task SaveResultValuesAsync_MarkComplete_RequiresDocumentId_WhenSampleHasMultipleLabDocuments()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт require document");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.RequireDocument", "Показник");
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-REQ-DOC"
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "SMP-REQ-DOC-01",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Routed
        });

        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                SampleId = sampleId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            },
            new OrderDocument
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                SampleId = sampleId,
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab,
                SentToLabAtUtc = DateTime.UtcNow
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var save = await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                MarkResultsComplete = true,
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "42",
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.False(save.Success);
        Assert.Equal(0, await context.SampleResultValues.CountAsync(value => value.SampleId == sampleId));
    }

    [Fact]
    public async Task SaveResultValuesAsync_AnnulsPreviousRow_WhenValueChanges()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.Change", "Зміна");
        var sampleId = await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "SMP-RES-00002");

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);

        await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "10",
                        EquipmentId = equipmentId
                    }
                ]
            });

        await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "11",
                        EquipmentId = equipmentId
                    }
                ]
            });

        var allRows = await context.SampleResultValues
            .IgnoreQueryFilters()
            .Where(value => value.SampleId == sampleId && value.DataFieldId == dataFieldId)
            .ToListAsync();

        Assert.Equal(2, allRows.Count);
        Assert.Single(allRows, row => !row.IsAnnulled && row.StoredValue == "11");
        Assert.Single(allRows, row => row.IsAnnulled && row.StoredValue == "10");
    }

    [Fact]
    public async Task SaveResultValuesAsync_NoWritePermission_SkipsAndDoesNotPersist()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт без прав");
        var equipmentId = await SeedEquipmentAsync(context, branchId);
        var dataFieldId = await SeedResultDataFieldAsync(context, "Result.Deny", "Заборонений показник");
        var sampleId = await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "SMP-DENY-00001");

        var service = CreateService(context, branchId, LimsRoles.Specialist);
        var save = await service.SaveResultValuesAsync(
            sampleId,
            new SaveResultEntryRequest
            {
                Values =
                [
                    new SaveResultEntryFieldRequest
                    {
                        DataFieldId = dataFieldId,
                        Value = "99",
                        EquipmentId = equipmentId
                    }
                ]
            });

        Assert.True(save.Success);
        Assert.Equal(0, save.SavedCount);
        Assert.Equal(1, save.SkippedCount);
        Assert.Contains("Пропущено без прав", save.Message);
        Assert.Empty(context.SampleResultValues.Where(value => value.SampleId == sampleId));

        var sample = await context.Samples.FindAsync(sampleId);
        Assert.Equal(SampleStatus.Registered, sample!.Status);
    }

    [Fact]
    public async Task GetResultEntryFormAsync_WhenTemplateHasLinkedResultFields_ReturnsOnlyLinkedFields()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeAsync(context);
        var customer = await SeedCustomerAsync(context, "Клієнт шаблон");
        await SeedEquipmentAsync(context, branchId);
        var linkedFieldId = await SeedResultDataFieldAsync(context, "Result.Linked", "Показник шаблону");
        await SeedResultDataFieldAsync(context, "Result.Generic", "Загальний показник");
        var sampleId = await SeedSampleAsync(
            context,
            customer.Id,
            branchId,
            investigationTypeId,
            "SMP-LINKED-00001");
        await SeedPublishedTemplateFieldAsync(context, investigationTypeId, linkedFieldId);

        var service = CreateService(context, branchId, LimsRoles.LaboratoryTechnician);
        var form = await service.GetResultEntryFormAsync(sampleId);

        Assert.NotNull(form);
        var field = Assert.Single(form.Fields);
        Assert.Equal(linkedFieldId, field.DataFieldId);
        Assert.Equal("Result.Linked", field.Key);
    }

    private static ResultEntryService CreateService(
        ApplicationDbContext context,
        Guid branchId,
        string role)
    {
        var currentUser = new TestCurrentUser(branchId);
        var permissions = new TestPermissionService(role);
        return new ResultEntryService(
            context,
            currentUser,
            new FixedLaboratoryBranchContext(branchId),
            new TestDateTimeProvider(),
            permissions,
            new SampleWorkflowService());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedBranchAsync(ApplicationDbContext context, Guid branchId)
    {
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "TST",
            Name = "Тестова філія",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedInvestigationTypeAsync(ApplicationDbContext context)
    {
        var type = new InvestigationType
        {
            Code = "TST",
            NameUk = "Тестовий тип",
            SortOrder = 1
        };
        context.InvestigationTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context, string fullName)
    {
        var customer = new Customer { FullName = fullName };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Guid> SeedEquipmentAsync(ApplicationDbContext context, Guid branchId)
    {
        var equipment = new Equipment
        {
            Code = "EQ-01",
            NameUk = "Прилад",
            BranchId = branchId,
            IsActive = true
        };
        context.Equipment.Add(equipment);
        await context.SaveChangesAsync();
        return equipment.Id;
    }

    private static async Task<Guid> SeedResultDataFieldAsync(
        ApplicationDbContext context,
        string key,
        string displayName)
    {
        var field = new DataField
        {
            Key = key,
            DisplayNameUk = displayName,
            Scope = DataFieldScope.Result,
            FieldType = DataFieldType.Number,
            Unit = "од.",
            IsActive = true,
            IsSystem = true
        };
        context.DataFields.Add(field);
        await context.SaveChangesAsync();
        return field.Id;
    }

    private static async Task<Guid> SeedSampleAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid investigationTypeId,
        string sampleNumber)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = "REF-001",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var sample = new Sample
        {
            OrderId = order.Id,
            Number = sampleNumber,
            RegisteredAt = DateTime.UtcNow,
            InvestigationTypeId = investigationTypeId,
            Status = SampleStatus.Registered
        };
        context.Samples.Add(sample);
        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            TargetBranchId = branchId,
            Status = OrderDocumentStatus.SentToLab
        });
        await context.SaveChangesAsync();
        return sample.Id;
    }

    private static async Task SeedPublishedTemplateFieldAsync(
        ApplicationDbContext context,
        Guid investigationTypeId,
        Guid dataFieldId)
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "LAB-DEMO",
            NameUk = "Лабораторний демо-шаблон",
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
            OriginalFileName = "lab-demo.pdf",
            StorageKey = "lab-demo",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('b', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        context.TemplateFields.Add(new TemplateField
        {
            TemplateVersionId = versionId,
            Tag = "Result.Linked",
            Title = "Показник шаблону",
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

        await context.SaveChangesAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public TestCurrentUser(Guid branchId) => BranchId = branchId;

        public string? UserId { get; } = "test-user";

        public string? UserName { get; } = "test";

        public string? UserFullName { get; } = "Test User";

        public Guid? BranchId { get; }

        public string? IpAddress { get; }

        public string? UserAgent { get; }

        public string? CorrelationId { get; }

        public bool IsAuthenticated { get; } = true;
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FixedLaboratoryBranchContext : ILaboratoryBranchContext
    {
        private readonly Guid? _branchId;

        public FixedLaboratoryBranchContext(Guid? branchId) => _branchId = branchId;

        public Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaboratoryBranchContextState { ActiveBranchId = _branchId });

        public Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestPermissionService : IResultFieldPermissionService
    {
        private readonly string _role;

        public TestPermissionService(string role) => _role = role;

        public Task<bool> CanWriteAsync(Guid sampleId, Guid dataFieldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(_role, LimsRoles.LaboratoryTechnician, StringComparison.Ordinal)
                            || string.Equals(_role, LimsRoles.SystemAdministrator, StringComparison.Ordinal));
    }
}
