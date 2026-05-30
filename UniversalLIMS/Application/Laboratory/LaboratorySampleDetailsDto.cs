using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

public sealed class LaboratorySampleDetailsDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string CustomerFullName { get; init; }

    public string? ReferralNumber { get; init; }

    public SampleStatus SampleStatus { get; init; }

    public required string WorkflowSummaryUk { get; init; }

    public IReadOnlyList<LaboratorySampleDocumentItemDto> Documents { get; init; } = [];
}

public sealed class LaboratorySampleDocumentItemDto
{
    public Guid SampleId { get; init; }

    public Guid OrderId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public Guid OrderDocumentId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public OrderDocumentStatus DocumentStatus { get; init; }

    public required string TargetBranchName { get; init; }

    public bool CanFill { get; init; }

    public bool CanSendToExpert { get; init; }
}
