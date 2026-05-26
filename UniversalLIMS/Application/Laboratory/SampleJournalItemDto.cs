using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

/// <summary>Рядок лабораторного журналу проб.</summary>
public sealed class SampleJournalItemDto
{
    public Guid SampleId { get; init; }

    /// <summary>SSOT: <see cref="Sample.Number"/>.</summary>
    public required string SampleNumber { get; init; }

    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    /// <summary>SSOT: <see cref="Customer.FullName"/> через <c>Order.CustomerId</c>.</summary>
    public required string CustomerFullName { get; init; }

    public DateTime RegisteredAt { get; init; }

    public SampleStatus Status { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string TargetBranchName { get; init; }
}
