using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Registration.Abstractions;

namespace UniversalLIMS.Infrastructure.Registration;

internal enum PdfWorkspaceMatchStrategy
{
    None,
    DataFieldKey,
    Tag,
    NormalizedTag,
    Title,
    ResolvedTag,
    ClientKey,
    ClientKeyTagPart
}

internal sealed record PdfWorkspaceTemplateFieldInfo(
    Guid Id,
    string Tag,
    string NormalizedTag,
    string? Title,
    string? DataFieldKey)
{
    public string StorageKey => PdfWorkspaceStorageKey.ForTemplateField(Tag, DataFieldKey);
}

internal sealed class PdfWorkspaceFieldMatchIndex
{
    private readonly Dictionary<string, PdfWorkspaceTemplateFieldInfo> _byDataFieldKey;
    private readonly Dictionary<string, PdfWorkspaceTemplateFieldInfo> _byTag;
    private readonly Dictionary<string, PdfWorkspaceTemplateFieldInfo> _byNormalizedTag;
    private readonly Dictionary<string, PdfWorkspaceTemplateFieldInfo> _byTitle;
    private readonly Dictionary<string, PdfWorkspaceTemplateFieldInfo> _byResolvedTag;
    private readonly IReadOnlyList<PdfWorkspaceTemplateFieldInfo> _fields;

    public PdfWorkspaceFieldMatchIndex(IReadOnlyList<PdfWorkspaceTemplateFieldInfo> fields)
    {
        _fields = fields;
        _byDataFieldKey = new Dictionary<string, PdfWorkspaceTemplateFieldInfo>(StringComparer.OrdinalIgnoreCase);
        _byTag = new Dictionary<string, PdfWorkspaceTemplateFieldInfo>(StringComparer.OrdinalIgnoreCase);
        _byNormalizedTag = new Dictionary<string, PdfWorkspaceTemplateFieldInfo>(StringComparer.OrdinalIgnoreCase);
        _byTitle = new Dictionary<string, PdfWorkspaceTemplateFieldInfo>(StringComparer.OrdinalIgnoreCase);
        _byResolvedTag = new Dictionary<string, PdfWorkspaceTemplateFieldInfo>(StringComparer.OrdinalIgnoreCase);

        var resolvedTagOwners = new Dictionary<string, List<PdfWorkspaceTemplateFieldInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var resolvedTag = PdfWorkspaceFieldKeyResolver.Resolve(field.Tag);
            if (!string.IsNullOrWhiteSpace(resolvedTag))
            {
                if (!resolvedTagOwners.TryGetValue(resolvedTag, out var owners))
                {
                    owners = [];
                    resolvedTagOwners[resolvedTag] = owners;
                }

                owners.Add(field);
            }
        }

