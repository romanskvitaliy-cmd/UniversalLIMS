using System.ComponentModel.DataAnnotations.Schema;
using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class TemplateField : BaseEntity, ISoftAnnulled
{
    public Guid TemplateVersionId { get; set; }

    public TemplateVersion TemplateVersion { get; set; } = null!;

    public string Tag { get; set; } = string.Empty;

    public string NormalizedTag { get; set; } = string.Empty;

    public string? Title { get; set; }

    public WordContentControlType WordControlType { get; set; } = WordContentControlType.Text;

    public FieldType FieldType { get; set; } = FieldType.Text;

    public TemplateFieldStatus Status { get; set; } = TemplateFieldStatus.NewTag;

    public Guid? DataFieldId { get; set; }

    public DataField? DataField { get; set; }

    public bool IsRequired { get; set; } = true;

    public int SortOrder { get; set; }

    public int? EstimatedCapacityChars { get; set; }

    public int? MaxLines { get; set; } = 1;

    public bool AllowMultiline { get; set; }

    public FieldOverflowPolicy OverflowPolicy { get; set; } = FieldOverflowPolicy.Block;

    /// <summary>Горизонтальний зсув тексту overlay у пікселях preview конструктора (до масштабу PDF).</summary>
    public decimal TextOffsetX { get; set; }

    /// <summary>Вертикальний зсув тексту overlay у пікселях preview конструктора (до масштабу PDF).</summary>
    public decimal TextOffsetY { get; set; }

    [Obsolete("Use Segments instead.")]
    [NotMapped]
    public int? PageNumber
    {
        get => GetPrimarySegment()?.PageNumber;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            EnsurePrimarySegment().PageNumber = Math.Max(1, value.Value);
        }
    }

    [Obsolete("Use Segments instead.")]
    [NotMapped]
    public decimal? PositionX
    {
        get => GetPrimarySegment()?.PositionX;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            EnsurePrimarySegment().PositionX = Math.Max(0, value.Value);
        }
    }

    [Obsolete("Use Segments instead.")]
    [NotMapped]
    public decimal? PositionY
    {
        get => GetPrimarySegment()?.PositionY;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            EnsurePrimarySegment().PositionY = Math.Max(0, value.Value);
        }
    }

    [Obsolete("Use Segments instead.")]
    [NotMapped]
    public decimal? Width
    {
        get => GetPrimarySegment()?.Width;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            EnsurePrimarySegment().Width = Math.Max(20, value.Value);
        }
    }

    [Obsolete("Use Segments instead.")]
    [NotMapped]
    public decimal? Height
    {
        get => GetPrimarySegment()?.Height;
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            EnsurePrimarySegment().Height = Math.Max(14, value.Value);
        }
    }

    public DateTime DetectedAtUtc { get; set; }

    public DateTime? LastMappedAtUtc { get; set; }

    public string? LastMappedByUserId { get; set; }

    public virtual ICollection<TemplateFieldSegment> Segments { get; set; } = new List<TemplateFieldSegment>();

    public ICollection<TemplateFieldPermission> Permissions { get; set; } = [];

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }

    public decimal GetTotalWidth() => Segments.Sum(segment => segment.Width);

    public TemplateFieldSegment? GetPrimarySegment() =>
        Segments.FirstOrDefault(segment => segment.IsPrimary) ??
        Segments.OrderBy(segment => segment.Sequence).FirstOrDefault();

    public TemplateFieldSegment EnsurePrimarySegment()
    {
        var primarySegment = GetPrimarySegment();
        if (primarySegment is not null)
        {
            return primarySegment;
        }

        primarySegment = new TemplateFieldSegment
        {
            TemplateFieldId = Id,
            Sequence = 1,
            PageNumber = 1,
            PositionX = 24,
            PositionY = 24,
            Width = 220,
            Height = 28,
            IsPrimary = true
        };

        Segments.Add(primarySegment);
        return primarySegment;
    }
}
