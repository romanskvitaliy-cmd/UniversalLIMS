using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Domain.Common.Exceptions;
using UniversalLIMS.Presentation.WebApi.Models;

namespace UniversalLIMS.Presentation.WebApi.Controllers;

/// <summary>
/// Base API controller with consistent domain-exception mapping.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Executes an action and maps known <see cref="DomainException"/> types to HTTP responses.
    /// </summary>
    protected async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new ApiErrorResponse(ex.Message));
        }
        catch (BusinessRuleViolationException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
        catch (DomainException ex)
        {
            return BadRequest(new ApiErrorResponse(ex.Message));
        }
    }
}
