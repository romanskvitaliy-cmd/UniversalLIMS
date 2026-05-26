using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.ViewModels.PdfWorkspace;

public sealed class PdfWorkspaceVersionListViewModel
{
    public List<PdfWorkspaceVersionListItemViewModel> Versions { get; set; } = [];
}

public sealed class PdfWorkspaceVersionListItemViewModel
{
    public Guid TemplateVersionId { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; }
}

public sealed class PdfWorkspaceFillViewModel
{
    public Guid TemplateVersionId { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public Guid? OrderId { get; set; }

    public string? PdfPreviewUrl { get; set; }

    public Dictionary<string, string?> SavedValuesByKey { get; set; } = new(StringComparer.Ordinal);

    public List<PdfWorkspaceFillSegmentViewModel> Segments { get; set; } = [];

    public string? ActiveRoleCode { get; set; }

    public int WritableFieldCount { get; set; }

    public int ReadOnlyFieldCount { get; set; }

    /// <summary>Сегментів у layout версії (до фільтра RBAC).</summary>
    public int LayoutSegmentCount { get; set; }

    /// <summary>У layout є поля, але активна роль не має Read/Write на жодне.</summary>
    public bool IsBlockedByPermissions =>
        LayoutSegmentCount > 0 && Segments.Count == 0;

    public bool HasNoLayout => LayoutSegmentCount == 0;

    public string? ActiveRoleDisplayName { get; set; }

    public string? FieldPermissionsUrl { get; set; }
}

public sealed class PdfWorkspaceFillSegmentViewModel
{
    public Guid SegmentId { get; set; }

    public Guid TemplateFieldId { get; set; }

    public string Tag { get; set; } = string.Empty;

    public string? Title { get; set; }

    public Guid? DataFieldId { get; set; }

    public string? DataFieldKey { get; set; }

    public int Sequence { get; set; }

    public int PageNumber { get; set; }

    public decimal PositionX { get; set; }

    public decimal PositionY { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public bool AllowMultiline { get; set; }

    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }

    public decimal? FontSize { get; set; }

    public string? FontName { get; set; }

    public string? HorizontalAlignment { get; set; }

    public string? VerticalAlignment { get; set; }

    public string TextAlignment { get; set; } = "Left";

    public decimal? LineHeight { get; set; }

    public string? SvgPathData { get; set; }

    public bool IsPrimary { get; set; } = true;

    public string? SegmentRowVersionBase64 { get; set; }

    public FieldAccessLevel AccessLevel { get; set; } = FieldAccessLevel.None;

    public bool CanWrite => AccessLevel >= FieldAccessLevel.Write;
}
