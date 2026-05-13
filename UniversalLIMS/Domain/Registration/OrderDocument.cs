using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Registration;

public class OrderDocument : BaseEntity, ISoftAnnulled
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid SampleId { get; set; }

    public Sample Sample { get; set; } = null!;

    public Guid TemplateId { get; set; }

    public Template Template { get; set; } = null!;

    public Guid TemplateVersionId { get; set; }

    public TemplateVersion TemplateVersion { get; set; } = null!;

    public Guid TargetBranchId { get; set; }

    public Branch TargetBranch { get; set; } = null!;

    public OrderDocumentStatus Status { get; set; } = OrderDocumentStatus.Pending;

    public DateTime? SentToLabAtUtc { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
