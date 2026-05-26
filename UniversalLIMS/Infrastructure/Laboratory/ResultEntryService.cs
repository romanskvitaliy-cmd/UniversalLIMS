using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class ResultEntryService : IResultEntryService
{
    private const string CorrectionAnnulmentReason = "Корекція значення результату.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IResultFieldPermissionService _permissions;
    private readonly ISampleWorkflowService _workflow;

    public ResultEntryService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ILaboratoryBranchContext laboratoryBranchContext,
        IDateTimeProvider dateTimeProvider,
        IResultFieldPermissionService permissions,
        ISampleWorkflowService workflow)
    {
        _context = context;
        _currentUser = currentUser;
        _laboratoryBranchContext = laboratoryBranchContext;
        _dateTimeProvider = dateTimeProvider;
        _permissions = permissions;
        _workflow = workflow;
    }

    public async Task<ResultEntryFormDto?> GetResultEntryFormAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var sample = await LoadSampleForBranchAsync(sampleId, cancellationToken);
        if (sample is null)
        {
            return null;
        }

        var dataFields = await LoadResultDataFieldsAsync(sample.InvestigationTypeId, cancellationToken);

        var currentValues = await _context.SampleResultValues
            .AsNoTracking()
            .Where(value => value.SampleId == sampleId)
            .ToDictionaryAsync(value => value.DataFieldId, cancellationToken);

        var fields = new List<ResultEntryFieldDto>();
        foreach (var dataField in dataFields)
        {
            currentValues.TryGetValue(dataField.Id, out var current);
            fields.Add(new ResultEntryFieldDto
            {
                DataFieldId = dataField.Id,
                Key = dataField.Key,
                DisplayNameUk = dataField.DisplayNameUk,
                FieldType = dataField.FieldType,
                Unit = dataField.Unit,
                CurrentValue = current?.StoredValue,
                CurrentUncertainty = current?.Uncertainty,
                CurrentEquipmentId = current?.EquipmentId,
                CanWrite = await _permissions.CanWriteAsync(sampleId, dataField.Id, cancellationToken)
            });
        }

        var equipmentOptions = await LoadEquipmentOptionsAsync(cancellationToken);

        return new ResultEntryFormDto
        {
            SampleId = sample.Id,
            SampleNumber = sample.Number,
            ReferralNumber = sample.Order.ReferralNumber,
            CustomerFullName = sample.Order.Customer.FullName,
            InvestigationTypeName = sample.InvestigationType.NameUk,
            Status = sample.Status,
            Fields = fields,
            EquipmentOptions = equipmentOptions
        };
    }

    public async Task<SaveResultEntryResult> SaveResultValuesAsync(
        Guid sampleId,
        SaveResultEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return new SaveResultEntryResult
            {
                Success = false,
                Message = "Користувач не автентифікований."
            };
        }

        var sample = await _context.Samples
            .Include(item => item.Order)
                .ThenInclude(order => order.OrderDocuments)
            .FirstOrDefaultAsync(item => item.Id == sampleId && !item.IsAnnulled, cancellationToken);

        if (sample is null || !await IsAccessibleForActiveLaboratoryAsync(sample, cancellationToken))
        {
            return new SaveResultEntryResult
            {
                Success = false,
                Message = "Пробу не знайдено або доступ заборонено."
            };
        }

        var defaultEquipmentId = await ResolveDefaultEquipmentIdAsync(cancellationToken);
        if (defaultEquipmentId is null)
        {
            return new SaveResultEntryResult
            {
                Success = false,
                Message = "У системі немає активного обладнання для фіксації результатів."
            };
        }

        var incoming = request.Values?
            .Where(item => item.DataFieldId != Guid.Empty)
            .GroupBy(item => item.DataFieldId)
            .Select(group => group.Last())
            .ToList() ?? [];

        var saved = 0;
        var skipped = 0;
        var enteredAtUtc = _dateTimeProvider.UtcNow;
        var userId = _currentUser.UserId!;

        foreach (var item in incoming)
        {
            if (!await _permissions.CanWriteAsync(sampleId, item.DataFieldId, cancellationToken))
            {
                skipped++;
                continue;
            }

            var dataField = await _context.DataFields
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    field =>
                        field.Id == item.DataFieldId
                        && field.Scope == DataFieldScope.Result
                        && field.IsActive
                        && !field.IsAnnulled,
                    cancellationToken);

            if (dataField is null)
            {
                skipped++;
                continue;
            }

            var trimmedValue = (item.Value ?? string.Empty).Trim();
            var activeRow = await _context.SampleResultValues
                .FirstOrDefaultAsync(
                    value => value.SampleId == sampleId && value.DataFieldId == item.DataFieldId,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(trimmedValue))
            {
                if (activeRow is not null)
                {
                    activeRow.Annul(CorrectionAnnulmentReason, userId);
                    saved++;
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            var equipmentId = item.EquipmentId ?? activeRow?.EquipmentId ?? defaultEquipmentId.Value;
            var unit = string.IsNullOrWhiteSpace(dataField.Unit) ? "—" : dataField.Unit!;
            var uncertainty = item.Uncertainty ?? activeRow?.Uncertainty ?? 0m;

            if (activeRow is not null)
            {
                if (string.Equals(activeRow.StoredValue, trimmedValue, StringComparison.Ordinal)
                    && activeRow.Uncertainty == uncertainty
                    && activeRow.EquipmentId == equipmentId)
                {
                    skipped++;
                    continue;
                }

                activeRow.Annul(CorrectionAnnulmentReason, userId);
            }

            var newRow = new SampleResultValue(
                sampleId,
                item.DataFieldId,
                trimmedValue,
                unit,
                uncertainty,
                equipmentId,
                enteredAtUtc,
                userId);

            _context.SampleResultValues.Add(newRow);
            saved++;
        }

        if (saved > 0 || request.MarkResultsComplete)
        {
            var sampleDocuments = sample.Order.OrderDocuments
                .Where(document => document.SampleId == sampleId)
                .ToList();

            _workflow.ApplyAfterResultSave(
                sample,
                sampleDocuments,
                request.MarkResultsComplete,
                hadPersistedChanges: saved > 0);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new SaveResultEntryResult
        {
            Success = true,
            Message = BuildSaveMessage(saved, skipped),
            SavedCount = saved,
            SkippedCount = skipped
        };
    }

    private static string BuildSaveMessage(int saved, int skipped)
    {
        if (saved > 0 && skipped > 0)
        {
            return $"Збережено змін: {saved}. Пропущено без прав або без змін: {skipped}.";
        }

        if (saved > 0)
        {
            return $"Збережено змін: {saved}.";
        }

        if (skipped > 0)
        {
            return $"Змін не збережено. Пропущено без прав або без змін: {skipped}.";
        }

        return "Змін для збереження не було.";
    }

    private async Task<Sample?> LoadSampleForBranchAsync(Guid sampleId, CancellationToken cancellationToken)
    {
        var sample = await _context.Samples
            .AsNoTracking()
            .Include(item => item.Order)
                .ThenInclude(order => order.Customer)
            .Include(item => item.Order)
                .ThenInclude(order => order.OrderDocuments)
            .Include(item => item.InvestigationType)
            .FirstOrDefaultAsync(item => item.Id == sampleId && !item.IsAnnulled, cancellationToken);

        if (sample is null || !await IsAccessibleForActiveLaboratoryAsync(sample, cancellationToken))
        {
            return null;
        }

        return sample;
    }

    private async Task<bool> IsAccessibleForActiveLaboratoryAsync(Sample sample, CancellationToken cancellationToken)
    {
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);
        if (branchContext.ActiveBranchId is not Guid branchId)
        {
            return true;
        }

        return sample.Order.OrderDocuments.Any(document =>
            !document.IsAnnulled
            && document.SampleId == sample.Id
            && document.TargetBranchId == branchId
            && document.Status != OrderDocumentStatus.Pending);
    }

    private async Task<IReadOnlyList<DataField>> LoadResultDataFieldsAsync(
        Guid investigationTypeId,
        CancellationToken cancellationToken)
    {
        var linkedRows = await (
            from link in _context.InvestigationTypeTemplates.AsNoTracking()
            join version in _context.TemplateVersions.AsNoTracking()
                on link.TemplateId equals version.TemplateId
            join templateField in _context.TemplateFields.AsNoTracking()
                on version.Id equals templateField.TemplateVersionId
            join dataField in _context.DataFields.AsNoTracking()
                on templateField.DataFieldId equals dataField.Id
            where link.InvestigationTypeId == investigationTypeId
                  && link.IsActive
                  && version.Status == TemplateVersionStatus.Published
                  && !version.IsAnnulled
                  && !templateField.IsAnnulled
                  && dataField.Scope == DataFieldScope.Result
                  && dataField.IsActive
                  && !dataField.IsAnnulled
            orderby templateField.SortOrder, dataField.DisplayNameUk
            select new
            {
                Field = dataField,
                templateField.SortOrder
            })
            .ToListAsync(cancellationToken);

        if (linkedRows.Count > 0)
        {
            return linkedRows
                .GroupBy(row => row.Field.Id)
                .Select(group => group.OrderBy(row => row.SortOrder).First())
                .OrderBy(row => row.SortOrder)
                .ThenBy(row => row.Field.DisplayNameUk)
                .Select(row => row.Field)
                .ToList();
        }

        return await _context.DataFields
            .AsNoTracking()
            .Where(field =>
                field.Scope == DataFieldScope.Result
                && field.IsSystem
                && field.IsActive
                && !field.IsAnnulled)
            .OrderBy(field => field.DescriptionUk)
            .ThenBy(field => field.Key)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ResultEquipmentOptionDto>> LoadEquipmentOptionsAsync(
        CancellationToken cancellationToken)
    {
        var query = _context.Equipment
            .AsNoTracking()
            .Where(equipment => equipment.IsActive && !equipment.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            query = query.Where(equipment => equipment.BranchId == null || equipment.BranchId == branchId);
        }

        return await query
            .OrderBy(equipment => equipment.Code)
            .Select(equipment => new ResultEquipmentOptionDto
            {
                Id = equipment.Id,
                Code = equipment.Code,
                NameUk = equipment.NameUk
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveDefaultEquipmentIdAsync(CancellationToken cancellationToken)
    {
        var query = _context.Equipment
            .AsNoTracking()
            .Where(equipment => equipment.IsActive && !equipment.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            var branchEquipment = await query
                .Where(equipment => equipment.BranchId == branchId)
                .OrderBy(equipment => equipment.Code)
                .Select(equipment => equipment.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (branchEquipment != Guid.Empty)
            {
                return branchEquipment;
            }
        }

        var globalEquipment = await query
            .OrderBy(equipment => equipment.Code)
            .Select(equipment => equipment.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return globalEquipment == Guid.Empty ? null : globalEquipment;
    }
}
