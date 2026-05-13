using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Persistence.Interceptors;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class OrderRegistrationServiceTests
{
    [Fact]
    public async Task RegisterSampleAsync_CreatesOrderSampleNumbersAndDocuments()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            FullName = "Іваненко Іван"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "WATER",
            NameUk = "Вода",
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "REF-TPL",
            NameUk = "Направлення",
            CurrentPublishedVersionId = versionId
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "ref.pdf",
            StorageKey = "templates/ref.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
        {
            InvestigationTypeId = investigationTypeId,
            TemplateId = templateId,
            SortOrder = 1
        });

        await context.SaveChangesAsync();

        var service = CreateRegistrationService(context);
        var result = await service.RegisterSampleAsync(new RegisterSampleRequest
        {
            CustomerId = customerId,
            InvestigationTypeId = investigationTypeId,
            RegistrationBranchId = branchId,
            TargetBranchId = branchId
        });

        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.StartsWith("REF-ZHY-", result.ReferralNumber);
        Assert.StartsWith("ZHY-", result.SampleNumber);
        Assert.Equal(1, result.DocumentsCreated);

        var order = await context.Orders
            .Include(item => item.Samples)
            .Include(item => item.OrderDocuments)
            .FirstAsync(item => item.Id == result.OrderId);

        Assert.Equal(OrderStatus.Registered, order.Status);
        Assert.Single(order.Samples);
        Assert.Single(order.OrderDocuments);
        Assert.Equal(customerId, order.CustomerId);
    }

    [Fact]
    public async Task OrderFieldValueService_RejectsReservedStaticKey()
    {
        await using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            Status = OrderStatus.Draft
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Customer.FullName",
            DisplayNameUk = "ПІБ",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        await context.SaveChangesAsync();

        var service = new OrderFieldValueService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAsync(
            orderId,
            [new OrderFieldValueInput { DataFieldId = dataFieldId, StoredValue = "Test" }]));
    }

    private static OrderRegistrationService CreateRegistrationService(ApplicationDbContext context)
    {
        var dateTimeProvider = new FixedDateTimeProvider(new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc));
        var numberingService = new NumberingService(context, dateTimeProvider);
        var fieldValueService = new OrderFieldValueService(context);
        return new OrderRegistrationService(context, dateTimeProvider, numberingService, fieldValueService);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }
}
