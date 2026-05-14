namespace UniversalLIMS.Application.Laboratory.Dtos;

/// <summary>
/// Read model for one active laboratory result row on a sample detail card.
/// </summary>
public sealed class LaboratoryResultDto
{
    public Guid ResultId { get; init; }

    public Guid DataFieldId { get; init; }

    public string DataFieldKey { get; init; } = string.Empty;

    public string DisplayNameUk { get; init; } = string.Empty;

    public string StoredValue { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public decimal Uncertainty { get; init; }

    public Guid EquipmentId { get; init; }

    public string EquipmentName { get; init; } = string.Empty;

    public DateTime EnteredAtUtc { get; init; }

    public string EnteredByUserId { get; init; } = string.Empty;
}
