using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Registration;

public class OrderFieldValue : BaseEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public Guid? SampleId { get; set; }

    public Sample? Sample { get; set; }

    public Guid DataFieldId { get; set; }

    public DataField DataField { get; set; } = null!;

    public string? StoredValue { get; set; }
}
