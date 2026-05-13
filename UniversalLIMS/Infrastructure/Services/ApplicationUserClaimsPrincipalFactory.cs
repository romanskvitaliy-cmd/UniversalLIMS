using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Identity;

namespace UniversalLIMS.Infrastructure.Services;

public sealed class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(new Claim(LimsClaimTypes.FullName, user.FullName));
        }

        if (user.BranchId.HasValue)
        {
            identity.AddClaim(new Claim(LimsClaimTypes.BranchId, user.BranchId.Value.ToString()));
        }

        return identity;
    }
}
