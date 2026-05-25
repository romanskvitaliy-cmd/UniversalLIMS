using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

/// <summary>Фільтри лабораторного журналу проб (read-only).</summary>
public sealed class SampleJournalFilter
{
    public string? SampleNumber { get; init; }

    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public SampleStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
