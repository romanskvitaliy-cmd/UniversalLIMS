namespace UniversalLIMS.Application.Registration;

public sealed class FieldTextLibraryEntryDto
{
    public Guid Id { get; init; }

    public string Body { get; init; } = string.Empty;

    public string? ShortLabel { get; init; }

    public int UsageCount { get; init; }

    public string RowVersionBase64 { get; init; } = string.Empty;
}

public sealed class FieldTextLibraryListResult
{
    public IReadOnlyList<FieldTextLibraryEntryDto> Entries { get; init; } = [];

    public int TotalCount { get; init; }
}

public sealed class FieldTextLibraryUpsertRequest
{
    public Guid? OrderId { get; init; }

    public Guid TemplateFieldId { get; init; }

    public string Body { get; init; } = string.Empty;

    public string? ShortLabel { get; init; }
}

public sealed class FieldTextLibraryUpdateRequest
{
    public Guid? OrderId { get; init; }

    public Guid TemplateFieldId { get; init; }

    public string Body { get; init; } = string.Empty;

    public string? ShortLabel { get; init; }

    public string? RowVersionBase64 { get; init; }
}

public sealed class FieldTextLibraryMutationResult
{
    public FieldTextLibraryEntryDto Entry { get; init; } = null!;

    public bool Created { get; init; }

    public string Message { get; init; } = string.Empty;
}
