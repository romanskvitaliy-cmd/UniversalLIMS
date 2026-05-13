using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class TemplateFieldSegment : BaseEntity, ISoftAnnulled
{
    public Guid TemplateFieldId { get; set; }

    public TemplateField TemplateField { get; set; } = null!;

    public int Sequence { get; set; }

    public int PageNumber { get; set; } = 1;

    public decimal PositionX { get; set; }

    public decimal PositionY { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public bool IsPrimary { get; set; }

    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;

    public string? FontName { get; set; }

    public decimal? FontSize { get; set; }

    public decimal? LineHeight { get; set; }

    public string? SvgPathData { get; set; }

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
