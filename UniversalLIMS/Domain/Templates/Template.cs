using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class Template : BaseEntity, ISoftAnnulled
{
    public string Code { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string? DescriptionUk { get; set; }

    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;

    public TemplatePurpose Purpose { get; set; } = TemplatePurpose.Protocol;

    public Guid? CurrentPublishedVersionId { get; set; }

    public TemplateVersion? CurrentPublishedVersion { get; set; }

    public ICollection<TemplateVersion> Versions { get; set; } = [];

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
