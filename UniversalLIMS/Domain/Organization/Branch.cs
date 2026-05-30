using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Organization;

public class Branch : BaseEntity, ISoftAnnulled
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public BranchKind Kind { get; set; } = BranchKind.Laboratory;

    /// <summary>Для LAB-філій: пул експертів (напр. LAB-BACT-ZHY → EXP-ZHY).</summary>
    public Guid? ExpertBranchId { get; set; }

    public Branch? ExpertBranch { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
