using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Domain.Audit;
using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Identity;

namespace UniversalLIMS.Infrastructure.Persistence.Interceptors;

public sealed class SoftAnnulmentSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SoftAnnulmentSaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplySoftAnnulmentRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftAnnulmentRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftAnnulmentRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Deleted))
        {
            if (entry.Entity is AuditLog)
            {
                throw new InvalidOperationException("Audit trail records cannot be deleted.");
            }

            if (entry.Entity is ISoftAnnulled softAnnulled)
            {
                if (string.IsNullOrWhiteSpace(softAnnulled.AnnulmentReason))
                {
                    throw new InvalidOperationException("Annulment reason is required before a record can be annulled.");
                }

                entry.State = EntityState.Modified;

                softAnnulled.IsAnnulled = true;
                softAnnulled.AnnulledAtUtc = _dateTimeProvider.UtcNow;
                softAnnulled.AnnulledByUserId = _currentUserService.UserId;

                continue;
            }

            if (IsProtectedDomainEntity(entry.Entity))
            {
                throw new InvalidOperationException(
                    $"Physical deletion is forbidden for protected entity '{entry.Entity.GetType().Name}'.");
            }
        }
    }

    private static bool IsProtectedDomainEntity(object entity)
    {
        return entity is ApplicationUser ||
            entity.GetType().Namespace?.StartsWith("UniversalLIMS.Domain", StringComparison.Ordinal) == true;
    }
}
