namespace UniversalLIMS.Presentation.WebApi.Models;

/// <summary>
/// Response for sample finalization readiness checks.
/// </summary>
public sealed class CanFinalizeSampleResponse
{
    /// <summary>
    /// <see langword="true"/> when every required result field has an active value.
    /// </summary>
    public required bool CanFinalize { get; init; }
}
