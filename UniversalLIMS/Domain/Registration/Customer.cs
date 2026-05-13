using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Registration;

public class Customer : BaseEntity, ISoftAnnulled
{
    public CustomerKind Kind { get; set; } = CustomerKind.Individual;

    public string FullName { get; set; } = string.Empty;

    public string? OrganizationName { get; set; }

    public string? ContactPhone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Edrpou { get; set; }

    public string? Rnokpp { get; set; }

    public string? Notes { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }

    public ICollection<Order> Orders { get; set; } = [];
}
