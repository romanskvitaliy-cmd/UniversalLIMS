using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Registration;

[Route("api/orders")]
[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class OrdersApiController : ApiControllerBase
{
    private readonly IOrderRegistrationService _orderRegistration;

    public OrdersApiController(IOrderRegistrationService orderRegistration)
    {
        _orderRegistration = orderRegistration;
    }

    [HttpGet]
    public Task<IActionResult> Get([FromQuery] OrderFilter filter, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await _orderRegistration.GetOrdersAsync(filter, cancellationToken);
            return Ok(result);
        });

    [HttpPost]
    public Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var result = await _orderRegistration.CreateOrderAsync(request, cancellationToken);
            return Ok(result);
        });
}
