using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Pilot;

/// <summary>
/// D-контент-4 — 1 клієнт + 1 REF + 4 протоколи (4 проби) + 3 групи спільних полів.
/// Per Sample: REF лише на першій пробі; протоколи на всіх чотирьох (як пакетне замовлення).
/// </summary>
public sealed class PilotRefDContent4Tests
{
    private static readonly DateTime PilotUtc = new(2026, 5, 31, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DContent4_FourSamples_ThreeSharedFieldLinkGroups_ApplyValuesOnEightDocuments()
    {
        var regBranchId = Guid.NewGuid();
        var labBranchId = Guid.NewGuid();

        await using var context = CreateContext();
        await SeedBranchesAsync(context, regBranchId, labBranchId);
        var (waterTypeId, foodTypeId, airTypeId, soilTypeId) = await SeedInvestigationTypesAsync(context);

        var referral = await SeedReferralWithFieldAsync(context, "REF_SamplingDate");
        var protocolWater = await SeedProtocolWithFieldAsync(context, "F327", "f327_SamplingDate");
        var protocolFood = await SeedProtocolWithSecondFieldAsync(
            context,
            "F343",
            "f343_SamplingDate",
            "f343_SamplingLocation");
        var protocolAir = await SeedProtocolWithFieldAsync(context, "F205", "f205_SamplingDate");
        var protocolSoil = await SeedProtocolWithFieldAsync(context, "F332", "f332_SamplingLocation");

        await LinkProtocolToInvestigationTypeAsync(context, waterTypeId, protocolWater.TemplateId);
        await LinkProtocolToInvestigationTypeAsync(context, foodTypeId, protocolFood.TemplateId);
        await LinkProtocolToInvestigationTypeAsync(context, airTypeId, protocolAir.TemplateId);
        await LinkProtocolToInvestigationTypeAsync(context, soilTypeId, protocolSoil.TemplateId);

        var customer = await SeedCustomerAsync(context);
        var registration = CreateRegistrationService(context, regBranchId);

        var created = await registration.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Samples =
            [
                Sample(waterTypeId, referral.VersionId, protocolWater.VersionId, labBranchId),
                ProtocolOnlySample(foodTypeId, protocolFood.VersionId, labBranchId),
                ProtocolOnlySample(airTypeId, protocolAir.VersionId, labBranchId),
                ProtocolOnlySample(soilTypeId, protocolSoil.VersionId, labBranchId)
            ]
        });

        Assert.Equal(4, created.Samples.Count);
        Assert.Equal(5, created.Documents.Count);

        var mappingSamples = new List<OrderCreateSampleInput>
        {
            MappingSample(waterTypeId, referral.VersionId, protocolWater.VersionId),
            MappingSample(foodTypeId, null, protocolFood.VersionId),
            MappingSample(airTypeId, null, protocolAir.VersionId),
            MappingSample(soilTypeId, null, protocolSoil.VersionId)
        };

        var mapping = new OrderFieldMappingPrepareDto
        {
            Templates =
            [
                MappingTemplate(referral, "REF-MOZ-001"),
                MappingTemplate(protocolWater, "F327"),
                MappingTemplate(protocolFood.PrimaryField, "F343"),
                MappingTemplate(protocolAir, "F205"),
                MappingTemplate(protocolSoil, "F332")
            ]
        };

        var form = new OrderCreateFormDto
        {
            InvestigationTypes =
            [
                TypeOption(waterTypeId, "WATER", "Вода"),
                TypeOption(foodTypeId, "FOOD", "Харчові"),
                TypeOption(airTypeId, "AIR", "Повітря"),
                TypeOption(soilTypeId, "SOIL", "Ґрунт")
            ],
            TemplateOptions = [],
            ReferralTemplateOptions = [],
            Branches = []
        };

        var groups = OrderFieldMappingSampleGroupBuilder.Build(mappingSamples, form, mapping);
        Assert.Equal(4, groups.Count);
        Assert.Equal(2, groups[0].Templates.Count);
        Assert.All(groups.Skip(1), group => Assert.Single(group.Templates));

        var linkService = new OrderFieldLinkService(context, new AllowAllTemplateFieldPermissionService(context));
        var linkGroups = new List<OrderFieldLinkGroupInput>
        {
            LinkGroup("Дата відбору (REF + вода)", referral, protocolWater),
            LinkGroup("Дата відбору (харчі + повітря)", protocolFood.PrimaryField, protocolAir),
            LinkGroup("Місце (харчі + ґрунт)", protocolFood.LocationField, protocolSoil)
        };

        await linkService.SaveFieldLinkGroupsAsync(created.OrderId, linkGroups);
        await linkService.ApplySharedFieldValuesAsync(
            created.OrderId,
            linkGroups,
            [
                new OrderSharedFieldValueInput { GroupIndex = 0, Value = "2026-05-31" },
                new OrderSharedFieldValueInput { GroupIndex = 1, Value = "2026-05-31" },
                new OrderSharedFieldValueInput { GroupIndex = 2, Value = "2026-05-31" }
            ]);

