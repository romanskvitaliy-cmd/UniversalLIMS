using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class RegistrationNotificationService : IRegistrationNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RegistrationNotificationService(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<IncomingRegistrarNotificationDto>> GetReadyForPickupSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var samplesQuery = _context.Samples
            .AsNoTracking()
            .Where(sample =>
                !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && !sample.Order.Customer.IsAnnulled
                && sample.DeliveryStatus == SampleDeliveryStatus.ReadyForPickup
                && sample.ReadyForPickupAtUtc.HasValue
                && sample.ReadyForPickupAtUtc.Value > sinceUtc
                && _context.ExpertConclusionReviews.Any(review =>
                    review.SampleId == sample.Id
                    && review.Status == ExpertConclusionStatus.Approved));

        if (_currentUser.BranchId is Guid branchId)
        {
            samplesQuery = samplesQuery.Where(sample => sample.Order.BranchId == branchId);
        }

        return await samplesQuery
            .OrderBy(sample => sample.ReadyForPickupAtUtc)
            .Take(20)
            .Select(sample => new IncomingRegistrarNotificationDto
            {
                SampleId = sample.Id,
                SampleNumber = sample.Number,
                CustomerFullName = sample.Order.Customer.FullName,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                ReadyForPickupAtUtc = sample.ReadyForPickupAtUtc!.Value
            })
            .ToListAsync(cancellationToken);
    }
}
