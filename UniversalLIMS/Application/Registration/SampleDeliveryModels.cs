using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public sealed class SampleDeliveryQueueFilter
{
    public string? SampleNumber { get; set; }

    public string? CustomerFullName { get; set; }

    /// <summary>false = лише очікують видачі; true = архів виданих.</summary>
    public bool ShowIssued { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class SampleDeliveryQueueItemDto
{
    public Guid SampleId { get; init; }

    public string SampleNumber { get; init; } = string.Empty;

    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string InvestigationTypeName { get; init; } = string.Empty;

    public DateTime? ApprovedAtUtc { get; init; }

    public DateTime? ReadyForPickupAtUtc { get; init; }

    public DateTime? IssuedAtUtc { get; init; }

    public SampleDeliveryStatus DeliveryStatus { get; init; }

    public Guid? SingleDocumentIdForFinalPdf { get; init; }

    public Guid? SingleTemplateVersionIdForFinalPdf { get; init; }

    public int DocumentCount { get; init; }
}
