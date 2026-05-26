namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryPdfFillService
{
    /// <summary>
    /// Документи проби, які лаборант може заповнювати в PDF Workspace (SentToLab / InProgress),
    /// або fallback на опублікований PDF-шаблон типу дослідження.
    /// </summary>
    Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);
}
