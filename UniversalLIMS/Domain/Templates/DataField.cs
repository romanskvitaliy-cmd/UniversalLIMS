using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class DataField : BaseEntity, ISoftAnnulled
{
    public string Key { get; set; } = string.Empty;

    public string DisplayNameUk { get; set; } = string.Empty;

    public string? DescriptionUk { get; set; }

    public DataFieldType FieldType { get; set; }

    public DataFieldScope Scope { get; set; }

    public string? Unit { get; set; }

    public int? MaxLength { get; set; }

    public string? Format { get; set; }

    public string? ValidationRegex { get; set; }

    public string? ExampleValue { get; set; }

    public bool IsRequired { get; set; }

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
