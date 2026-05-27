using UniversalLIMS.Application.Laboratory;

namespace UniversalLIMS.Application.Expert.Abstractions;

public interface IExpertPdfFillService
{
    Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);
}
