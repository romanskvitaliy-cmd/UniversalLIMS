using System.Security.Claims;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Tests.Security;

public sealed class PortalEntryFlowTests
{
    [Fact]
    public void CanAccessRolePortal_ForAnyUserWithPickableRole()
    {
        Assert.True(PortalEntryFlow.CanAccessRolePortal(UserWithRoles(LimsRoles.SystemAdministrator)));
        Assert.True(PortalEntryFlow.CanAccessRolePortal(UserWithRoles(LimsRoles.Registrar)));
        Assert.True(PortalEntryFlow.CanAccessRolePortal(UserWithRoles(LimsRoles.LaboratoryTechnician)));
        Assert.True(PortalEntryFlow.CanAccessRolePortal(UserWithRoles(LimsRoles.Specialist)));
    }

    [Fact]
    public void CanSwitchRole_OnlyWhenMultiplePickableRoles()
    {
        Assert.True(PortalEntryFlow.CanSwitchRole(UserWithRoles(LimsRoles.SystemAdministrator)));
        Assert.False(PortalEntryFlow.CanSwitchRole(UserWithRoles(LimsRoles.Registrar)));
        Assert.True(PortalEntryFlow.CanSwitchRole(UserWithRoles(LimsRoles.Registrar, LimsRoles.LaboratoryTechnician)));
    }

    [Fact]
    public void GetDefaultLandingPath_AdminToPortal_OthersToWorkspace()
    {
        Assert.Equal("/", PortalEntryFlow.GetDefaultLandingPath(UserWithRoles(LimsRoles.SystemAdministrator)));
        Assert.Equal("/Home/Workspace", PortalEntryFlow.GetDefaultLandingPath(UserWithRoles(LimsRoles.Registrar)));
    }

    [Fact]
    public void ResolveWorkspaceEntryRole_ReturnsSingleOrFirstAssumable()
    {
        Assert.Equal(
            LimsRoles.LaboratoryTechnician,
            PortalEntryFlow.ResolveWorkspaceEntryRole(UserWithRoles(LimsRoles.LaboratoryTechnician)));

        var multi = UserWithRoles(LimsRoles.Registrar, LimsRoles.LaboratoryTechnician);
        Assert.Equal(LimsRoles.Registrar, PortalEntryFlow.ResolveWorkspaceEntryRole(multi));
    }

    [Fact]
    public void TryGetSingleAssumableRole_ReturnsRole_WhenOnlyRegistrar()
    {
        var user = UserWithRoles(LimsRoles.Registrar);

        var role = PortalEntryFlow.TryGetSingleAssumableRole(user);

        Assert.Equal(LimsRoles.Registrar, role);
    }

    [Fact]
    public void TryGetSingleAssumableRole_ReturnsNull_WhenAdministrator()
    {
        var user = UserWithRoles(LimsRoles.SystemAdministrator);

        Assert.Null(PortalEntryFlow.TryGetSingleAssumableRole(user));
    }

    [Fact]
    public void TryGetSingleAssumableRole_ReturnsNull_WhenMultipleIdentityRoles()
    {
        var user = UserWithRoles(LimsRoles.Registrar, LimsRoles.LaboratoryTechnician);

        Assert.Null(PortalEntryFlow.TryGetSingleAssumableRole(user));
    }

    [Fact]
    public void ShouldAutoRedirectToWorkspace_RespectsOption()
    {
        var user = UserWithRoles(LimsRoles.Specialist);

        Assert.True(PortalEntryFlow.ShouldAutoRedirectToWorkspace(user, autoRedirectEnabled: true));
        Assert.False(PortalEntryFlow.ShouldAutoRedirectToWorkspace(user, autoRedirectEnabled: false));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/Identity/Account/Manage", true)]
    [InlineData("/Identity/Account/Login", true)]
    [InlineData("/", false)]
    [InlineData("/Home/Workspace", false)]
    [InlineData("/Templates", false)]
    public void ShouldRedirectToPortalHome_DetectsIdentityPaths(string? redirectUri, bool expected)
    {
        Assert.Equal(expected, PortalEntryFlow.ShouldRedirectToPortalHome(redirectUri));
    }

    private static ClaimsPrincipal UserWithRoles(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim(ClaimTypes.Name, "test@example.com"));
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }
}
