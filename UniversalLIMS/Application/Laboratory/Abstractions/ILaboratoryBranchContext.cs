using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryBranchContext
{
    Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default);

    Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default);
}

public sealed class LaboratoryBranchContextState
{
    public bool CanSelectBranch { get; init; }

    public Guid? ActiveBranchId { get; init; }

    public IReadOnlyList<BranchOptionDto> Branches { get; init; } = [];
}
