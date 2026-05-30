using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Laboratory;

[Route("api/laboratory/documents")]
[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[RequireActiveLimsRole]
public sealed class LaboratoryDocumentsApiController : ApiControllerBase
{
    private readonly ILaboratoryDocumentSubmissionService _submission;

    public LaboratoryDocumentsApiController(ILaboratoryDocumentSubmissionService submission)
    {
        _submission = submission;
    }

    [HttpPost("{orderDocumentId:guid}/send-to-expert")]
    public Task<IActionResult> SendToExpert(Guid orderDocumentId, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            try
            {
                var result = await _submission.SendDocumentToExpertAsync(orderDocumentId, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        });
}
