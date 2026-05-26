using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Registration;

/// <summary>
/// Типовий текст для поля (тег / DataField) у межах філії. Не змінює збережені значення замовлень.
/// </summary>
public sealed class FieldTextLibraryEntry : BaseEntity, ISoftAnnulled
{
    public Guid BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    /// <summary>Семантичний ключ поля; пріоритетний для бібліотеки між версіями шаблону.</summary>
    public Guid? DataFieldId { get; set; }

    public DataField? DataField { get; set; }

    /// <summary>Fallback, якщо поле не прив’язане до DataField.</summary>
    public string? NormalizedTag { get; set; }

    public string Body { get; set; } = string.Empty;

    public string NormalizedBodyHash { get; set; } = string.Empty;

    public string? ShortLabel { get; set; }

    public int UsageCount { get; set; }

    public int SortOrder { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
