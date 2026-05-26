namespace UniversalLIMS.Application.Registration;

public sealed class OrderFieldMappingPrepareDto
{
    public required IReadOnlyList<OrderFieldMappingTemplateDto> Templates { get; init; }
}

public sealed class OrderFieldMappingTemplateDto
{
    public Guid TemplateVersionId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public IReadOnlyList<OrderFieldMappingFieldDto> Fields { get; init; } = [];
}

public sealed class OrderFieldMappingFieldDto
{
    public Guid TemplateFieldId { get; init; }

    public required string Tag { get; init; }

    public string? Title { get; init; }

    public bool CanRead { get; init; }

    public bool CanWrite { get; init; }
}

public sealed class OrderFieldLinkGroupInput
{
    public string? Label { get; init; }

    public IReadOnlyList<OrderFieldLinkMemberInput> Members { get; init; } = [];
}

public sealed class OrderFieldLinkMemberInput
{
    public Guid TemplateVersionId { get; init; }

    public Guid TemplateFieldId { get; init; }
}

public sealed class OrderSharedFieldValueInput
{
    /// <summary>Індекс групи в масиві <see cref="OrderFieldLinkGroupInput"/> (0-based).</summary>
    public int GroupIndex { get; init; }

    public string? Value { get; init; }
}

public sealed class OrderFieldLinkGroupsDetailDto
{
    public IReadOnlyList<OrderFieldLinkGroupDetailDto> Groups { get; init; } = [];
}

public sealed class OrderFieldLinkGroupDetailDto
{
    public Guid GroupId { get; init; }

    public string? Label { get; init; }

    public int SortOrder { get; init; }

    /// <summary>Спільне значення з OrderFieldValue (якщо є).</summary>
    public string? SharedValue { get; init; }

    public IReadOnlyList<OrderFieldLinkMemberDetailDto> Members { get; init; } = [];
}

public sealed class OrderFieldLinkMemberDetailDto
{
    public Guid TemplateVersionId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public Guid TemplateFieldId { get; init; }

    public required string Tag { get; init; }

    public string? Title { get; init; }

    public string? DataFieldKey { get; init; }
}
