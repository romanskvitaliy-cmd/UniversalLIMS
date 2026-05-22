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

    public string Status { get; set; } = string.Empty;
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
}

public sealed class PdfWorkspaceFillSegmentViewModel
{
    public Guid SegmentId { get; set; }

    public Guid TemplateFieldId { get; set; }

    public string Tag { get; set; } = string.Empty;

    public string? Title { get; set; }

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
}
