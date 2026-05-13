using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Domain.Laboratory;

public class Equipment : BaseEntity, ISoftAnnulled
{
    public string Code { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string? SerialNumber { get; set; }

    public Guid? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
