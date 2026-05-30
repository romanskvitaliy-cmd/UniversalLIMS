namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryPdfFillService
{
    /// <summary>
    /// Документи проби, які лаборант може заповнювати в PDF Workspace (SentToLab / InProgress).
    /// </summary>
    Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Картка проби з усіма документами лабораторного workflow (не Pending).
    /// </summary>
    Task<LaboratorySampleDetailsDto?> GetSampleDetailsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);
}
