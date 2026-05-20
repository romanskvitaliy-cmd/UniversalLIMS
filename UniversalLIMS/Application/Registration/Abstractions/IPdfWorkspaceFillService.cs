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

    /// <summary>Тестовий PDF для режиму калібрування (зразкові тексти по полях).</summary>
    Task<byte[]> GenerateCalibrationPreviewPdfAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, string> sampleTextsByFieldId,
        CancellationToken cancellationToken = default);
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