        foreach (var field in fields)
        {
            TryAddUnique(_byTag, field.Tag, field);

            if (!string.IsNullOrWhiteSpace(field.NormalizedTag))
            {
                TryAddUnique(_byNormalizedTag, field.NormalizedTag, field);
            }

            if (!string.IsNullOrWhiteSpace(field.Title))
            {
                TryAddUnique(_byTitle, field.Title.Trim(), field);
            }

            if (!string.IsNullOrWhiteSpace(field.DataFieldKey))
            {
                TryAddUnique(_byDataFieldKey, field.DataFieldKey, field);
            }

            var resolvedTag = PdfWorkspaceFieldKeyResolver.Resolve(field.Tag);
            if (!string.IsNullOrWhiteSpace(resolvedTag) &&
                resolvedTagOwners.TryGetValue(resolvedTag, out var owners) &&
                owners.Count == 1)
            {
                TryAddUnique(_byResolvedTag, resolvedTag, field);
            }
        }
    }

    public IReadOnlyList<PdfWorkspaceTemplateFieldInfo> Fields => _fields;

    public (PdfWorkspaceTemplateFieldInfo? Field, PdfWorkspaceMatchStrategy Strategy) Match(PdfWorkspaceFieldValueDto item)
    {
        var candidates = BuildLookupCandidates(item);
        foreach (var (candidate, strategies) in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            foreach (var strategy in strategies)
            {
                var field = strategy switch
                {
                    PdfWorkspaceMatchStrategy.DataFieldKey => TryGet(_byDataFieldKey, candidate),
                    PdfWorkspaceMatchStrategy.Tag => TryGet(_byTag, candidate),
                    PdfWorkspaceMatchStrategy.NormalizedTag => TryGet(_byNormalizedTag, candidate),
                    PdfWorkspaceMatchStrategy.Title => TryGet(_byTitle, candidate),
                    PdfWorkspaceMatchStrategy.ResolvedTag => TryGet(_byResolvedTag, candidate) ?? TryGet(_byDataFieldKey, candidate),
                    PdfWorkspaceMatchStrategy.ClientKey => TryGet(_byTag, candidate)
                        ?? TryGet(_byNormalizedTag, candidate)
                        ?? TryGet(_byDataFieldKey, candidate),
                    PdfWorkspaceMatchStrategy.ClientKeyTagPart => TryGet(_byTag, candidate),
                    _ => null
                };

                if (field is not null)
                {
                    return (field, strategy);
                }
            }
        }

        return (null, PdfWorkspaceMatchStrategy.None);
    }

    private static List<(string Candidate, PdfWorkspaceMatchStrategy[] Strategies)> BuildLookupCandidates(
        PdfWorkspaceFieldValueDto item)
    {
        var list = new List<(string, PdfWorkspaceMatchStrategy[])>();

        if (!string.IsNullOrWhiteSpace(item.Tag))
        {
            var tag = item.Tag.Trim();
            list.Add((tag, [PdfWorkspaceMatchStrategy.Tag, PdfWorkspaceMatchStrategy.NormalizedTag, PdfWorkspaceMatchStrategy.Title]));
            list.Add((PdfWorkspaceFieldKeyResolver.Resolve(tag), [PdfWorkspaceMatchStrategy.ResolvedTag, PdfWorkspaceMatchStrategy.DataFieldKey]));
        }

        if (!string.IsNullOrWhiteSpace(item.DataFieldKey))
        {
            var dataFieldKey = item.DataFieldKey.Trim();
            list.Add((dataFieldKey, [PdfWorkspaceMatchStrategy.DataFieldKey, PdfWorkspaceMatchStrategy.ResolvedTag]));
            var resolvedDataFieldKey = PdfWorkspaceFieldKeyResolver.Resolve(dataFieldKey);
            if (!string.Equals(resolvedDataFieldKey, dataFieldKey, StringComparison.OrdinalIgnoreCase))
            {
                list.Add((resolvedDataFieldKey, [PdfWorkspaceMatchStrategy.ResolvedTag, PdfWorkspaceMatchStrategy.DataFieldKey]));
            }
        }

        var key = item.Key.Trim();
        if (!string.IsNullOrWhiteSpace(key))
        {
            if (TryParseTagSequenceKey(key, out var tagFromKey, out _))
            {
                list.Add((tagFromKey, [PdfWorkspaceMatchStrategy.ClientKeyTagPart, PdfWorkspaceMatchStrategy.Tag]));
                list.Add((PdfWorkspaceFieldKeyResolver.Resolve(tagFromKey),
                    [PdfWorkspaceMatchStrategy.ResolvedTag, PdfWorkspaceMatchStrategy.DataFieldKey]));
            }
            else
            {
                list.Add((key, [PdfWorkspaceMatchStrategy.ClientKey, PdfWorkspaceMatchStrategy.Tag]));
                list.Add((PdfWorkspaceFieldKeyResolver.Resolve(key),
                    [PdfWorkspaceMatchStrategy.ResolvedTag, PdfWorkspaceMatchStrategy.DataFieldKey]));
            }
        }

        return list;
    }

    private static PdfWorkspaceTemplateFieldInfo? TryGet(
        Dictionary<string, PdfWorkspaceTemplateFieldInfo> index,
        string key) =>
        index.TryGetValue(key.Trim(), out var field) ? field : null;

    private static void TryAddUnique(
        Dictionary<string, PdfWorkspaceTemplateFieldInfo> index,
        string key,
        PdfWorkspaceTemplateFieldInfo field)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        index.TryAdd(key.Trim(), field);
    }

    private static bool TryParseTagSequenceKey(string key, out string tag, out int? sequence)
    {
        var hashIndex = key.LastIndexOf('#');
        if (hashIndex <= 0 || hashIndex >= key.Length - 1)
        {
            tag = string.Empty;
            sequence = null;
            return false;
        }

        tag = key[..hashIndex];
        if (!int.TryParse(key[(hashIndex + 1)..], out var parsedSequence))
        {
            sequence = null;
            return false;
        }

        sequence = parsedSequence;
        return true;
    }
}

