using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Registration;

public class InvestigationTypeTemplate
{
    public Guid InvestigationTypeId { get; set; }

    public InvestigationType InvestigationType { get; set; } = null!;

    public Guid TemplateId { get; set; }

    public Template Template { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
