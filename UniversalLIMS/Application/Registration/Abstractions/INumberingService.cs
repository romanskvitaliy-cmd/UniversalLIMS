namespace UniversalLIMS.Application.Registration.Abstractions;

public interface INumberingService
{
    Task<string> AssignSampleNumberAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<string> AssignReferralNumberAsync(Guid branchId, CancellationToken cancellationToken = default);
}
