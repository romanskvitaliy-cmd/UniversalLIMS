using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Laboratory.Dtos;
using UniversalLIMS.Application.Laboratory.Services;
using UniversalLIMS.Domain.Common.Exceptions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Persistence.Queries;
using UniversalLIMS.Infrastructure.Persistence.Repositories;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class LaboratoryJournalServiceTests
{
    [Fact]
    public async Task GetJournalAsync_ReturnsOnlyRoutedSamplesWithResultFields()
    {
        await using var context = CreateContext();
        var fixture = await SeedLaboratoryFixtureAsync(context);
        var service = CreateJournalService(context);

        var result = await service.GetJournalAsync(new LaboratoryJournalFilter());

        Assert.Single(result.Items);
        Assert.Equal(fixture.RoutedSampleId, result.Items[0].SampleId);
        Assert.Equal(1, result.Items[0].RequiredResultsCount);
        Assert.Equal(0, result.Items[0].EnteredResultsCount);
    }

    [Fact]
    public async Task GetJournalAsync_FiltersBySearchText()
    {
        await using var context = CreateContext();
        var fixture = await SeedLaboratoryFixtureAsync(context);
        var service = CreateJournalService(context);

        var bySampleNumber = await service.GetJournalAsync(new LaboratoryJournalFilter
        {
            SearchText = fixture.SampleNumber
        });

        var byCustomer = await service.GetJournalAsync(new LaboratoryJournalFilter
        {
            SearchText = "Петренко"
        });

        Assert.Single(bySampleNumber.Items);
        Assert.Single(byCustomer.Items);
    }

    [Fact]
    public async Task GetSampleDetailsAsync_ReturnsActiveResultsFromResultService()
    {
        await using var context = CreateContext();
        var fixture = await SeedLaboratoryFixtureAsync(context);
        var userId = Guid.NewGuid();
        var resultService = CreateResultService(context);
        await resultService.AddResultAsync(
            fixture.RoutedSampleId,
            fixture.ResultDataFieldId,
            "12.5",
            unit: "мг/л",
            uncertainty: "0.1",
            fixture.EquipmentId,
            userId);

        var service = CreateJournalService(context);
        var details = await service.GetSampleDetailsAsync(fixture.RoutedSampleId);

        Assert.Equal(fixture.SampleNumber, details.SampleNumber);
        Assert.Single(details.Results);
        Assert.Equal("12.5", details.Results[0].StoredValue);
        Assert.Equal("Result.pH", details.Results[0].DataFieldKey);
        Assert.Equal(1, details.EnteredResultsCount);
    }

    [Fact]
    public async Task CanFinalizeSampleAsync_ReturnsFalseUntilAllRequiredResultsEntered()
    {
        await using var context = CreateContext();
        var fixture = await SeedLaboratoryFixtureAsync(context);
        var service = CreateJournalService(context);

        Assert.False(await service.CanFinalizeSampleAsync(fixture.RoutedSampleId));

        await CreateResultService(context).AddResultAsync(
            fixture.RoutedSampleId,
            fixture.ResultDataFieldId,
            "7.0",
            unit: null,
            uncertainty: null,
            fixture.EquipmentId,
            Guid.NewGuid());

        Assert.True(await service.CanFinalizeSampleAsync(fixture.RoutedSampleId));
    }

    [Fact]
    public async Task GetSampleDetailsAsync_ThrowsWhenSampleNotInJournal()
    {
        await using var context = CreateContext();
        var fixture = await SeedLaboratoryFixtureAsync(context);
        var service = CreateJournalService(context);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.GetSampleDetailsAsync(fixture.RegisteredSampleId));
    }

    [Fact]
    public async Task GetSampleDetailsAsync_ThrowsEntityNotFoundForMissingSample()
    {
        await using var context = CreateContext();
        var service = CreateJournalService(context);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetSampleDetailsAsync(Guid.NewGuid()));
    }

    private static LaboratoryJournalService CreateJournalService(ApplicationDbContext context)
    {
        var journalQuery = new LaboratoryJournalQuery(context);
        var resultService = CreateResultService(context);
        var dataFieldRepository = new LaboratoryDataFieldRepository(context);
        var equipmentRepository = new LaboratoryEquipmentRepository(context);
        var currentUserService = new FixedCurrentUserService();

        return new LaboratoryJournalService(
            journalQuery,
            resultService,
            dataFieldRepository,
            equipmentRepository,
            currentUserService);
    }

    private static SampleResultService CreateResultService(ApplicationDbContext context)
    {
        var dateTimeProvider = new FixedDateTimeProvider(new DateTime(2026, 5, 14, 9, 0, 0, DateTimeKind.Utc));

        return new SampleResultService(
            new SampleRepository(context),
            new SampleResultRepository(context),
            new LaboratoryDataFieldRepository(context),
            new LaboratoryEquipmentRepository(context),
            new EfUnitOfWork(context),
            dateTimeProvider);
    }

    private static async Task<LaboratoryFixture> SeedLaboratoryFixtureAsync(ApplicationDbContext context)
    {
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var resultDataFieldId = Guid.NewGuid();
        var equipmentId = Guid.NewGuid();
        var routedOrderId = Guid.NewGuid();
        var registeredOrderId = Guid.NewGuid();
        var routedSampleId = Guid.NewGuid();
        var registeredSampleId = Guid.NewGuid();
        var routedDocumentId = Guid.NewGuid();
        const string sampleNumber = "ZHY-2026-0001";

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
            FullName = "Петренко Петро"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "WATER",
            NameUk = "Вода",
            IsActive = true
        });

        context.DataFields.Add(new DataField
        {
            Id = resultDataFieldId,
            Key = "Result.pH",
            DisplayNameUk = "pH",
            FieldType = DataFieldType.Number,
            Scope = DataFieldScope.Result,
            Unit = "од.",
            IsRequired = true,
            IsActive = true
        });

        context.Equipment.Add(new Equipment
        {
            Id = equipmentId,
            Code = "PH-01",
            NameUk = "pH-метр",
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "LAB-TPL",
            NameUk = "Протокол",
            CurrentPublishedVersionId = versionId
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "lab.docx",
            StorageKey = "templates/lab.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        context.TemplateFields.Add(new TemplateField
        {
            TemplateVersionId = versionId,
            Tag = "Result.pH",
            NormalizedTag = "RESULT.PH",
            DataFieldId = resultDataFieldId,
            IsRequired = true
        });

        context.Orders.AddRange(
            new Order
            {
                Id = routedOrderId,
                CustomerId = customerId,
                BranchId = branchId,
                Status = OrderStatus.Registered,
                ReferralNumber = "REF-ZHY-0001"
            },
            new Order
            {
                Id = registeredOrderId,
                CustomerId = customerId,
                BranchId = branchId,
                Status = OrderStatus.Registered,
                ReferralNumber = "REF-ZHY-0002"
            });

        context.Samples.AddRange(
            new Sample
            {
                Id = routedSampleId,
                OrderId = routedOrderId,
                Number = sampleNumber,
                RegisteredAt = new DateTime(2026, 5, 14),
                InvestigationTypeId = investigationTypeId,
                Status = SampleStatus.Routed,
                RoutedAtUtc = DateTime.UtcNow
            },
            new Sample
            {
                Id = registeredSampleId,
                OrderId = registeredOrderId,
                Number = "ZHY-2026-0002",
                RegisteredAt = new DateTime(2026, 5, 14),
                InvestigationTypeId = investigationTypeId,
                Status = SampleStatus.Registered
            });

        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                Id = routedDocumentId,
                OrderId = routedOrderId,
                SampleId = routedSampleId,
                TemplateId = templateId,
                TemplateVersionId = versionId,
                TargetBranchId = branchId,
                Status = OrderDocumentStatus.SentToLab
            },
            new OrderDocument
            {
                OrderId = registeredOrderId,
                SampleId = registeredSampleId,
                TemplateId = templateId,
                TemplateVersionId = versionId,
                TargetBranchId = branchId
            });

        await context.SaveChangesAsync();

        return new LaboratoryFixture(
            routedSampleId,
            registeredSampleId,
            resultDataFieldId,
            equipmentId,
            sampleNumber);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record LaboratoryFixture(
        Guid RoutedSampleId,
        Guid RegisteredSampleId,
        Guid ResultDataFieldId,
        Guid EquipmentId,
        string SampleNumber);

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => null;
        public string? UserFullName => null;
        public Guid? BranchId => null;
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? CorrelationId => null;
        public bool IsAuthenticated => false;
    }
}
