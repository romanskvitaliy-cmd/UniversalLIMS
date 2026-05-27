using UniversalLIMS.Application.Laboratory;

namespace UniversalLIMS.ViewModels.Laboratory;

public sealed class LaboratoryResultsViewModel
{
    public ResultEntryFormDto Form { get; set; } = null!;

    public SaveResultEntryRequest Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public string? StatusType { get; set; }

    public bool HasPdfFillTargets { get; set; }

    public IReadOnlyList<SamplePdfFillTargetDto> PdfFillTargets { get; set; } = [];
}
