using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Pilot;

/// <summary>
/// D8a — Per Sample: 1 клієнт + 3 проби × (REF-MOZ-001 + протокол); REF не йде в lab.
/// UI Map/Fill — docs/pilot-d8a-qa-checklist.md.
/// </summary>
public sealed class PilotRefPerSampleFlowTests
{
    private static readonly DateTime PilotUtc = new(2026, 5, 31, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task D8a_ThreeSamples_ReferralStaysAtRegistrar_OnlyProtocolsRoutableToLab()
    {
        var regBranchId = Guid.NewGuid();
        var labBranchId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedBranchesAsync(context, regBranchId, labBranchId);
        var (waterTypeId, foodTypeId, airTypeId) = await SeedInvestigationTypesAsync(context);
        var referralVersionId = await SeedReferralTemplateAsync(context);
        var protocolWaterId = await SeedProtocolTemplateAsync(context, "F327", "Ф327 вода");
        var protocolFoodId = await SeedProtocolTemplateAsync(context, "F343", "Ф343 харчі");
        var protocolAirId = await SeedProtocolTemplateAsync(context, "F205", "Ф205 повітря");
        var customer = await SeedCustomerAsync(context);

        var registration = CreateRegistrationService(context, regBranchId);
        var created = await registration.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Samples =
            [
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = waterTypeId,
                    ReferralTemplateVersionId = referralVersionId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = protocolWaterId,
                            TargetBranchId = labBranchId
                        }
                    ]
                },
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = foodTypeId,
                    ReferralTemplateVersionId = referralVersionId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = protocolFoodId,
                            TargetBranchId = labBranchId
                        }
                    ]
                },
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = airTypeId,
                    ReferralTemplateVersionId = referralVersionId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = protocolAirId,
                            TargetBranchId = labBranchId
                        }
                    ]
                }
            ]
        });

        Assert.Equal(3, created.Samples.Count);
        Assert.Equal(6, created.Documents.Count);

        var detail = await registration.GetOrderDetailAsync(created.OrderId);
        Assert.NotNull(detail);
        Assert.Equal(6, detail!.Documents.Count);

        var referralDocs = detail.Documents
            .Where(document => document.TemplatePurpose == TemplatePurpose.Referral)
            .ToList();
        var protocolDocs = detail.Documents
            .Where(document => document.TemplatePurpose == TemplatePurpose.Protocol)
            .ToList();

        Assert.Equal(3, referralDocs.Count);
        Assert.Equal(3, protocolDocs.Count);
        Assert.All(referralDocs, document =>
        {
            Assert.Equal(regBranchId, document.TargetBranchId);
            Assert.False(document.CanSendToLab);
        });
        Assert.All(protocolDocs, document =>
        {
            Assert.Equal(labBranchId, document.TargetBranchId);
            Assert.True(document.CanSendToLab);
        });

        await registration.SendDocumentsToLabAsync(new SendOrderDocumentsRequest
        {
            OrderId = created.OrderId,
            OrderDocumentIds = protocolDocs.Select(document => document.OrderDocumentId).ToList()
        });

        var documentsAfterSend = await context.OrderDocuments
            .Where(document => document.OrderId == created.OrderId && !document.IsAnnulled)
            .ToListAsync();

        Assert.Equal(3, documentsAfterSend.Count(document => document.Status == OrderDocumentStatus.SentToLab));
        Assert.Equal(3, documentsAfterSend.Count(document => document.Status == OrderDocumentStatus.Pending));

        var labJournal = new LaboratoryJournalService(context, new FixedLaboratoryBranchContext(labBranchId));
        var incoming = await labJournal.GetIncomingSinceAsync(PilotUtc.AddMinutes(-5));
        Assert.Equal(3, incoming.Count);
        Assert.Equal(3, incoming.Select(item => item.SampleNumber).Distinct().Count());

        var mappingSamples = new List<OrderCreateSampleInput>
        {
            new()
            {
                InvestigationTypeId = waterTypeId,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolWaterId]
            },
            new()
            {
                InvestigationTypeId = foodTypeId,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolFoodId]
            },
            new()
            {
                InvestigationTypeId = airTypeId,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolAirId]
            }
        };

        var mapping = new OrderFieldMappingPrepareDto
        {
            Templates =
            [
                CreateMappingTemplate(referralVersionId, "REF-MOZ-001", "REF_SamplingDate"),
                CreateMappingTemplate(protocolWaterId, "F327", "f327_SamplingDate"),
                CreateMappingTemplate(protocolFoodId, "F343", "f343_SamplingDate"),
                CreateMappingTemplate(protocolAirId, "F205", "f205_SamplingDate")
            ]
        };

        var form = new OrderCreateFormDto
        {
            InvestigationTypes =
            [
                new InvestigationTypeOptionDto { Id = waterTypeId, Code = "WATER", NameUk = "Вода" },
                new InvestigationTypeOptionDto { Id = foodTypeId, Code = "FOOD", NameUk = "Харчові" },
                new InvestigationTypeOptionDto { Id = airTypeId, Code = "AIR", NameUk = "Повітря" }
            ],
            TemplateOptions = [],
            ReferralTemplateOptions = [],
            Branches = []
        };

        var groups = OrderFieldMappingSampleGroupBuilder.Build(mappingSamples, form, mapping);
        Assert.Equal(3, groups.Count);
        Assert.All(groups, group => Assert.Equal(2, group.Templates.Count));
        Assert.Contains(groups, group => group.Label.StartsWith("Проба 1", StringComparison.Ordinal));
        Assert.Contains(groups, group => group.Label.StartsWith("Проба 2", StringComparison.Ordinal));
        Assert.Contains(groups, group => group.Label.StartsWith("Проба 3", StringComparison.Ordinal));
    }

    private static OrderRegistrationService CreateRegistrationService(
        ApplicationDbContext context,
        Guid branchId) =>
        new(
            context,
            new BranchCurrentUser(branchId),
            new CustomerService(context),
            new NumberingService(context, new FixedDateTimeProvider(PilotUtc)),
            new FixedDateTimeProvider(PilotUtc));

    private static OrderFieldMappingTemplateDto CreateMappingTemplate(
        Guid templateVersionId,
        string nameUk,
        string tag) =>
        new()
        {
            TemplateVersionId = templateVersionId,
            TemplateNameUk = nameUk,
            VersionNumber = 1,
            Fields =
            [
                new OrderFieldMappingFieldDto
                {
                    TemplateFieldId = Guid.NewGuid(),
                    Tag = tag,
                    Title = tag,
                    CanRead = true,
                    CanWrite = true
                }
            ]
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedBranchesAsync(ApplicationDbContext context, params Guid[] branchIds)
    {
        foreach (var branchId in branchIds)
        {
            context.Branches.Add(new Branch
            {
                Id = branchId,
                Code = branchId.ToString("N")[..6],
                Name = $"Філія {branchId.ToString("N")[..4]}",
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<(Guid Water, Guid Food, Guid Air)> SeedInvestigationTypesAsync(
        ApplicationDbContext context)
    {
        var water = new InvestigationType { Code = "WATER", NameUk = "Вода", SortOrder = 1 };
        var food = new InvestigationType { Code = "FOOD", NameUk = "Харчові", SortOrder = 2 };
        var air = new InvestigationType { Code = "AIR", NameUk = "Повітря", SortOrder = 3 };
        context.InvestigationTypes.AddRange(water, food, air);
        await context.SaveChangesAsync();
        return (water.Id, food.Id, air.Id);
    }

    private static async Task<Guid> SeedReferralTemplateAsync(ApplicationDbContext context) =>
        await SeedTemplateAsync(context, "REF-MOZ-001", "Направлення МОЗ", TemplatePurpose.Referral);

    private static async Task<Guid> SeedProtocolTemplateAsync(
        ApplicationDbContext context,
        string code,
        string nameUk) =>
        await SeedTemplateAsync(context, code, nameUk, TemplatePurpose.Protocol);

    private static async Task<Guid> SeedTemplateAsync(
        ApplicationDbContext context,
        string code,
        string nameUk,
        TemplatePurpose purpose)
    {
        var template = new Template
        {
            Code = code,
            NameUk = nameUk,
            Status = TemplateStatus.Active,
            Purpose = purpose
        };
        context.Templates.Add(template);

        var version = new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = $"{code}.pdf"
        };
        context.TemplateVersions.Add(version);
        template.CurrentPublishedVersionId = version.Id;
        await context.SaveChangesAsync();
        return version.Id;
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context)
    {
        var customer = new Customer
        {
            FullName = "Клієнт D8a",
            Kind = CustomerKind.Individual
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private sealed class BranchCurrentUser(Guid branchId) : ICurrentUserService
    {
        public string? UserId => "registrar-d8a";

        public string? UserName => "registrar";

        public string? UserFullName => "Registrar D8a";

        public Guid? BranchId => branchId;

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedLaboratoryBranchContext(Guid? branchId) : ILaboratoryBranchContext
    {
        public Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaboratoryBranchContextState { ActiveBranchId = branchId });

        public Task SetSelectedBranchAsync(Guid? selectedBranchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
