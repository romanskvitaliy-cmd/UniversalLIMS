using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Laboratory.Dtos;
using UniversalLIMS.Domain.Common.Exceptions;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Laboratory.Services;

/// <summary>
/// Coordinates laboratory journal read models with SSOT result retrieval.
/// </summary>
public sealed class LaboratoryJournalService : ILaboratoryJournalService
{
    private readonly ILaboratoryJournalQuery _journalQuery;
    private readonly ILaboratoryResultService _resultService;
    private readonly ILaboratoryDataFieldRepository _dataFieldRepository;
    private readonly ILaboratoryEquipmentRepository _equipmentRepository;
    private readonly ICurrentUserService _currentUserService;

    public LaboratoryJournalService(
        ILaboratoryJournalQuery journalQuery,
        ILaboratoryResultService resultService,
        ILaboratoryDataFieldRepository dataFieldRepository,
        ILaboratoryEquipmentRepository equipmentRepository,
        ICurrentUserService currentUserService)
    {
        _journalQuery = journalQuery;
        _resultService = resultService;
        _dataFieldRepository = dataFieldRepository;
        _equipmentRepository = equipmentRepository;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public async Task<PagedResult<LaboratoryJournalItemDto>> GetJournalAsync(
        LaboratoryJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        var effectiveFilter = ApplyBranchScope(filter);
        var (items, totalCount) = await _journalQuery.SearchAsync(effectiveFilter, cancellationToken);

        return new PagedResult<LaboratoryJournalItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = Math.Max(1, effectiveFilter.Page),
            PageSize = Math.Clamp(effectiveFilter.PageSize, 1, 100)
        };
    }

    /// <inheritdoc />
    public async Task<SampleLaboratoryDetailsDto> GetSampleDetailsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var header = await _journalQuery.GetSampleHeaderAsync(sampleId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Domain.Registration.Sample), sampleId);

        if (!await _journalQuery.IsLaboratorySampleAsync(sampleId, cancellationToken))
        {
            throw new BusinessRuleViolationException(
                "The sample is not available in the laboratory journal. Route the sample and ensure result fields are configured.");
        }

        var results = await _resultService.GetResultsForSampleAsync(sampleId, includeAnnulled: false);
        var resultDtos = await MapResultsAsync(results, cancellationToken);

        return new SampleLaboratoryDetailsDto
        {
            SampleId = header.SampleId,
            SampleNumber = header.SampleNumber,
            ReceivedAt = header.ReceivedAt,
            RoutedAtUtc = header.RoutedAtUtc,
            Status = header.Status,
            Notes = header.Notes,
            InvestigationTypeId = header.InvestigationTypeId,
            InvestigationTypeName = header.InvestigationTypeName,
            OrderId = header.OrderId,
            ReferralNumber = header.ReferralNumber,
            CustomerFullName = header.CustomerFullName,
            CustomerOrganizationName = header.CustomerOrganizationName,
            TargetBranchName = header.TargetBranchName,
            RequiredResultsCount = header.RequiredResultsCount,
            EnteredResultsCount = header.EnteredResultsCount,
            Results = resultDtos
        };
    }

    /// <inheritdoc />
    public async Task<bool> CanFinalizeSampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        if (!await _journalQuery.IsLaboratorySampleAsync(sampleId, cancellationToken))
        {
            return false;
        }

        var requiredFieldIds = await _journalQuery.GetRequiredResultDataFieldIdsAsync(sampleId, cancellationToken);
        if (requiredFieldIds.Count == 0)
        {
            return false;
        }

        var activeResults = await _resultService.GetResultsForSampleAsync(sampleId, includeAnnulled: false);
        var enteredFieldIds = activeResults.Select(result => result.DataFieldId).ToHashSet();

        return requiredFieldIds.All(enteredFieldIds.Contains);
    }

    private LaboratoryJournalFilter ApplyBranchScope(LaboratoryJournalFilter filter)
    {
        if (filter.TargetBranchId.HasValue || _currentUserService.BranchId is null)
        {
            return filter;
        }

        return new LaboratoryJournalFilter
        {
            ReceivedDateFrom = filter.ReceivedDateFrom,
            ReceivedDateTo = filter.ReceivedDateTo,
            InvestigationTypeId = filter.InvestigationTypeId,
            SampleStatus = filter.SampleStatus,
            SearchText = filter.SearchText,
            TargetBranchId = _currentUserService.BranchId,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    private async Task<IReadOnlyList<LaboratoryResultDto>> MapResultsAsync(
        IReadOnlyList<SampleResultValue> results,
        CancellationToken cancellationToken)
    {
        if (results.Count == 0)
        {
            return [];
        }

        var dataFieldIds = results.Select(result => result.DataFieldId).Distinct().ToList();
        var equipmentIds = results.Select(result => result.EquipmentId).Distinct().ToList();

        var dataFields = await _dataFieldRepository.GetByIdsIncludingAnnulledAsync(dataFieldIds, cancellationToken);
        var equipment = await _equipmentRepository.GetByIdsIncludingAnnulledAsync(equipmentIds, cancellationToken);

        return results
            .OrderBy(result => result.EnteredAtUtc)
            .Select(result =>
            {
                dataFields.TryGetValue(result.DataFieldId, out var dataField);
                equipment.TryGetValue(result.EquipmentId, out var equipmentItem);

                return new LaboratoryResultDto
                {
                    ResultId = result.Id,
                    DataFieldId = result.DataFieldId,
                    DataFieldKey = dataField?.Key ?? string.Empty,
                    DisplayNameUk = dataField?.DisplayNameUk ?? string.Empty,
                    StoredValue = result.StoredValue,
                    Unit = result.Unit,
                    Uncertainty = result.Uncertainty,
                    EquipmentId = result.EquipmentId,
                    EquipmentName = equipmentItem?.NameUk ?? string.Empty,
                    EnteredAtUtc = result.EnteredAtUtc,
                    EnteredByUserId = result.EnteredByUserId
                };
            })
            .ToList();
    }
}
