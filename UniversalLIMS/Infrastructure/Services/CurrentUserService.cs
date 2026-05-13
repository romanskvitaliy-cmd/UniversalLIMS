using System.Security.Claims;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => User?.Identity?.Name;

    public string? UserFullName => User?.FindFirstValue(LimsClaimTypes.FullName) ?? UserName;

    public Guid? BranchId
    {
        get
        {
            var branchId = User?.FindFirstValue(LimsClaimTypes.BranchId);
            return Guid.TryParse(branchId, out var value) ? value : null;
        }
    }

    public string? IpAddress => HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => HttpContext?.Request.Headers.UserAgent.ToString();

    public string? CorrelationId => HttpContext?.TraceIdentifier;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    private ClaimsPrincipal? User => HttpContext?.User;
}
