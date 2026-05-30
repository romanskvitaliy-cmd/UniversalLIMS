using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Application.Organization;

public static class BranchKindCatalog
{
    public static string GetDisplayNameUk(BranchKind kind) =>
        kind switch
        {
            BranchKind.Registration => "Реєстратура",
            BranchKind.Laboratory => "Лабораторія",
            BranchKind.Expert => "Експертиза",
            BranchKind.Mixed => "Lab + Expert",
            _ => kind.ToString()
        };

    public static string GetBadgeClass(BranchKind kind) =>
        kind switch
        {
            BranchKind.Registration => "lims-branch-kind--registration",
            BranchKind.Laboratory => "lims-branch-kind--laboratory",
            BranchKind.Expert => "lims-branch-kind--expert",
            BranchKind.Mixed => "lims-branch-kind--mixed",
            _ => "lims-branch-kind--default"
        };

    public static IReadOnlyList<BranchKind> All { get; } =
    [
        BranchKind.Registration,
        BranchKind.Laboratory,
        BranchKind.Expert,
        BranchKind.Mixed
    ];
}
