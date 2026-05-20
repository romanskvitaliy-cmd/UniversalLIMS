namespace UniversalLIMS.Infrastructure.Registration;

internal static class OrderFieldValueSelection
{
    public static Dictionary<Guid, string?> ResolveByDataFieldId(
        IEnumerable<OrderFieldValueCandidate> values)
    {
        return values
            .GroupBy(value => value.DataFieldId)
            .Select(group => new
            {
                group.Key,
                StoredValue = SelectStoredValue(group)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.StoredValue))
            .ToDictionary(item => item.Key, item => item.StoredValue);
    }

    public static string? SelectStoredValue(IEnumerable<OrderFieldValueCandidate> values)
    {
        var candidates = values
            .Where(value => !string.IsNullOrWhiteSpace(value.StoredValue))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var orderLevel = candidates
            .Where(value => value.SampleId is null)
            .OrderByDescending(value => value.UpdatedAtUtc ?? value.CreatedAtUtc)
            .FirstOrDefault();

        if (orderLevel is not null)
        {
            return orderLevel.StoredValue;
        }

        return candidates
            .OrderByDescending(value => value.UpdatedAtUtc ?? value.CreatedAtUtc)
            .First()
            .StoredValue;
    }
}

internal sealed record OrderFieldValueCandidate(
    Guid DataFieldId,
    Guid? SampleId,
    string? StoredValue,
    DateTime? UpdatedAtUtc,
    DateTime CreatedAtUtc);
