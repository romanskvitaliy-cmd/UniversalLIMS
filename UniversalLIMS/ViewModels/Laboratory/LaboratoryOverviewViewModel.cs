using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Laboratory;

namespace UniversalLIMS.ViewModels.Laboratory;

public sealed class LaboratoryOverviewViewModel
{
    public required LaboratoryOverviewDto Overview { get; init; }

    public required ExpertOverviewDto ExpertOverview { get; init; }
}
