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
            var useDefaultWriteAccess = accessByFieldId.Count == 0;

            var fields = version.Fields
                .OrderBy(field => field.SortOrder)
                .ThenBy(field => field.Tag)
                .Select(field =>
                {
                    var access = useDefaultWriteAccess
                        ? FieldAccessLevel.Write
                        : accessByFieldId.GetValueOrDefault(field.Id, FieldAccessLevel.None);
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

    public async Task<OrderFieldLinkGroupsDetailDto> GetFieldLinkGroupsForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var groups = await _context.OrderFieldLinkGroups
            .AsNoTracking()
            .Where(group => group.OrderId == orderId)
            .Include(group => group.Members)
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return new OrderFieldLinkGroupsDetailDto();
        }

        var memberFieldIds = groups
            .SelectMany(group => group.Members)
            .Select(member => member.TemplateFieldId)
            .Distinct()
            .ToList();

        var templateFields = await _context.TemplateFields
            .AsNoTracking()
            .Where(field => memberFieldIds.Contains(field.Id) && !field.IsAnnulled)
            .ToListAsync(cancellationToken);

        var fieldsById = templateFields.ToDictionary(field => field.Id);

        var versionIds = groups
            .SelectMany(group => group.Members)
            .Select(member => member.TemplateVersionId)
            .Distinct()
            .ToList();

        var versions = await _context.TemplateVersions
            .AsNoTracking()
            .Include(version => version.Template)
            .Where(version => versionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, cancellationToken);

        var storageIdsByFieldId = await TemplateFieldStorageDataFieldResolver.ResolveStorageDataFieldIdsReadOnlyAsync(
            _context,
            templateFields,
            cancellationToken);

        var storageDataFieldIds = storageIdsByFieldId.Values.Distinct().ToList();

        var dataFieldKeys = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => storageDataFieldIds.Contains(dataField.Id))
            .ToDictionaryAsync(dataField => dataField.Id, dataField => dataField.Key, cancellationToken);

        var orderValues = await _context.OrderFieldValues
            .AsNoTracking()
            .Where(fieldValue => fieldValue.OrderId == orderId
                                  && fieldValue.SampleId == null
                                  && storageDataFieldIds.Contains(fieldValue.DataFieldId))
            .ToDictionaryAsync(fieldValue => fieldValue.DataFieldId, fieldValue => fieldValue.StoredValue, cancellationToken);

        var resultGroups = new List<OrderFieldLinkGroupDetailDto>();

        foreach (var group in groups)
        {
            var memberDetails = new List<OrderFieldLinkMemberDetailDto>();

            foreach (var member in group.Members.OrderBy(member => member.CreatedAtUtc))
            {
                if (!fieldsById.TryGetValue(member.TemplateFieldId, out var field))
                {
                    continue;
                }

                if (!versions.TryGetValue(member.TemplateVersionId, out var version))
                {
                    continue;
                }

                string? dataFieldKey = null;
                if (storageIdsByFieldId.TryGetValue(member.TemplateFieldId, out var storageId)
                    && dataFieldKeys.TryGetValue(storageId, out var key))
                {
                    dataFieldKey = key;
                }

                memberDetails.Add(new OrderFieldLinkMemberDetailDto
                {
                    TemplateVersionId = member.TemplateVersionId,
                    TemplateNameUk = version.Template.NameUk,
                    VersionNumber = version.VersionNumber,
                    TemplateFieldId = member.TemplateFieldId,
                    Tag = field.Tag,
                    Title = field.Title,
                    DataFieldKey = dataFieldKey
                });
            }

            var sharedValue = ResolveGroupSharedValue(group.Members, storageIdsByFieldId, orderValues);

            resultGroups.Add(new OrderFieldLinkGroupDetailDto
            {
                GroupId = group.Id,
                Label = group.Label,
                SortOrder = group.SortOrder,
                SharedValue = sharedValue,
                Members = memberDetails
            });
        }

        return new OrderFieldLinkGroupsDetailDto { Groups = resultGroups };
    }

    public async Task<IReadOnlyList<OrderFieldMappingSourceOrderDto>> GetFieldMappingSourceOrdersAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            take = 20;
        }

        return await (
            from linkGroup in _context.OrderFieldLinkGroups.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on linkGroup.OrderId equals order.Id
            group linkGroup by new { order.Id, order.ReferralNumber, order.CreatedAtUtc } into grouped
            orderby grouped.Key.CreatedAtUtc descending
            select new OrderFieldMappingSourceOrderDto
            {
                OrderId = grouped.Key.Id,
                ReferralNumber = grouped.Key.ReferralNumber,
                OrderDateUtc = grouped.Key.CreatedAtUtc,
                GroupCount = grouped.Count()
            })
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderFieldMappingAdaptResultDto> AdaptFieldLinkGroupsFromOrderAsync(
        Guid sourceOrderId,
        IReadOnlyList<Guid> targetTemplateVersionIds,
        CancellationToken cancellationToken = default)
    {
        if (targetTemplateVersionIds.Count < 2)
        {
            throw new InvalidOperationException("Оберіть щонайменше два шаблони для мапінгу.");
        }

        var sourceDetail = await GetFieldLinkGroupsForOrderAsync(sourceOrderId, cancellationToken);
        if (sourceDetail.Groups.Count == 0)
        {
            return new OrderFieldMappingAdaptResultDto
            {
                InfoMessage = "У вибраному замовленні немає збережених груп мапінгу."
            };
        }

        var targetMapping = await GetMappingPrepareAsync(targetTemplateVersionIds, cancellationToken);

        var adaptedGroups = new List<OrderFieldMappingAdaptedGroupDto>();
        var sharedValues = new List<OrderSharedFieldValueInput>();
        var usedTargetFieldIds = new HashSet<Guid>();
        var skippedMembers = 0;

        for (var sourceIndex = 0; sourceIndex < sourceDetail.Groups.Count; sourceIndex++)
        {
            var sourceGroup = sourceDetail.Groups[sourceIndex];
            var adaptedMembers = new List<OrderFieldMappingAdaptedMemberDto>();

            foreach (var sourceMember in sourceGroup.Members)
            {
                var targetMatch = ResolveTargetFieldForCopy(
                    sourceMember.TemplateVersionId,
                    sourceMember.TemplateFieldId,
                    sourceMember.Tag,
                    targetMapping,
                    usedTargetFieldIds);

                if (targetMatch is null)
                {
                    skippedMembers++;
                    continue;
                }

                usedTargetFieldIds.Add(targetMatch.Value.Field.TemplateFieldId);
                adaptedMembers.Add(new OrderFieldMappingAdaptedMemberDto
                {
                    TemplateVersionId = targetMatch.Value.TemplateVersionId,
                    TemplateFieldId = targetMatch.Value.Field.TemplateFieldId,
                    Tag = targetMatch.Value.Field.Tag,
                    Title = targetMatch.Value.Field.Title
                });
            }

            if (adaptedMembers.Count < 2)
            {
                foreach (var member in adaptedMembers)
                {
                    usedTargetFieldIds.Remove(member.TemplateFieldId);
                }

                continue;
            }

            var adaptedIndex = adaptedGroups.Count;
            adaptedGroups.Add(new OrderFieldMappingAdaptedGroupDto
            {
                Label = sourceGroup.Label,
                Members = adaptedMembers
            });

            if (!string.IsNullOrWhiteSpace(sourceGroup.SharedValue)
                && sourceGroup.SharedValue != "(різні значення в полях групи)")
            {
                sharedValues.Add(new OrderSharedFieldValueInput
                {
                    GroupIndex = adaptedIndex,
                    Value = sourceGroup.SharedValue
                });
            }
        }

        string? infoMessage = null;
        if (adaptedGroups.Count == 0)
        {
            infoMessage = "Не вдалося перенести жодну групу: теги полів не збігаються з обраними шаблонами.";
        }
        else if (skippedMembers > 0 || adaptedGroups.Count < sourceDetail.Groups.Count)
        {
            infoMessage =
                $"Перенесено {adaptedGroups.Count} з {sourceDetail.Groups.Count} груп (деякі поля пропущено — інші версії шаблонів або теги).";
        }

        return new OrderFieldMappingAdaptResultDto
        {
            Groups = adaptedGroups,
            SharedValues = sharedValues,
            InfoMessage = infoMessage
        };
    }

    private static (Guid TemplateVersionId, OrderFieldMappingFieldDto Field)? ResolveTargetFieldForCopy(
        Guid sourceVersionId,
        Guid sourceFieldId,
        string sourceTag,
        OrderFieldMappingPrepareDto targetMapping,
        IReadOnlySet<Guid> usedTargetFieldIds)
    {
        (Guid TemplateVersionId, OrderFieldMappingFieldDto Field)? TryPick(OrderFieldMappingTemplateDto template)
        {
            var exact = template.Fields.FirstOrDefault(field =>
                field.TemplateFieldId == sourceFieldId && field.CanWrite && !usedTargetFieldIds.Contains(field.TemplateFieldId));
            if (exact is not null)
            {
                return (template.TemplateVersionId, exact);
            }

            var byTag = template.Fields.FirstOrDefault(field =>
                string.Equals(field.Tag, sourceTag, StringComparison.OrdinalIgnoreCase)
                && field.CanWrite
                && !usedTargetFieldIds.Contains(field.TemplateFieldId));

            return byTag is null ? null : (template.TemplateVersionId, byTag);
        }

        var preferredTemplate = targetMapping.Templates.FirstOrDefault(template => template.TemplateVersionId == sourceVersionId);
        var preferred = preferredTemplate is not null ? TryPick(preferredTemplate) : null;
        if (preferred is not null)
        {
            return preferred;
        }

        foreach (var template in targetMapping.Templates)
        {
            if (template.TemplateVersionId == sourceVersionId)
            {
                continue;
            }

            var match = TryPick(template);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string? ResolveGroupSharedValue(
        IEnumerable<OrderFieldLinkMember> members,
        IReadOnlyDictionary<Guid, Guid> storageIdsByFieldId,
        IReadOnlyDictionary<Guid, string?> orderValues)
    {
        var distinctValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in members)
        {
            if (!storageIdsByFieldId.TryGetValue(member.TemplateFieldId, out var dataFieldId))
            {
                continue;
            }

            if (!orderValues.TryGetValue(dataFieldId, out var stored) || string.IsNullOrWhiteSpace(stored))
            {
                continue;
            }

            distinctValues.Add(stored.Trim());
        }

        return distinctValues.Count switch
        {
            0 => null,
            1 => distinctValues.First(),
            _ => "(різні значення в полях групи)"
        };
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
