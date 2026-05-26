using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Laboratory;

public sealed class LaboratoryIndexViewModel
{
    public required SampleJournalFilter Filter { get; init; }

    public required PagedResult<SampleJournalItemDto> Result { get; init; }

    public bool CanSelectLaboratoryBranch { get; init; }

    public Guid? ActiveLaboratoryBranchId { get; init; }

    public IReadOnlyList<BranchOptionDto> LaboratoryBranches { get; init; } = [];

    public static IReadOnlyList<(SampleStatus Value, string Label)> StatusOptions { get; } =
    [
        (SampleStatus.Registered, SampleStatusDisplay.ToUk(SampleStatus.Registered)),
        (SampleStatus.Routed, SampleStatusDisplay.ToUk(SampleStatus.Routed)),
        (SampleStatus.InProgress, SampleStatusDisplay.ToUk(SampleStatus.InProgress)),
        (SampleStatus.ResultsEntered, SampleStatusDisplay.ToUk(SampleStatus.ResultsEntered))
    ];
}
