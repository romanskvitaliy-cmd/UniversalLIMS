using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Expert;

[Route("api/expert/notifications")]
[Authorize(Policy = LimsPolicies.ApproveConclusions)]
[RequireActiveLimsRole]
public sealed class ExpertNotificationsApiController : ApiControllerBase
{
    private readonly IExpertReviewQueueService _queue;

    public ExpertNotificationsApiController(IExpertReviewQueueService queue)
    {
        _queue = queue;
    }

    [HttpGet("incoming")]
    public Task<IActionResult> GetIncoming([FromQuery] DateTime? since, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var sinceUtc = since ?? DateTime.UtcNow.AddMinutes(-1);
            if (sinceUtc.Kind == DateTimeKind.Unspecified)
            {
                sinceUtc = DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc);
            }
            else if (sinceUtc.Kind == DateTimeKind.Local)
            {
                sinceUtc = sinceUtc.ToUniversalTime();
            }

            var items = await _queue.GetIncomingSinceAsync(sinceUtc, cancellationToken);
            return Ok(new
            {
                items,
                serverTimeUtc = DateTime.UtcNow
            });
        });
}
