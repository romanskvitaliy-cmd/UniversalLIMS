using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Tests.Home;

public sealed class WorkspaceCabinetLabelsTests
{
    [Theory]
    [InlineData(LimsRoles.Registrar, "← На головну")]
    [InlineData(LimsRoles.LaboratoryTechnician, "← На головну")]
    [InlineData(LimsRoles.Specialist, "← На головну")]
    [InlineData(LimsRoles.SystemAdministrator, "← На головну")]
    public void GetBackLinkLabel_ReturnsRoleSpecificText(string roleCode, string expected)
    {
        Assert.Equal(expected, WorkspaceCabinetLabels.GetBackLinkLabel(roleCode));
    }

    [Fact]
    public void IsOperationalRole_RecognizesRegistrarLabAndExpert()
    {
        Assert.True(WorkspaceCabinetLabels.IsOperationalRole(LimsRoles.Registrar));
        Assert.True(WorkspaceCabinetLabels.IsOperationalRole(LimsRoles.LaboratoryTechnician));
        Assert.True(WorkspaceCabinetLabels.IsOperationalRole(LimsRoles.Specialist));
        Assert.False(WorkspaceCabinetLabels.IsOperationalRole(LimsRoles.SystemAdministrator));
    }
}
