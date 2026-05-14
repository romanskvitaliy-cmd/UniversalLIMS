using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

/// <summary>
/// Application service for immutable laboratory result entry and soft annulment (ISO 17025).
/// </summary>
public interface ILaboratoryResultService
{
    /// <summary>
    /// Creates a new immutable result row for one <c>DataField</c> on the specified sample.
    /// </summary>
    /// <param name="userId">Identity user identifier of the technician (GUID form of ASP.NET Identity id).</param>
    Task<SampleResultValue> AddResultAsync(
        Guid sampleId,
        Guid dataFieldId,
        string storedValue,
        string? unit,
        string? uncertainty,
        Guid equipmentId,
        Guid userId);

    /// <summary>
    /// Creates multiple immutable result rows in a single unit-of-work transaction.
    /// </summary>
    /// <param name="fieldValues">Map of <c>DataFieldId</c> to captured value.</param>
    /// <param name="userId">Identity user identifier of the technician.</param>
    Task AddResultsBatchAsync(
        Guid sampleId,
        Dictionary<Guid, string> fieldValues,
        Guid equipmentId,
        Guid userId);

    /// <summary>
    /// Soft-annuls an existing result. Physical deletion is forbidden; corrections require a new row.
    /// </summary>
    Task AnnulResultAsync(Guid resultId, string reason, Guid userId);

    /// <summary>
    /// Returns all result rows for a sample, optionally including annulled history.
    /// </summary>
    Task<IReadOnlyList<SampleResultValue>> GetResultsForSampleAsync(
        Guid sampleId,
        bool includeAnnulled = false);
}
