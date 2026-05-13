using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class TemplateFieldPermission : BaseEntity, ISoftAnnulled
{
    public Guid TemplateFieldId { get; set; }

    public TemplateField TemplateField { get; set; } = null!;

    public string RoleName { get; set; } = string.Empty;

    public FieldAccessLevel AccessLevel { get; set; } = FieldAccessLevel.None;

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
