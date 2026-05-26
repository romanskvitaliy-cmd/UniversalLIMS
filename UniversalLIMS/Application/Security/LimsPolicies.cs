namespace UniversalLIMS.Application.Security;

public static class LimsPolicies
{
    public const string ManageSystem = "ManageSystem";
    public const string RegisterSamples = "RegisterSamples";
    public const string EnterLaboratoryResults = "EnterLaboratoryResults";
    public const string ApproveConclusions = "ApproveConclusions";

    /// <summary>Заповнення PDF-шаблонів (реєстратура, лабораторія, експерт).</summary>
    public const string FillPdfWorkspace = "FillPdfWorkspace";
}
