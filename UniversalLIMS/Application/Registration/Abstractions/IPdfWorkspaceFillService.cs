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
}

public sealed class PdfWorkspaceFieldValueDto
{
    public Guid? TemplateFieldId { get; init; }

    public string? Value { get; init; }
}

public sealed class PdfWorkspaceSaveResult
{
    public Guid OrderId { get; init; }

    public int SavedCount { get; init; }

    public int TotalFields { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> UnmatchedFields { get; init; } = [];
}
