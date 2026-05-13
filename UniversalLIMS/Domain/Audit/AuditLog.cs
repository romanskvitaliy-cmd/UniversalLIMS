namespace UniversalLIMS.Domain.Audit;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? UserFullName { get; set; }

    public Guid? BranchId { get; set; }

    public AuditAction Action { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string ChangedProperties { get; set; } = "[]";

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? Reason { get; set; }

    public string? CorrelationId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime TimestampUtc { get; set; }
}
