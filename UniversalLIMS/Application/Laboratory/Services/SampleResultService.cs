using System.Globalization;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Common.Exceptions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Laboratory.Services;

/// <summary>
/// Coordinates immutable laboratory result entry with SSOT validation and ISO 17025 traceability.
/// </summary>
public sealed class SampleResultService : ILaboratoryResultService
{
    private readonly ISampleRepository _sampleRepository;
    private readonly ISampleResultRepository _sampleResultRepository;
    private readonly ILaboratoryDataFieldRepository _dataFieldRepository;
    private readonly ILaboratoryEquipmentRepository _equipmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SampleResultService(
        ISampleRepository sampleRepository,
        ISampleResultRepository sampleResultRepository,
        ILaboratoryDataFieldRepository dataFieldRepository,
        ILaboratoryEquipmentRepository equipmentRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _sampleRepository = sampleRepository;
        _sampleResultRepository = sampleResultRepository;
        _dataFieldRepository = dataFieldRepository;
        _equipmentRepository = equipmentRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<SampleResultValue> AddResultAsync(
        Guid sampleId,
        Guid dataFieldId,
        string storedValue,
        string? unit,
        string? uncertainty,
        Guid equipmentId,
        Guid userId)
    {
        var sample = await RequireActiveSampleAsync(sampleId);
        var dataField = await RequireResultDataFieldAsync(dataFieldId);
        await RequireActiveEquipmentAsync(equipmentId);

        if (await _sampleResultRepository.ExistsActiveAsync(sampleId, dataFieldId))
        {
            throw new BusinessRuleViolationException(
                "An active result already exists for this sample and data field. Annul the existing row before entering a correction.");
        }

        var resolvedUnit = ResolveUnit(unit, dataField);
        var resolvedUncertainty = ParseUncertainty(uncertainty);
        var enteredByUserId = ResolveUserId(userId);
        var enteredAtUtc = _dateTimeProvider.UtcNow;
        var isFirstResult = await _sampleResultRepository.CountActiveForSampleAsync(sampleId) == 0;

        var result = new SampleResultValue(
            sampleId,
            dataFieldId,
            storedValue,
            resolvedUnit,
            resolvedUncertainty,
            equipmentId,
            enteredAtUtc,
            enteredByUserId);

        ApplyFirstResultStatusTransition(sample, isFirstResult);
        _sampleResultRepository.Add(result);
        await _unitOfWork.SaveChangesAsync();

        return result;
    }

    /// <inheritdoc />
    public async Task AddResultsBatchAsync(
        Guid sampleId,
        Dictionary<Guid, string> fieldValues,
        Guid equipmentId,
        Guid userId)
    {
        if (fieldValues.Count == 0)
        {
            throw new BusinessRuleViolationException("At least one result value is required for batch entry.");
        }

        var sample = await RequireActiveSampleAsync(sampleId);
        await RequireActiveEquipmentAsync(equipmentId);

        var dataFieldIds = fieldValues.Keys.ToList();
        var dataFields = await _dataFieldRepository.GetByIdsIncludingAnnulledAsync(dataFieldIds);

        if (dataFields.Count != dataFieldIds.Count)
        {
            var missingId = dataFieldIds.First(id => !dataFields.ContainsKey(id));
            throw new EntityNotFoundException(nameof(DataField), missingId);
        }

        foreach (var dataField in dataFields.Values)
        {
            ValidateResultDataField(dataField);
        }

        foreach (var (dataFieldId, _) in fieldValues)
        {
            if (await _sampleResultRepository.ExistsActiveAsync(sampleId, dataFieldId))
            {
                throw new BusinessRuleViolationException(
                    $"An active result already exists for data field '{dataFieldId}'. Annul it before batch correction.");
            }
        }

        var enteredByUserId = ResolveUserId(userId);
        var enteredAtUtc = _dateTimeProvider.UtcNow;
        var isFirstResult = await _sampleResultRepository.CountActiveForSampleAsync(sampleId) == 0;

        foreach (var (dataFieldId, value) in fieldValues)
        {
            var dataField = dataFields[dataFieldId];
            var result = new SampleResultValue(
                sampleId,
                dataFieldId,
                value,
                ResolveUnit(unit: null, dataField),
                uncertainty: 0m,
                equipmentId,
                enteredAtUtc,
                enteredByUserId);

            _sampleResultRepository.Add(result);
        }

        ApplyFirstResultStatusTransition(sample, isFirstResult);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task AnnulResultAsync(Guid resultId, string reason, Guid userId)
    {
        var result = await _sampleResultRepository.GetByIdAsync(resultId, includeAnnulled: false)
            ?? throw new EntityNotFoundException(nameof(SampleResultValue), resultId);

        var annulledByUserId = ResolveUserId(userId);
        result.Annul(reason, annulledByUserId);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SampleResultValue>> GetResultsForSampleAsync(
        Guid sampleId,
        bool includeAnnulled = false)
    {
        return _sampleResultRepository.GetForSampleAsync(sampleId, includeAnnulled);
    }

    private async Task<Sample> RequireActiveSampleAsync(Guid sampleId)
    {
        var sample = await _sampleRepository.GetByIdIncludingAnnulledAsync(sampleId)
            ?? throw new EntityNotFoundException(nameof(Sample), sampleId);

        if (sample.IsAnnulled)
        {
            throw new BusinessRuleViolationException("Results cannot be entered for an annulled sample.");
        }

        if (sample.Status == SampleStatus.Registered)
        {
            throw new BusinessRuleViolationException(
                "The sample has not been routed to the laboratory yet. Route the sample before entering results.");
        }

        return sample;
    }

    private async Task<DataField> RequireResultDataFieldAsync(Guid dataFieldId)
    {
        var dataField = await _dataFieldRepository.GetByIdIncludingAnnulledAsync(dataFieldId)
            ?? throw new EntityNotFoundException(nameof(DataField), dataFieldId);

        ValidateResultDataField(dataField);
        return dataField;
    }

    private async Task RequireActiveEquipmentAsync(Guid equipmentId)
    {
        var equipment = await _equipmentRepository.GetByIdIncludingAnnulledAsync(equipmentId)
            ?? throw new EntityNotFoundException(nameof(Equipment), equipmentId);

        if (equipment.IsAnnulled)
        {
            throw new BusinessRuleViolationException("Annulled equipment cannot be used for result entry.");
        }

        if (!equipment.IsActive)
        {
            throw new BusinessRuleViolationException("Inactive equipment cannot be used for result entry.");
        }
    }

    private static void ValidateResultDataField(DataField dataField)
    {
        if (dataField.IsAnnulled)
        {
            throw new BusinessRuleViolationException("Annulled data fields cannot receive laboratory results.");
        }

        if (!dataField.IsActive)
        {
            throw new BusinessRuleViolationException("Inactive data fields cannot receive laboratory results.");
        }

        if (dataField.Scope != DataFieldScope.Result)
        {
            throw new BusinessRuleViolationException(
                "Only dictionary fields with Scope = Result can be stored in SampleResultValue (SSOT).");
        }
    }

    private static string ResolveUnit(string? unit, DataField dataField)
    {
        var resolved = string.IsNullOrWhiteSpace(unit) ? dataField.Unit : unit.Trim();

        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new BusinessRuleViolationException(
                "Unit is required. Provide it explicitly or configure Unit on the DataField dictionary entry.");
        }

        return resolved;
    }

    private static decimal ParseUncertainty(string? uncertainty)
    {
        if (string.IsNullOrWhiteSpace(uncertainty))
        {
            return 0m;
        }

        if (decimal.TryParse(uncertainty.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (decimal.TryParse(uncertainty.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            return value;
        }

        throw new BusinessRuleViolationException(
            $"Uncertainty value '{uncertainty}' is not a valid decimal number.");
    }

    private static string ResolveUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleViolationException("User identifier is required for traceability.");
        }

        return userId.ToString("D");
    }

    private static void ApplyFirstResultStatusTransition(Sample sample, bool isFirstResult)
    {
        if (!isFirstResult)
        {
            return;
        }

        if (sample.Status is SampleStatus.Routed or SampleStatus.Registered)
        {
            sample.Status = SampleStatus.InProgress;
        }
    }
}
