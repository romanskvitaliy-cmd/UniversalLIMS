namespace UniversalLIMS.Domain.Common;

public abstract class BaseEntity : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
