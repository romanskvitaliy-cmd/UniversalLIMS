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
        await context.SaveChangesAsync();
        return sample.Id;
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

    private sealed class TestPermissionService : IResultFieldPermissionService
    {
        private readonly string _role;

        public TestPermissionService(string role) => _role = role;

        public Task<bool> CanWriteAsync(Guid sampleId, Guid dataFieldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(_role, LimsRoles.LaboratoryTechnician, StringComparison.Ordinal)
                            || string.Equals(_role, LimsRoles.SystemAdministrator, StringComparison.Ordinal));
    }
}
