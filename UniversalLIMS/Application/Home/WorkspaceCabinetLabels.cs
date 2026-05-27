using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Application.Home;

public static class WorkspaceCabinetLabels
{
    public static string GetBackLinkLabel(string? roleCode) =>
        roleCode switch
        {
            LimsRoles.Registrar => "← На головну",
            LimsRoles.LaboratoryTechnician => "← На головну",
            LimsRoles.Specialist => "← На головну",
            LimsRoles.SystemAdministrator => "← На головну",
            _ => "← На головну"
        };

    public static bool IsOperationalRole(string? roleCode) =>
        roleCode is LimsRoles.Registrar
            or LimsRoles.LaboratoryTechnician
            or LimsRoles.Specialist;
}