        var saved = await linkService.GetFieldLinkGroupsForOrderAsync(created.OrderId);
        Assert.Equal(3, saved.Groups.Count);
        Assert.All(saved.Groups, group => Assert.Equal(2, group.Members.Count));
        Assert.All(saved.Groups, group => Assert.Equal("2026-05-31", group.SharedValue));

        var fieldValues = await context.OrderFieldValues
            .Where(value => value.OrderId == created.OrderId)
            .ToListAsync();

        Assert.Equal(6, fieldValues.Count);
        Assert.Equal(6, fieldValues.Select(value => value.DataFieldId).Distinct().Count());

        var referralDocuments = await context.OrderDocuments
            .CountAsync(document =>
                document.OrderId == created.OrderId
                && !document.IsAnnulled
                && document.TemplateVersionId == referral.VersionId);
        Assert.Equal(1, referralDocuments);
    }

    private static CreateOrderSampleRequest ProtocolOnlySample(
        Guid investigationTypeId,
        Guid protocolVersionId,
        Guid labBranchId) =>
        new()
        {
            InvestigationTypeId = investigationTypeId,
            Documents =
            [
                new CreateOrderDocumentRequest
                {
                    TemplateVersionId = protocolVersionId,
                    TargetBranchId = labBranchId
                }
            ]
        };

    private static CreateOrderSampleRequest Sample(
        Guid investigationTypeId,
        Guid referralVersionId,
        Guid protocolVersionId,
        Guid labBranchId) =>
        new()
        {
            InvestigationTypeId = investigationTypeId,
            ReferralTemplateVersionId = referralVersionId,
            Documents =
            [
                new CreateOrderDocumentRequest
                {
                    TemplateVersionId = protocolVersionId,
                    TargetBranchId = labBranchId
                }
            ]
        };

    private static OrderCreateSampleInput MappingSample(
        Guid investigationTypeId,
        Guid? referralVersionId,
        Guid protocolVersionId) =>
        new()
        {
            InvestigationTypeId = investigationTypeId,
            ReferralTemplateVersionId = referralVersionId,
            SelectedTemplateVersionIds = [protocolVersionId]
        };

    private static InvestigationTypeOptionDto TypeOption(Guid id, string code, string nameUk) =>
        new() { Id = id, Code = code, NameUk = nameUk };

    private static OrderFieldLinkGroupInput LinkGroup(
        string label,
        SeededTemplateField first,
        SeededTemplateField second) =>
        new()
        {
            Label = label,
            Members =
            [
                Member(first.VersionId, first.FieldId),
                Member(second.VersionId, second.FieldId)
            ]
        };

    private static OrderFieldLinkMemberInput Member(Guid versionId, Guid fieldId) =>
        new() { TemplateVersionId = versionId, TemplateFieldId = fieldId };

    private static OrderFieldMappingTemplateDto MappingTemplate(SeededTemplateField seeded, string nameUk) =>
        new()
        {
            TemplateVersionId = seeded.VersionId,
            TemplateNameUk = nameUk,
            VersionNumber = 1,
            Fields =
            [
                new OrderFieldMappingFieldDto
                {
                    TemplateFieldId = seeded.FieldId,
                    Tag = seeded.Tag,
                    Title = seeded.Tag,
                    CanRead = true,
                    CanWrite = true
                }
            ]
        };

    private static OrderRegistrationService CreateRegistrationService(
        ApplicationDbContext context,
        Guid branchId) =>
        new(
            context,
            new BranchCurrentUser(branchId),
            new CustomerService(context),
            new NumberingService(context, new FixedDateTimeProvider(PilotUtc)),
            new FixedDateTimeProvider(PilotUtc));

    private static async Task<SeededTemplateField> SeedReferralWithFieldAsync(
        ApplicationDbContext context,
        string tag) =>
        await SeedTemplateWithFieldAsync(context, "REF-MOZ-001", "Направлення МОЗ", TemplatePurpose.Referral, tag);

    private static async Task<SeededProtocolTemplate> SeedProtocolWithSecondFieldAsync(
        ApplicationDbContext context,
        string code,
        string primaryTag,
        string secondaryTag)
    {
        var primaryDataFieldId = Guid.NewGuid();
        var secondaryDataFieldId = Guid.NewGuid();
        var primaryFieldId = Guid.NewGuid();
        var secondaryFieldId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.DataFields.AddRange(
            new DataField
            {
                Id = primaryDataFieldId,
                Key = primaryTag,
                DisplayNameUk = primaryTag,
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            },
            new DataField
            {
                Id = secondaryDataFieldId,
                Key = secondaryTag,
                DisplayNameUk = secondaryTag,
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Sample,
                IsActive = true
            });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = code,
            NameUk = code,
            Status = TemplateStatus.Active,
            Purpose = TemplatePurpose.Protocol
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = $"{code}.pdf",
            Fields =
            [
                CreateTemplateField(primaryFieldId, versionId, primaryTag, primaryDataFieldId, 1),
                CreateTemplateField(secondaryFieldId, versionId, secondaryTag, secondaryDataFieldId, 2)
            ]
        });

        var template = await context.Templates.FindAsync(templateId);
        template!.CurrentPublishedVersionId = versionId;
        await context.SaveChangesAsync();

        var primary = new SeededTemplateField(versionId, templateId, primaryFieldId, primaryDataFieldId, primaryTag);
        var location = new SeededTemplateField(versionId, templateId, secondaryFieldId, secondaryDataFieldId, secondaryTag);
        return new SeededProtocolTemplate(versionId, templateId, primary, location);
    }

    private static TemplateField CreateTemplateField(
        Guid fieldId,
        Guid versionId,
        string tag,
        Guid dataFieldId,
        int sortOrder) =>
        new()
        {
            Id = fieldId,
            TemplateVersionId = versionId,
            Tag = tag,
            DataFieldId = dataFieldId,
            SortOrder = sortOrder,
            Segments =
            [
                new TemplateFieldSegment
                {
                    Id = Guid.NewGuid(),
                    Sequence = 1,
                    PageNumber = 1,
                    PositionX = sortOrder,
                    PositionY = sortOrder,
                    Width = 10,
                    Height = 10,
                    IsPrimary = true
                }
            ]
        };

    private static async Task<SeededTemplateField> SeedProtocolWithFieldAsync(
        ApplicationDbContext context,
        string code,
        string tag,
        string? displayNameUk = null) =>
        await SeedTemplateWithFieldAsync(
            context,
            code,
            code,
            TemplatePurpose.Protocol,
            tag,
            displayNameUk);

    private static async Task<SeededTemplateField> SeedTemplateWithFieldAsync(
        ApplicationDbContext context,
        string code,
        string nameUk,
        TemplatePurpose purpose,
        string tag,
        string? displayNameUk = null)
    {
        var dataFieldId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var titleUk = displayNameUk ?? tag;

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = tag,
            DisplayNameUk = titleUk,
            FieldType = DataFieldType.Date,
            Scope = DataFieldScope.Sample,
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = code,
            NameUk = nameUk,
            Status = TemplateStatus.Active,
            Purpose = purpose
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = $"{code}.pdf",
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = tag,
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 1,
                            PositionY = 1,
                            Width = 10,
                            Height = 10,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        var template = await context.Templates.FindAsync(templateId);
        template!.CurrentPublishedVersionId = versionId;
        await context.SaveChangesAsync();

        return new SeededTemplateField(versionId, templateId, fieldId, dataFieldId, tag);
    }

    private sealed record SeededTemplateField(
        Guid VersionId,
        Guid TemplateId,
        Guid FieldId,
        Guid DataFieldId,
        string Tag);

    private sealed record SeededProtocolTemplate(
        Guid VersionId,
        Guid TemplateId,
        SeededTemplateField PrimaryField,
        SeededTemplateField LocationField);

    private static async Task LinkProtocolToInvestigationTypeAsync(
        ApplicationDbContext context,
        Guid investigationTypeId,
        Guid templateId)
    {
        context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
        {
            InvestigationTypeId = investigationTypeId,
            TemplateId = templateId,
            SortOrder = 1
        });
        await context.SaveChangesAsync();
    }

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

    private static async Task<(Guid Water, Guid Food, Guid Air, Guid Soil)> SeedInvestigationTypesAsync(
        ApplicationDbContext context)
    {
        var water = new InvestigationType { Code = "WATER", NameUk = "Вода", SortOrder = 1 };
        var food = new InvestigationType { Code = "FOOD", NameUk = "Харчові", SortOrder = 2 };
        var air = new InvestigationType { Code = "AIR", NameUk = "Повітря", SortOrder = 3 };
        var soil = new InvestigationType { Code = "SOIL", NameUk = "Ґрунт", SortOrder = 4 };
        context.InvestigationTypes.AddRange(water, food, air, soil);
        await context.SaveChangesAsync();
        return (water.Id, food.Id, air.Id, soil.Id);
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context)
    {
        var customer = new Customer
        {
            FullName = "Клієнт D-контент-4",
            Kind = CustomerKind.Individual
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private sealed class BranchCurrentUser(Guid branchId) : ICurrentUserService
    {
        public string? UserId => "registrar-d4";

        public string? UserName => "registrar";

        public string? UserFullName => "Registrar";

        public Guid? BranchId => branchId;

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;

        public bool IsAuthenticated => true;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class AllowAllTemplateFieldPermissionService(ApplicationDbContext context)
        : ITemplateFieldPermissionService
    {
        public async Task<IReadOnlyDictionary<Guid, FieldAccessLevel>> GetFieldAccessLevelsForVersionAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default)
        {
            var fieldIds = await context.TemplateFields
                .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
                .Select(field => field.Id)
                .ToListAsync(cancellationToken);

            return fieldIds.ToDictionary(id => id, _ => FieldAccessLevel.Write);
        }
    }
}
