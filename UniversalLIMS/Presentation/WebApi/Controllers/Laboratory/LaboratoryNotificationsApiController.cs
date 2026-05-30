using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Laboratory;

[Route("api/laboratory/notifications")]
[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[RequireActiveLimsRole]
public sealed class LaboratoryNotificationsApiController : ApiControllerBase
{
    private readonly ILaboratoryJournalService _journal;

    public LaboratoryNotificationsApiController(ILaboratoryJournalService journal)
    {
        _journal = journal;
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

            var items = await _journal.GetIncomingSinceAsync(sinceUtc, cancellationToken);
            var reworkItems = await _journal.GetReworkSinceAsync(sinceUtc, cancellationToken);
            return Ok(new
            {
                items,
                reworkItems,
                serverTimeUtc = DateTime.UtcNow
            });
        });
}
