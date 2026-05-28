using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Expert;

public sealed class ExpertSampleDetailsViewModel
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string CustomerFullName { get; init; }

    public string? ReferralNumber { get; init; }

    public DateTime RegisteredAtUtc { get; init; }

    public ExpertConclusionStatus? ReviewStatus { get; init; }

    public DateTime? ReviewStartedAtUtc { get; init; }

    public DateTime? ApprovedAtUtc { get; init; }

    public string? NotesUk { get; init; }

    public Guid OrderId { get; init; }

    public IReadOnlyList<ExpertSampleDocumentItemViewModel> Documents { get; init; } = [];
}

public sealed class ExpertSampleDocumentItemViewModel
{
    public Guid OrderDocumentId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public required string TemplateName { get; init; }

    public int VersionNumber { get; init; }

    public required string TargetBranchName { get; init; }

    public OrderDocumentStatus Status { get; init; }

    public DateTime? SentToLabAtUtc { get; init; }
}
