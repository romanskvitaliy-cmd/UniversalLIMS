using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Registration;

public sealed class OrderFieldLinkMember : BaseEntity
{
    public Guid GroupId { get; set; }

    public OrderFieldLinkGroup Group { get; set; } = null!;

    public Guid TemplateVersionId { get; set; }

    public TemplateVersion TemplateVersion { get; set; } = null!;

    public Guid TemplateFieldId { get; set; }

    public TemplateField TemplateField { get; set; } = null!;
}
