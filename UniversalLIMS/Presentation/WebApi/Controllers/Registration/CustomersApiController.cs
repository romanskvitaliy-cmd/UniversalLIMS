using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Presentation.WebApi.Controllers;

namespace UniversalLIMS.Presentation.WebApi.Controllers.Registration;

[Route("api/customers")]
[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class CustomersApiController : ApiControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersApiController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("search")]
    public Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var results = await _customerService.SearchAsync(q, take, cancellationToken);
            return Ok(results);
        });
}