internal sealed class PdfWorkspaceMatchDiagnostics
{
    public int TotalSubmitted { get; init; }

    public int TotalTemplateFields { get; init; }

    public List<string> MatchedFields { get; } = [];

    public List<string> UnmatchedFields { get; } = [];

    public List<PdfWorkspaceMatchLogEntry> MatchLog { get; } = [];

    public List<PdfWorkspaceTemplateFieldSnapshot> TemplateFields { get; } = [];
}

internal sealed class PdfWorkspaceMatchLogEntry
{
    public string ClientKey { get; init; } = string.Empty;

    public string? ClientTag { get; init; }

    public string? ClientDataFieldKey { get; init; }

    public string? ValuePreview { get; init; }

    public string? MatchedTemplateTag { get; init; }

    public string? MatchedStorageKey { get; init; }

    public string MatchStrategy { get; init; } = string.Empty;

    public bool IsMatched { get; init; }
}

internal sealed class PdfWorkspaceTemplateFieldSnapshot
{
    public string Tag { get; init; } = string.Empty;

    public string NormalizedTag { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? DataFieldKey { get; init; }

    public string StorageKey { get; init; } = string.Empty;
}

internal static class PdfWorkspaceFieldMatcher
{
    public static (Dictionary<string, string?> StorageValues, PdfWorkspaceMatchDiagnostics Diagnostics) BuildStorageValues(
        IReadOnlyList<PdfWorkspaceTemplateFieldInfo> templateFields,
        IReadOnlyList<PdfWorkspaceFieldValueDto> submittedValues,
        ILogger logger)
    {
        var diagnostics = new PdfWorkspaceMatchDiagnostics
        {
            TotalSubmitted = submittedValues.Count,
            TotalTemplateFields = templateFields.Count
        };

        foreach (var field in templateFields)
        {
            diagnostics.TemplateFields.Add(new PdfWorkspaceTemplateFieldSnapshot
            {
                Tag = field.Tag,
                NormalizedTag = field.NormalizedTag,
                Title = field.Title,
                DataFieldKey = field.DataFieldKey,
                StorageKey = field.StorageKey
            });
        }

        logger.LogInformation(
            "PdfWorkspace match: templateVersion has {FieldCount} fields: {Fields}",
            templateFields.Count,
            string.Join("; ", diagnostics.TemplateFields.Select(field =>
                $"Tag={field.Tag}, NormalizedTag={field.NormalizedTag}, Title={field.Title}, DataFieldKey={field.DataFieldKey}, StorageKey={field.StorageKey}")));

        var index = new PdfWorkspaceFieldMatchIndex(templateFields);
        var valuesByTemplateFieldId = new Dictionary<Guid, List<(int Sequence, string Value)>>();
        var directByStorageKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in submittedValues)
        {
            var rawValue = item.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            var (field, strategy) = index.Match(item);
            var valuePreview = rawValue.Length > 40 ? rawValue[..40] + "…" : rawValue;

            var logEntry = new PdfWorkspaceMatchLogEntry
            {
                ClientKey = item.Key,
                ClientTag = item.Tag,
                ClientDataFieldKey = item.DataFieldKey,
                ValuePreview = valuePreview,
                MatchStrategy = strategy.ToString(),
                IsMatched = field is not null,
                MatchedTemplateTag = field?.Tag,
                MatchedStorageKey = field?.StorageKey
            };
            diagnostics.MatchLog.Add(logEntry);

            logger.LogInformation(
                "PdfWorkspace match attempt: Key={Key}, Tag={Tag}, DataFieldKey={DataFieldKey}, Value={Value} => {Result} via {Strategy}, StorageKey={StorageKey}",
                item.Key,
                item.Tag,
                item.DataFieldKey,
                valuePreview,
                field is null ? "UNMATCHED" : $"matched Tag={field.Tag}",
                strategy,
                field?.StorageKey);

            if (field is not null)
            {
                if (!valuesByTemplateFieldId.TryGetValue(field.Id, out var list))
                {
                    list = [];
                    valuesByTemplateFieldId[field.Id] = list;
                }

                list.Add((ResolveSubmissionSequence(item), rawValue));
                diagnostics.MatchedFields.Add(field.Tag);
                continue;
            }

            var fallbackKey = PdfWorkspaceStorageKey.ForUnmatchedClient(
                item.DataFieldKey,
                ResolveSubmissionTag(item),
                item.Key.Trim());

            if (string.IsNullOrWhiteSpace(fallbackKey))
            {
                diagnostics.UnmatchedFields.Add(item.Key);
                continue;
            }

            directByStorageKey[fallbackKey] = rawValue;
            diagnostics.MatchedFields.Add(fallbackKey);
            diagnostics.MatchLog.Remove(logEntry);
            diagnostics.MatchLog.Add(new PdfWorkspaceMatchLogEntry
            {
                ClientKey = logEntry.ClientKey,
                ClientTag = logEntry.ClientTag,
                ClientDataFieldKey = logEntry.ClientDataFieldKey,
                ValuePreview = logEntry.ValuePreview,
                MatchedTemplateTag = null,
                MatchedStorageKey = fallbackKey,
                MatchStrategy = "FallbackStorageKey",
                IsMatched = true
            });

            logger.LogWarning(
                "PdfWorkspace unmatched to template field; saving by fallback key {StorageKey} (Key={Key}, Tag={Tag})",
                fallbackKey,
                item.Key,
                item.Tag);
        }

