namespace UniversalLIMS.Infrastructure.Registration;

internal static class OrderFieldValueSelection
{
    public static Dictionary<Guid, string?> ResolveByDataFieldId(
        IEnumerable<OrderFieldValueCandidate> values,
        Guid? preferredSampleId = null)
    {
        return values
            .GroupBy(value => value.DataFieldId)
            .Select(group => new
            {
                group.Key,
                StoredValue = SelectStoredValue(group, preferredSampleId)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.StoredValue))
            .ToDictionary(item => item.Key, item => item.StoredValue);
    }

    public static string? SelectStoredValue(
        IEnumerable<OrderFieldValueCandidate> values,
        Guid? preferredSampleId = null)
    {
        var candidates = values
            .Where(value => !string.IsNullOrWhiteSpace(value.StoredValue))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (preferredSampleId is Guid sampleId)
        {
            var sampleLevel = candidates
                .Where(value => value.SampleId == sampleId)
                .OrderByDescending(value => value.UpdatedAtUtc ?? value.CreatedAtUtc)
                .FirstOrDefault();

            if (sampleLevel is not null)
            {
                return sampleLevel.StoredValue;
            }
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
