namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IPdfWorkspaceFillService
{
    Task<PdfWorkspaceSaveResult> SaveValuesAsync(
        Guid templateVersionId,
        Guid? orderId,
        Guid? orderDocumentId,
        IReadOnlyList<PdfWorkspaceFieldValueDto> values,
        CancellationToken cancellationToken = default);

    Task<byte[]> GenerateFilledPdfAsync(
        Guid templateVersionId,
        Guid orderId,
        Guid? orderDocumentId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string?>> GetSavedValuesByKeyAsync(
        Guid orderId,
        Guid templateVersionId,
        Guid? orderDocumentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Сегменти для Fill UI: лише поля з доступом Read+ для активної ролі (TemplateFieldPermission).
    /// </summary>
    Task<IReadOnlyList<PdfWorkspaceFillSegmentDto>> GetFillSegmentsAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>Кількість сегментів у layout версії (без фільтра RBAC).</summary>
    Task<int> GetLayoutSegmentCountAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF preview калібрування: WYSIWYG лише з <see cref="PreviewCalibrationRequest.Fields"/>.
    /// Layout і текст з БД не читаються.
    /// </summary>
    Task<CalibrationPreviewPdfResult> GenerateCalibrationPreviewAsync(
        PreviewCalibrationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CalibrationPreviewPdfResult(
    byte[] PdfBytes,
    int SegmentsDrawn,
    int SegmentsSkippedEmpty,
    int SegmentsSkippedPage,
    int PdfPageCount);

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
