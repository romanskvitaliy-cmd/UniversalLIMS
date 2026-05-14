using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Common;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Laboratory.Dtos;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Presentation.WebApi.Models;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Laboratory;

/// <summary>
/// Laboratory sample journal and read-side sample card API.
/// </summary>
[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[Route("api/laboratory")]
public sealed class LaboratoryJournalController : ApiControllerBase
{
    private readonly ILaboratoryJournalService _journalService;

    public LaboratoryJournalController(ILaboratoryJournalService journalService)
    {
        _journalService = journalService;
    }

    /// <summary>
    /// Returns a paginated laboratory journal of routed samples with configured result fields.
    /// </summary>
    /// <param name="filter">
    /// Journal filters. When <see cref="LaboratoryJournalFilter.TargetBranchId"/> is omitted,
    /// the current user's branch is applied automatically by the application service.
    /// </param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Paginated journal page.</response>
    /// <response code="400">Invalid filter or domain rule violation.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not authorized for laboratory operations.</response>
    [HttpGet("journal")]
    [ProducesResponseType(typeof(PagedResult<LaboratoryJournalItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetJournal(
        [FromQuery] LaboratoryJournalFilter filter,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var result = await _journalService.GetJournalAsync(filter, cancellationToken);
            return Ok(result);
        });
    }

    /// <summary>
    /// Returns the laboratory detail card for a sample, including active result rows.
    /// </summary>
    /// <param name="sampleId">Sample identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Sample laboratory details.</response>
    /// <response code="400">Sample is not part of the laboratory journal.</response>
    /// <response code="404">Sample was not found.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not authorized for laboratory operations.</response>
    [HttpGet("samples/{sampleId:guid}")]
    [ProducesResponseType(typeof(SampleLaboratoryDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetSampleDetails(Guid sampleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var details = await _journalService.GetSampleDetailsAsync(sampleId, cancellationToken);
            return Ok(details);
        });
    }

    /// <summary>
    /// Checks whether all required laboratory result fields have active values for the sample.
    /// </summary>
    /// <param name="sampleId">Sample identifier.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <response code="200">Finalization readiness flag.</response>
    /// <response code="400">Domain rule violation.</response>
    /// <response code="401">Caller is not authenticated.</response>
    /// <response code="403">Caller is not authorized for laboratory operations.</response>
    [HttpGet("samples/{sampleId:guid}/can-finalize")]
    [ProducesResponseType(typeof(CanFinalizeSampleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> CanFinalizeSample(Guid sampleId, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            var canFinalize = await _journalService.CanFinalizeSampleAsync(sampleId, cancellationToken);
            return Ok(new CanFinalizeSampleResponse { CanFinalize = canFinalize });
        });
    }
}
