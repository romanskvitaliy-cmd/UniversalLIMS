using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Registration;

/// <summary>Сегмент поля для UI заповнення PDF з урахуванням RBAC.</summary>
public sealed class PdfWorkspaceFillSegmentDto
{
    public Guid SegmentId { get; init; }

    public Guid TemplateFieldId { get; init; }

    public string Tag { get; init; } = string.Empty;

    public string? Title { get; init; }

    public Guid? DataFieldId { get; init; }

    public string? DataFieldKey { get; init; }

    public int Sequence { get; init; }

    public int PageNumber { get; init; }

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public bool AllowMultiline { get; init; }

    public decimal TextOffsetX { get; init; }

    public decimal TextOffsetY { get; init; }

    public decimal? FontSize { get; init; }

    public string? FontName { get; init; }

    public string? HorizontalAlignment { get; init; }

    public string? VerticalAlignment { get; init; }

    public string TextAlignment { get; init; } = "Left";

    public decimal? LineHeight { get; init; }

    public string? SvgPathData { get; init; }

    public bool IsPrimary { get; init; } = true;

    public byte[]? SegmentRowVersion { get; init; }

    public FieldAccessLevel AccessLevel { get; init; }

    public bool CanWrite => AccessLevel >= FieldAccessLevel.Write;
}
