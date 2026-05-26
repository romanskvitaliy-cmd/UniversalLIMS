using UniversalLIMS.Application.Laboratory;

namespace UniversalLIMS.ViewModels.Laboratory;

public sealed class LaboratoryChooseDocumentViewModel
{
    public required string SampleNumber { get; init; }

    public required string InvestigationTypeName { get; init; }

    public IReadOnlyList<SamplePdfFillTargetDto> Targets { get; init; } = [];
}
