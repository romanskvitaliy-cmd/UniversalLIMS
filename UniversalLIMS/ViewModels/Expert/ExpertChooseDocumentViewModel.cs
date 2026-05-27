using UniversalLIMS.Application.Laboratory;

namespace UniversalLIMS.ViewModels.Expert;

public sealed class ExpertChooseDocumentViewModel
{
    public required string SampleNumber { get; init; }

    public required string InvestigationTypeName { get; init; }

    public IReadOnlyList<SamplePdfFillTargetDto> Targets { get; init; } = [];
}
