using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Registration;

public class InvestigationType : BaseEntity, ISoftAnnulled
{
    public string Code { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string? DescriptionUk { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }

    public ICollection<InvestigationTypeTemplate> InvestigationTypeTemplates { get; set; } = [];

    public ICollection<Sample> Samples { get; set; } = [];
}
