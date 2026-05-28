using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class OrderFieldLinkServiceTests
{
    [Fact]
    public async Task GetMappingPrepareAsync_UsesWriteFallback_WhenNoFieldPermissionsConfigured()
    {
        await using var context = CreateContext();
        var versionA = Guid.NewGuid();
        var versionB = Guid.NewGuid();
        var fieldA = Guid.NewGuid();
        var fieldB = Guid.NewGuid();
        var dataFieldA = Guid.NewGuid();
        var dataFieldB = Guid.NewGuid();

        context.DataFields.AddRange(
            new DataField
            {
                Id = dataFieldA,
                Key = "f327_pH",
                DisplayNameUk = "pH",
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Sample,
                IsActive = true
            },
            new DataField
            {
                Id = dataFieldB,
                Key = "Food_pH",
                DisplayNameUk = "pH",
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Sample,
                IsActive = true
            });

        SeedVersionWithField(context, versionA, "T-A", fieldA, "f327_pH", dataFieldA);
        SeedVersionWithField(context, versionB, "T-B", fieldB, "Food_pH", dataFieldB);
        await context.SaveChangesAsync();

        var service = new OrderFieldLinkService(context, new EmptyTemplateFieldPermissionService());

        var result = await service.GetMappingPrepareAsync([versionA, versionB]);

        Assert.Equal(2, result.Templates.Count);
        Assert.All(result.Templates, template =>
        {
            Assert.NotEmpty(template.Fields);
            Assert.All(template.Fields, field =>
            {
                Assert.True(field.CanRead);
                Assert.True(field.CanWrite);
            });
        });
    }

    [Fact]
    public async Task ApplySharedFieldValuesAsync_WritesToAllDataFieldsInGroup()
    {
        await using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var versionA = Guid.NewGuid();
        var versionB = Guid.NewGuid();
        var fieldA = Guid.NewGuid();
        var fieldB = Guid.NewGuid();
        var dataFieldA = Guid.NewGuid();
        var dataFieldB = Guid.NewGuid();

        SeedMinimalOrder(context, orderId);

        context.DataFields.AddRange(
            new DataField
            {
                Id = dataFieldA,
                Key = "f327_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            },
            new DataField
            {
                Id = dataFieldB,
                Key = "Food_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            });

        SeedVersionWithField(context, versionA, "T-A", fieldA, "f327_SamplingDate", dataFieldA);
        SeedVersionWithField(context, versionB, "T-B", fieldB, "Food_SamplingDate", dataFieldB);
        await context.SaveChangesAsync();

        var service = new OrderFieldLinkService(context, new AllowAllTemplateFieldPermissionService(context));

        var groups = new List<OrderFieldLinkGroupInput>
        {
            new()
            {
                Label = "Дата відбору",
                Members =
                [
                    new OrderFieldLinkMemberInput { TemplateVersionId = versionA, TemplateFieldId = fieldA },
                    new OrderFieldLinkMemberInput { TemplateVersionId = versionB, TemplateFieldId = fieldB }
                ]
            }
        };

        await service.ApplySharedFieldValuesAsync(
            orderId,
            groups,
            [new OrderSharedFieldValueInput { GroupIndex = 0, Value = "2026-05-26" }]);

        var values = await context.OrderFieldValues
            .Where(fieldValue => fieldValue.OrderId == orderId)
            .ToListAsync();

        Assert.Equal(2, values.Count);
        Assert.Contains(values, fieldValue => fieldValue.DataFieldId == dataFieldA && fieldValue.StoredValue == "2026-05-26");
        Assert.Contains(values, fieldValue => fieldValue.DataFieldId == dataFieldB && fieldValue.StoredValue == "2026-05-26");
    }

    [Fact]
    public async Task GetFieldLinkGroupsForOrderAsync_ReturnsSavedGroupsWithSharedValue()
    {
        await using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var versionA = Guid.NewGuid();
        var versionB = Guid.NewGuid();
        var fieldA = Guid.NewGuid();
        var fieldB = Guid.NewGuid();
        var dataFieldA = Guid.NewGuid();
        var dataFieldB = Guid.NewGuid();

        SeedMinimalOrder(context, orderId);

        context.DataFields.AddRange(
            new DataField
            {
                Id = dataFieldA,
                Key = "f327_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            },
            new DataField
            {
                Id = dataFieldB,
                Key = "Food_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            });

        SeedVersionWithField(context, versionA, "T-A", fieldA, "f327_SamplingDate", dataFieldA);
        SeedVersionWithField(context, versionB, "T-B", fieldB, "Food_SamplingDate", dataFieldB);
        await context.SaveChangesAsync();

        var service = new OrderFieldLinkService(context, new AllowAllTemplateFieldPermissionService(context));

        var groups = new List<OrderFieldLinkGroupInput>
        {
            new()
            {
                Label = "Дата відбору",
                Members =
                [
                    new OrderFieldLinkMemberInput { TemplateVersionId = versionA, TemplateFieldId = fieldA },
                    new OrderFieldLinkMemberInput { TemplateVersionId = versionB, TemplateFieldId = fieldB }
                ]
            }
        };

        await service.SaveFieldLinkGroupsAsync(orderId, groups);
        await service.ApplySharedFieldValuesAsync(
            orderId,
            groups,
            [new OrderSharedFieldValueInput { GroupIndex = 0, Value = "2026-05-26" }]);

        var detail = await service.GetFieldLinkGroupsForOrderAsync(orderId);

        Assert.Single(detail.Groups);
        Assert.Equal("Дата відбору", detail.Groups[0].Label);
        Assert.Equal("2026-05-26", detail.Groups[0].SharedValue);
        Assert.Equal(2, detail.Groups[0].Members.Count);
        Assert.Contains(detail.Groups[0].Members, member => member.Tag == "f327_SamplingDate");
        Assert.Contains(detail.Groups[0].Members, member => member.Tag == "Food_SamplingDate");
    }

    [Fact]
    public async Task AdaptFieldLinkGroupsFromOrderAsync_MatchesFieldsByTagOnNewTemplateVersions()
    {
        await using var context = CreateContext();
        var orderId = Guid.NewGuid();
        var sourceVersionA = Guid.NewGuid();
        var sourceVersionB = Guid.NewGuid();
        var targetVersionA = Guid.NewGuid();
        var targetVersionB = Guid.NewGuid();
        var sourceFieldA = Guid.NewGuid();
        var sourceFieldB = Guid.NewGuid();
        var targetFieldA = Guid.NewGuid();
        var targetFieldB = Guid.NewGuid();
        var dataFieldA = Guid.NewGuid();
        var dataFieldB = Guid.NewGuid();

        SeedMinimalOrder(context, orderId);

        context.DataFields.AddRange(
            new DataField
            {
                Id = dataFieldA,
                Key = "f327_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            },
            new DataField
            {
                Id = dataFieldB,
                Key = "Food_SamplingDate",
                DisplayNameUk = "Дата",
                FieldType = DataFieldType.Date,
                Scope = DataFieldScope.Sample,
                IsActive = true
            });

        SeedVersionWithField(context, sourceVersionA, "T-A", sourceFieldA, "f327_SamplingDate", dataFieldA);
        SeedVersionWithField(context, sourceVersionB, "T-B", sourceFieldB, "Food_SamplingDate", dataFieldB);
        SeedVersionWithField(context, targetVersionA, "T-A2", targetFieldA, "f327_SamplingDate", dataFieldA);
        SeedVersionWithField(context, targetVersionB, "T-B2", targetFieldB, "Food_SamplingDate", dataFieldB);
        await context.SaveChangesAsync();

        var service = new OrderFieldLinkService(context, new AllowAllTemplateFieldPermissionService(context));

        var groups = new List<OrderFieldLinkGroupInput>
        {
            new()
            {
                Label = "Дата відбору",
                Members =
                [
                    new OrderFieldLinkMemberInput { TemplateVersionId = sourceVersionA, TemplateFieldId = sourceFieldA },
                    new OrderFieldLinkMemberInput { TemplateVersionId = sourceVersionB, TemplateFieldId = sourceFieldB }
                ]
            }
        };

        await service.SaveFieldLinkGroupsAsync(orderId, groups);
        await service.ApplySharedFieldValuesAsync(
            orderId,
            groups,
            [new OrderSharedFieldValueInput { GroupIndex = 0, Value = "2026-05-26" }]);

        var adapted = await service.AdaptFieldLinkGroupsFromOrderAsync(
            orderId,
            [targetVersionA, targetVersionB]);

        Assert.Single(adapted.Groups);
        Assert.Equal(2, adapted.Groups[0].Members.Count);
        Assert.Contains(adapted.Groups[0].Members, member => member.TemplateFieldId == targetFieldA);
        Assert.Contains(adapted.Groups[0].Members, member => member.TemplateFieldId == targetFieldB);
        Assert.Single(adapted.SharedValues);
        Assert.Equal("2026-05-26", adapted.SharedValues[0].Value);
    }

    private static void SeedMinimalOrder(ApplicationDbContext context, Guid orderId)
    {
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR",
            Name = "Branch",
            City = "City",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Test"
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Registered,
            ReferralNumber = "REF-1"
        });
    }

    private static void SeedVersionWithField(
        ApplicationDbContext context,
        Guid versionId,
        string templateCode,
        Guid fieldId,
        string tag,
        Guid dataFieldId)
    {
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = templateCode,
            NameUk = templateCode,
            Status = TemplateStatus.Active
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "t.pdf",
            StorageKey = "t.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow,
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
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class AllowAllTemplateFieldPermissionService : ITemplateFieldPermissionService
    {
        private readonly ApplicationDbContext _context;

        public AllowAllTemplateFieldPermissionService(ApplicationDbContext context) => _context = context;

        public async Task<IReadOnlyDictionary<Guid, FieldAccessLevel>> GetFieldAccessLevelsForVersionAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default)
        {
            var fieldIds = await _context.TemplateFields
                .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
                .Select(field => field.Id)
                .ToListAsync(cancellationToken);

            return fieldIds.ToDictionary(id => id, _ => FieldAccessLevel.Write);
        }
    }

    private sealed class EmptyTemplateFieldPermissionService : ITemplateFieldPermissionService
    {
        public Task<IReadOnlyDictionary<Guid, FieldAccessLevel>> GetFieldAccessLevelsForVersionAsync(
            Guid templateVersionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, FieldAccessLevel>>(new Dictionary<Guid, FieldAccessLevel>());
    }
}
