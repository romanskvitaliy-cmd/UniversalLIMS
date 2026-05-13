using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Domain.Registration;

public class Sample : BaseEntity, ISoftAnnulled
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string Number { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    public Guid InvestigationTypeId { get; set; }

    public InvestigationType InvestigationType { get; set; } = null!;

    public SampleStatus Status { get; set; } = SampleStatus.Registered;

    public DateTime? RoutedAtUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }

    public ICollection<OrderDocument> OrderDocuments { get; set; } = [];

    public ICollection<OrderFieldValue> FieldValues { get; set; } = [];

    public ICollection<SampleResultValue> ResultValues { get; set; } = [];
}
