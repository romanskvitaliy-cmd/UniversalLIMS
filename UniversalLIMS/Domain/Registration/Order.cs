using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Domain.Registration;

public class Order : BaseEntity, ISoftAnnulled
{
    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public Guid BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public string? ReferralNumber { get; set; }

    public DateTime? RegisteredAtUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }

    public ICollection<Sample> Samples { get; set; } = [];

    public ICollection<OrderDocument> OrderDocuments { get; set; } = [];

    public ICollection<OrderFieldValue> FieldValues { get; set; } = [];

    public ICollection<OrderFieldLinkGroup> FieldLinkGroups { get; set; } = [];
}
