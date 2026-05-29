using UniversalLIMS.Application.Organization;

namespace UniversalLIMS.Application.Organization.Abstractions;

public interface IBranchService
{
    Task<IReadOnlyList<BranchListItemDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<BranchEditDto?> GetForEditAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid branchId, UpdateBranchRequest request, CancellationToken cancellationToken = default);

    Task AnnulAsync(Guid branchId, string reason, CancellationToken cancellationToken = default);
}
