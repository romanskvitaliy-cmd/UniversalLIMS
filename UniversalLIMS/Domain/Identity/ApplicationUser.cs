using Microsoft.AspNetCore.Identity;
using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Domain.Identity;

public class ApplicationUser : IdentityUser, IAuditableEntity
{
    public string FullName { get; set; } = string.Empty;

    public string? Position { get; set; }

    public Guid? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedByUserId { get; set; }
}
