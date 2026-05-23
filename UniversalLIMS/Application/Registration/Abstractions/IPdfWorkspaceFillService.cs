namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IPdfWorkspaceFillService
{
    Task<PdfWorkspaceSaveResult> SaveValuesAsync(
        Guid templateVersionId,
        Guid? orderId,
        IReadOnlyList<PdfWorkspaceFieldValueDto> values,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateFilledPdfAsync(
        Guid templateVersionId,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string?>> GetSavedValuesByKeyAsync(
        Guid orderId,
        Guid templateVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF preview калібрування: рендер із координат і тексту з клієнта (WYSIWYG),
    /// без залежності від збереження в БД.
    /// </summary>
    Task<byte[]> GenerateCalibrationPreviewPdfAsync(
        Guid templateVersionId,
        IReadOnlyList<CalibrationPreviewOverlayDto> overlays,
        CancellationToken cancellationToken = default);
}

/// <summary>Один overlay-сегмент для preview калібрування (координати в preview-просторі конструктора).</summary>
public sealed class CalibrationPreviewOverlayDto
{
    public Guid? FieldId { get; init; }

    public string? Tag { get; init; }

    public string Text { get; init; } = string.Empty;

    public int PageNumber { get; init; } = 1;

    public decimal PositionX { get; init; }

    public decimal PositionY { get; init; }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public decimal TextOffsetX { get; init; }

    public decimal TextOffsetY { get; init; }

    public decimal? FontSize { get; init; }

    public string? FontName { get; init; }

    public string? HorizontalAlignment { get; init; }

    public string? VerticalAlignment { get; init; }
}

public sealed class PdfWorkspaceFieldValueDto
{
    public Guid? TemplateFieldId { get; init; }

    public string? Value { get; init; }
}

public sealed class PdfWorkspaceSaveResult
{
    public Guid OrderId { get; init; }

    public int Received { get; init; }

    public int Mapped { get; init; }

    public int Saved { get; init; }

    public int SkippedUnmapped { get; init; }

    public int SkippedEmpty { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<PdfWorkspaceSaveFieldFailure> FailedFields { get; init; } = [];
}

public sealed class PdfWorkspaceSaveFieldFailure
{
    public Guid? TemplateFieldId { get; init; }

    public string Reason { get; init; } = string.Empty;
}
