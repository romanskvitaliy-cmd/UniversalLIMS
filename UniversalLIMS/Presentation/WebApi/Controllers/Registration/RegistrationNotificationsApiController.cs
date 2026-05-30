using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Registration;

[Route("api/registration/notifications")]
[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class RegistrationNotificationsApiController : ApiControllerBase
{
    private readonly IRegistrationNotificationService _notifications;

    public RegistrationNotificationsApiController(IRegistrationNotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet("ready-for-pickup")]
    public Task<IActionResult> GetReadyForPickup([FromQuery] DateTime? since, CancellationToken cancellationToken) =>
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

            var items = await _notifications.GetReadyForPickupSinceAsync(sinceUtc, cancellationToken);
            return Ok(new
            {
                items,
                serverTimeUtc = DateTime.UtcNow
            });
        });
}
