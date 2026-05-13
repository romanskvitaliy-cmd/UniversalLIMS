using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Laboratory;

public class SampleResultValue : BaseEntity, ISoftAnnulled
{
    public Guid SampleId { get; set; }

    public Sample Sample { get; set; } = null!;

    public Guid DataFieldId { get; set; }

    public DataField DataField { get; set; } = null!;

    public string? StoredValue { get; set; }

    public DateTime EnteredAtUtc { get; set; }

    public string EnteredByUserId { get; set; } = string.Empty;

    public Guid? EquipmentId { get; set; }

    public Equipment? Equipment { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
