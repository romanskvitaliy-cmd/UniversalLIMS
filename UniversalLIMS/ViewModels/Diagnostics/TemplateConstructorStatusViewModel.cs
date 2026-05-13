namespace UniversalLIMS.ViewModels.Diagnostics;

public sealed class TemplateConstructorStatusViewModel
{
    public int TemplateCount { get; set; }

    public int TemplateVersionCount { get; set; }

    public int PublishedTemplateVersionCount { get; set; }

    public int AnnulledTemplateVersionCount { get; set; }

    public int TemplateFieldCount { get; set; }

    public int UnmappedRequiredFieldCount { get; set; }

    public int TemplateFieldPermissionCount { get; set; }

    public int TemplatesWithoutPublishedVersionCount { get; set; }

    public string? LatestTemplateCode { get; set; }

    public IReadOnlyList<string> AppliedMigrations { get; set; } = [];

    public IReadOnlyList<string> PendingMigrations { get; set; } = [];

    public bool HasPendingMigrations => PendingMigrations.Count > 0;
}
