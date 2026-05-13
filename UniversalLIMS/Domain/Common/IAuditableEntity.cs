namespace UniversalLIMS.Domain.Common;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    string? CreatedByUserId { get; set; }

    DateTime? UpdatedAtUtc { get; set; }

    string? UpdatedByUserId { get; set; }
}
