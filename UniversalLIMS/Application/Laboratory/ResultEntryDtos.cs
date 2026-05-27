using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Laboratory;

public sealed class ResultEntryFormDto
{
    public Guid SampleId { get; init; }

    public string SampleNumber { get; init; } = string.Empty;

    public string? ReferralNumber { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string InvestigationTypeName { get; init; } = string.Empty;

    public SampleStatus Status { get; init; }

    public IReadOnlyList<ResultEntryFieldDto> Fields { get; init; } = [];

    public IReadOnlyList<ResultEquipmentOptionDto> EquipmentOptions { get; init; } = [];
}

public sealed class ResultEntryFieldDto
{
    public Guid DataFieldId { get; init; }

    public string Key { get; init; } = string.Empty;

    public string DisplayNameUk { get; init; } = string.Empty;

    public DataFieldType FieldType { get; init; }

    public string? Unit { get; init; }

    public string? CurrentValue { get; init; }

    public decimal? CurrentUncertainty { get; init; }

    public Guid? CurrentEquipmentId { get; init; }

    public bool CanWrite { get; init; }
}

public sealed class ResultEquipmentOptionDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string NameUk { get; init; } = string.Empty;
}

public sealed class SaveResultEntryRequest
{
    public List<SaveResultEntryFieldRequest> Values { get; set; } = [];

    public bool MarkResultsComplete { get; set; }

    public Guid? OrderDocumentId { get; set; }
}

public sealed class SaveResultEntryFieldRequest
{
    public Guid DataFieldId { get; set; }

    public string? Value { get; set; }

    public decimal? Uncertainty { get; set; }

    public Guid? EquipmentId { get; set; }
}

public sealed class SaveResultEntryResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int SavedCount { get; init; }

    public int SkippedCount { get; init; }
}
