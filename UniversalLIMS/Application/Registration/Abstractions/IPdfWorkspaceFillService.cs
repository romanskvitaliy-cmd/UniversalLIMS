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
    public string Key { get; init; } = string.Empty;

    public string? Value { get; init; }

    public string? Tag { get; init; }

    public string? DataFieldKey { get; init; }

    public int? Sequence { get; init; }
}

public sealed class PdfWorkspaceSaveResult
{
    public Guid OrderId { get; init; }

    public int SavedCount { get; init; }

    public int TotalFields { get; init; }

    public IReadOnlyList<string> MatchedFields { get; init; } = [];

    public IReadOnlyList<string> UnmatchedFields { get; init; } = [];

    public IReadOnlyList<string> SkippedKeys { get; init; } = [];

    public IReadOnlyList<PdfWorkspaceSaveMatchLogEntry> MatchLog { get; init; } = [];
}

public sealed class PdfWorkspaceSaveMatchLogEntry
{
    public string ClientKey { get; init; } = string.Empty;

    public string? ClientTag { get; init; }

    public string? ClientDataFieldKey { get; init; }

    public string? MatchedTemplateTag { get; init; }

    public string? MatchedStorageKey { get; init; }

    public string MatchStrategy { get; init; } = string.Empty;

    public bool IsMatched { get; init; }
}
