using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Domain.Audit;
using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Infrastructure.Persistence.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> TechnicalPropertyNames =
    [
        nameof(IAuditableEntity.CreatedAtUtc),
        nameof(IAuditableEntity.CreatedByUserId),
        nameof(IAuditableEntity.UpdatedAtUtc),
        nameof(IAuditableEntity.UpdatedByUserId),
        nameof(BaseEntity.RowVersion)
    ];

    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISystemOperationContext _systemOperationContext;

    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ISystemOperationContext systemOperationContext)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _systemOperationContext = systemOperationContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var timestampUtc = _dateTimeProvider.UtcNow;
        var auditLogs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            ApplyAuditableFields(entry, timestampUtc);

            var auditLog = CreateAuditLog(entry, timestampUtc);
            if (auditLog is not null)
            {
                auditLogs.Add(auditLog);
            }
        }

        if (auditLogs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditLogs);
        }
    }

    private void ApplyAuditableFields(EntityEntry entry, DateTime timestampUtc)
    {
        if (entry.Entity is not IAuditableEntity auditableEntity)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            auditableEntity.CreatedAtUtc = timestampUtc;
            auditableEntity.CreatedByUserId = _currentUserService.UserId;
        }

        if (entry.State == EntityState.Modified)
        {
            auditableEntity.UpdatedAtUtc = timestampUtc;
            auditableEntity.UpdatedByUserId = _currentUserService.UserId;
        }
    }

    private AuditLog? CreateAuditLog(EntityEntry entry, DateTime timestampUtc)
    {
        var action = ResolveAuditAction(entry);
        if (action is null)
        {
            return null;
        }

        var changedProperties = GetChangedProperties(entry).ToList();
        var oldValues = GetOldValues(entry, changedProperties);
        var newValues = GetNewValues(entry, changedProperties);

        return new AuditLog
        {
            UserId = _currentUserService.UserId,
            UserName = _systemOperationContext.IsActive ? "System" : _currentUserService.UserName,
            UserFullName = _systemOperationContext.IsActive ? "Системна операція" : _currentUserService.UserFullName,
            BranchId = _currentUserService.BranchId,
            Action = _systemOperationContext.IsActive && action.Value == AuditAction.Created
                ? AuditAction.Seeded
                : action.Value,
            EntityName = entry.Entity.GetType().Name,
            EntityId = GetPrimaryKeyValue(entry),
            ChangedProperties = JsonSerializer.Serialize(changedProperties, JsonOptions),
            OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            Reason = _systemOperationContext.IsActive
                ? _systemOperationContext.OperationName
                : entry.Entity is ISoftAnnulled softAnnulled ? softAnnulled.AnnulmentReason : null,
            CorrelationId = _currentUserService.CorrelationId,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            TimestampUtc = timestampUtc
        };
    }

    private static AuditAction? ResolveAuditAction(EntityEntry entry)
    {
        return entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Modified when IsAnnulment(entry) => AuditAction.Annulled,
            EntityState.Modified => AuditAction.Updated,
            EntityState.Deleted => AuditAction.Annulled,
            _ => null
        };
    }

    private static bool IsAnnulment(EntityEntry entry)
    {
        return entry.Entity is ISoftAnnulled &&
            entry.Properties.Any(property =>
                property.Metadata.Name == nameof(ISoftAnnulled.IsAnnulled) &&
                property.CurrentValue is true &&
                !Equals(property.OriginalValue, property.CurrentValue));
    }

    private static IEnumerable<string> GetChangedProperties(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return entry.Properties
                .Where(property => !TechnicalPropertyNames.Contains(property.Metadata.Name))
                .Select(property => property.Metadata.Name);
        }

        return entry.Properties
            .Where(property => property.IsModified && !TechnicalPropertyNames.Contains(property.Metadata.Name))
            .Select(property => property.Metadata.Name);
    }

    private static Dictionary<string, object?> GetOldValues(EntityEntry entry, IReadOnlyCollection<string> changedProperties)
    {
        if (entry.State == EntityState.Added)
        {
            return [];
        }

        return entry.Properties
            .Where(property => changedProperties.Contains(property.Metadata.Name))
            .ToDictionary(property => property.Metadata.Name, property => property.OriginalValue);
    }

    private static Dictionary<string, object?> GetNewValues(EntityEntry entry, IReadOnlyCollection<string> changedProperties)
    {
        if (entry.State == EntityState.Deleted)
        {
            return [];
        }

        return entry.Properties
            .Where(property => changedProperties.Contains(property.Metadata.Name))
            .ToDictionary(property => property.Metadata.Name, property => property.CurrentValue);
    }

    private static string? GetPrimaryKeyValue(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return null;
        }

        var keyValues = primaryKey.Properties
            .Select(property => entry.Property(property.Name).CurrentValue?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(",", keyValues);
    }
}
