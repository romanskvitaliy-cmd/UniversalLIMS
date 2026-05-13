namespace UniversalLIMS.ViewModels.Diagnostics;

public sealed class FoundationStatusViewModel
{
    public int RoleCount { get; set; }

    public int BranchCount { get; set; }

    public int DataFieldCount { get; set; }

    public int AuditLogCount { get; set; }

    public int ActiveDiagnosticDataFieldCount { get; set; }

    public int AnnulledDiagnosticDataFieldCount { get; set; }

    public string? LatestDiagnosticDataFieldKey { get; set; }

    public IReadOnlyList<string> AppliedMigrations { get; set; } = [];

    public IReadOnlyList<string> PendingMigrations { get; set; } = [];

    public bool HasPendingMigrations => PendingMigrations.Count > 0;
}
