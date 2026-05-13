namespace UniversalLIMS.Application.Security;

public static class LimsRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string Registrar = "Registrar";
    public const string LaboratoryTechnician = "LaboratoryTechnician";
    public const string Specialist = "Specialist";

    public static readonly string[] All =
    [
        SystemAdministrator,
        Registrar,
        LaboratoryTechnician,
        Specialist
    ];
}
