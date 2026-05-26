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
}
