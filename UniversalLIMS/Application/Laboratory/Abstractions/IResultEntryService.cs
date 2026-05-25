namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface IResultEntryService
{
    Task<ResultEntryFormDto?> GetResultEntryFormAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default);

    Task<SaveResultEntryResult> SaveResultValuesAsync(
        Guid sampleId,
        SaveResultEntryRequest request,
        CancellationToken cancellationToken = default);
}