        var storageValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in templateFields)
        {
            if (!valuesByTemplateFieldId.TryGetValue(field.Id, out var fieldValues) || fieldValues.Count == 0)
            {
                continue;
            }

            var orderedValues = fieldValues
                .OrderBy(entry => entry.Sequence)
                .Select(entry => entry.Value)
                .ToList();

            storageValues[field.StorageKey] = orderedValues.Count == 1
                ? orderedValues[0]
                : string.Join('\n', orderedValues);
        }

        foreach (var (storageKey, value) in directByStorageKey)
        {
            storageValues[storageKey] = value;
        }

        foreach (var log in diagnostics.MatchLog.Where(log => !log.IsMatched))
        {
            diagnostics.UnmatchedFields.Add(log.ClientKey);
        }

        logger.LogInformation(
            "PdfWorkspace match result: {MatchedCount} matched keys, {StorageCount} storage keys, unmatched: {Unmatched}",
            diagnostics.MatchedFields.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            storageValues.Count,
            string.Join(", ", diagnostics.UnmatchedFields.Distinct(StringComparer.OrdinalIgnoreCase)));

        return (storageValues, diagnostics);
    }

    private static string ResolveSubmissionTag(PdfWorkspaceFieldValueDto item)
    {
        var tag = item.Tag?.Trim();
        if (!string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        var key = item.Key.Trim();
        return TryParseTagSequenceKey(key, out var tagFromKey, out _) ? tagFromKey : key;
    }

    private static int ResolveSubmissionSequence(PdfWorkspaceFieldValueDto item)
    {
        if (item.Sequence is > 0)
        {
            return item.Sequence.Value;
        }

        return TryParseTagSequenceKey(item.Key.Trim(), out _, out var sequence) && sequence is > 0
            ? sequence.Value
            : 0;
    }

    private static bool TryParseTagSequenceKey(string key, out string tag, out int? sequence)
    {
        var hashIndex = key.LastIndexOf('#');
        if (hashIndex <= 0 || hashIndex >= key.Length - 1)
        {
            tag = string.Empty;
            sequence = null;
            return false;
        }

        tag = key[..hashIndex];
        if (!int.TryParse(key[(hashIndex + 1)..], out var parsedSequence))
        {
            sequence = null;
            return false;
        }

        sequence = parsedSequence;
        return true;
    }
}
