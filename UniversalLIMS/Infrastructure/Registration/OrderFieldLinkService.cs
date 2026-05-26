using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class OrderFieldLinkService : IOrderFieldLinkService
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateFieldPermissionService _fieldPermissions;

    public OrderFieldLinkService(
        ApplicationDbContext context,
        ITemplateFieldPermissionService fieldPermissions)
    {
        _context = context;
        _fieldPermissions = fieldPermissions;
    }

    public async Task<OrderFieldMappingPrepareDto> GetMappingPrepareAsync(
        IReadOnlyList<Guid> templateVersionIds,
        CancellationToken cancellationToken = default)
    {
        if (templateVersionIds.Count < 2)
        {
            throw new InvalidOperationException("Мапінг полів доступний лише для двох і більше шаблонів.");
        }

        var distinctIds = templateVersionIds.Distinct().ToList();
        if (distinctIds.Count != templateVersionIds.Count)
        {
            throw new InvalidOperationException("Кожен шаблон можна обрати лише один раз.");
        }

        var versions = await _context.TemplateVersions
            .AsNoTracking()
            .Include(version => version.Template)
            .Include(version => version.Fields.Where(field => !field.IsAnnulled))
            .Where(version => distinctIds.Contains(version.Id))
            .ToListAsync(cancellationToken);

        if (versions.Count != distinctIds.Count)
        {
            throw new InvalidOperationException("Один або кілька обраних шаблонів не знайдено.");
        }

        var templates = new List<OrderFieldMappingTemplateDto>();

        foreach (var versionId in distinctIds)
        {
            var version = versions.First(version => version.Id == versionId);
            var accessByFieldId = await _fieldPermissions.GetFieldAccessLevelsForVersionAsync(
                version.Id,
                cancellationToken);

            var fields = version.Fields
                .OrderBy(field => field.SortOrder)
                .ThenBy(field => field.Tag)
                .Select(field =>
                {
                    var access = accessByFieldId.GetValueOrDefault(field.Id, FieldAccessLevel.None);
                    return new OrderFieldMappingFieldDto
                    {
                        TemplateFieldId = field.Id,
                        Tag = field.Tag,
                        Title = field.Title,
                        CanRead = access >= FieldAccessLevel.Read,
                        CanWrite = access >= FieldAccessLevel.Write
                    };
                })
                .Where(field => field.CanRead)
                .ToList();

            templates.Add(new OrderFieldMappingTemplateDto
            {
                TemplateVersionId = version.Id,
                TemplateNameUk = version.Template.NameUk,
                VersionNumber = version.VersionNumber,
                Fields = fields
            });
        }

        return new OrderFieldMappingPrepareDto { Templates = templates };
    }

    public async Task SaveFieldLinkGroupsAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldLinkGroupInput> groups,
        CancellationToken cancellationToken = default)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var orderExists = await _context.Orders.AnyAsync(order => order.Id == orderId, cancellationToken);
        if (!orderExists)
        {
            throw new InvalidOperationException("Замовлення не знайдено.");
        }

        ValidateGroups(groups);

        var sortOrder = 0;
        foreach (var groupInput in groups)
        {
            if (groupInput.Members.Count < 2)
            {
                continue;
            }

            var group = new OrderFieldLinkGroup
            {
                OrderId = orderId,
                Label = string.IsNullOrWhiteSpace(groupInput.Label) ? null : groupInput.Label.Trim(),
                SortOrder = sortOrder++
            };

            foreach (var memberInput in groupInput.Members)
            {
                group.Members.Add(new OrderFieldLinkMember
                {
                    TemplateVersionId = memberInput.TemplateVersionId,
                    TemplateFieldId = memberInput.TemplateFieldId
                });
            }

            _context.OrderFieldLinkGroups.Add(group);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplySharedFieldValuesAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldLinkGroupInput> groups,
        IReadOnlyList<OrderSharedFieldValueInput> sharedValues,
        CancellationToken cancellationToken = default)
    {
        if (sharedValues.Count == 0 || groups.Count == 0)
        {
            return;
        }

        var valuesByGroupIndex = sharedValues
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.GroupIndex, item => item.Value!.Trim());

        if (valuesByGroupIndex.Count == 0)
        {
            return;
        }

        var allMemberInputs = new List<OrderFieldLinkMemberInput>();
        for (var index = 0; index < groups.Count; index++)
        {
            if (!valuesByGroupIndex.ContainsKey(index) || groups[index].Members.Count < 2)
            {
                continue;
            }

            allMemberInputs.AddRange(groups[index].Members);
        }

        if (allMemberInputs.Count == 0)
        {
            return;
        }

        var fieldIds = allMemberInputs.Select(member => member.TemplateFieldId).Distinct().ToList();
        var templateFields = await _context.TemplateFields
            .Where(field => fieldIds.Contains(field.Id) && !field.IsAnnulled)
            .ToListAsync(cancellationToken);

        if (templateFields.Count == 0)
        {
            return;
        }

        var storageIdsByFieldId = await TemplateFieldStorageDataFieldResolver.ResolveOrCreateStorageDataFieldIdsAsync(
            _context,
            templateFields,
            cancellationToken);

        var existingValues = await _context.OrderFieldValues
            .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == null)
            .ToListAsync(cancellationToken);

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            if (!valuesByGroupIndex.TryGetValue(groupIndex, out var trimmedValue))
            {
                continue;
            }

            var group = groups[groupIndex];
            if (group.Members.Count < 2)
            {
                continue;
            }

            var targetDataFieldIds = new HashSet<Guid>();
            foreach (var member in group.Members)
            {
                if (!storageIdsByFieldId.TryGetValue(member.TemplateFieldId, out var dataFieldId))
                {
                    continue;
                }

                targetDataFieldIds.Add(dataFieldId);
            }

            foreach (var dataFieldId in targetDataFieldIds)
            {
                var stored = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
                if (stored is null)
                {
                    var created = new OrderFieldValue
                    {
                        OrderId = orderId,
                        SampleId = null,
                        DataFieldId = dataFieldId,
                        StoredValue = trimmedValue
                    };
                    _context.OrderFieldValues.Add(created);
                    existingValues.Add(created);
                }
                else
                {
                    stored.StoredValue = trimmedValue;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateGroups(IReadOnlyList<OrderFieldLinkGroupInput> groups)
    {
        var usedFieldIds = new HashSet<Guid>();

        foreach (var group in groups)
        {
            foreach (var member in group.Members)
            {
                if (member.TemplateFieldId == Guid.Empty || member.TemplateVersionId == Guid.Empty)
                {
                    throw new InvalidOperationException("Некоректний ідентифікатор поля в групі мапінгу.");
                }

                if (!usedFieldIds.Add(member.TemplateFieldId))
                {
                    throw new InvalidOperationException(
                        "Одне поле не може входити в кілька груп мапінгу для цього замовлення.");
                }
            }
        }
    }
}
