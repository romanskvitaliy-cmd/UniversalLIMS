using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class PdfWorkspaceFillService : IPdfWorkspaceFillService
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateDocumentStorage _templateDocumentStorage;
    private readonly IOrderFieldValueService _orderFieldValueService;
    private readonly ReferralPdfOverlayRenderer _overlayRenderer;
    private readonly ILogger<PdfWorkspaceFillService> _logger;

    public PdfWorkspaceFillService(
        ApplicationDbContext context,
        ITemplateDocumentStorage templateDocumentStorage,
        IOrderFieldValueService orderFieldValueService,
        ILogger<PdfWorkspaceFillService> logger)
    {
        _context = context;
        _templateDocumentStorage = templateDocumentStorage;
        _orderFieldValueService = orderFieldValueService;
        _logger = logger;
        _overlayRenderer = new ReferralPdfOverlayRenderer();
    }

    public async Task<PdfWorkspaceSaveResult> SaveValuesAsync(
        Guid templateVersionId,
        Guid? orderId,
        IReadOnlyList<PdfWorkspaceFieldValueDto> values,
        CancellationToken cancellationToken = default)
    {
        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);

        var order = await EnsureOrderAsync(orderId, templateVersionId, cancellationToken);
        await EnsureOrderDocumentAsync(order, templateVersionId, cancellationToken);

        var submittedValues = values
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToList();

        if (submittedValues.Count == 0)
        {
            return new PdfWorkspaceSaveResult
            {
                OrderId = order.Id,
                SavedCount = 0,
                TotalFields = submittedValues.Count
            };
        }

        var templateFields = await LoadTemplateFieldInfosAsync(templateVersionId, cancellationToken);
        var (storageValues, diagnostics) = PdfWorkspaceFieldMatcher.BuildStorageValues(
            templateFields,
            submittedValues,
            _logger);

        if (storageValues.Count == 0)
        {
            return new PdfWorkspaceSaveResult
            {
                OrderId = order.Id,
                SavedCount = 0,
                TotalFields = submittedValues.Count,
                MatchedFields = diagnostics.MatchedFields,
                UnmatchedFields = diagnostics.UnmatchedFields,
                SkippedKeys = submittedValues.Select(item => item.Key).ToList(),
                MatchLog = MapMatchLog(diagnostics)
            };
        }

        var dataFieldKeys = storageValues.Keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var dataFieldIdByKey = await EnsureDataFieldIdsAsync(dataFieldKeys, cancellationToken);

        var inputs = new List<OrderFieldValueInput>();
        var skippedKeys = new List<string>();

        foreach (var (storageKey, storedValue) in storageValues)
        {
            if (!dataFieldIdByKey.TryGetValue(storageKey, out var dataFieldId))
            {
                skippedKeys.Add(storageKey);
                continue;
            }

            inputs.Add(new OrderFieldValueInput
            {
                DataFieldId = dataFieldId,
                SampleId = null,
                StoredValue = storedValue
            });
        }

        if (inputs.Count > 0)
        {
            await _orderFieldValueService.UpsertAsync(order.Id, inputs, cancellationToken);
            await LinkTemplateFieldsAsync(templateVersionId, dataFieldIdByKey, cancellationToken);
        }

        var distinctMatched = diagnostics.MatchedFields
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PdfWorkspaceSaveResult
        {
            OrderId = order.Id,
            SavedCount = inputs.Count,
            TotalFields = submittedValues.Count,
            MatchedFields = distinctMatched,
            UnmatchedFields = diagnostics.UnmatchedFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SkippedKeys = skippedKeys,
            MatchLog = MapMatchLog(diagnostics)
        };
    }

    public async Task<byte[]> GenerateFilledPdfAsync(
        Guid templateVersionId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == templateVersionId, cancellationToken)
            ?? throw new InvalidOperationException("Версію шаблону не знайдено.");

        if (version.DocumentFormat != TemplateDocumentFormat.Pdf)
        {
            throw new InvalidOperationException("Підтримуються лише PDF-шаблони.");
        }

        if (!await _templateDocumentStorage.ExistsAsync(version.StorageKey, cancellationToken))
        {
            throw new InvalidOperationException("Оригінальний PDF шаблону не знайдено у сховищі.");
        }

        var segments = await LoadOverlaySegmentsWithValuesAsync(templateVersionId, orderId, cancellationToken);

        await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
            version.StorageKey,
            cancellationToken);

        return _overlayRenderer.Render(originalPdfStream, segments, new Dictionary<Guid, string?>());
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetSavedValuesByKeyAsync(
        Guid orderId,
        Guid templateVersionId,
        CancellationToken cancellationToken = default)
    {
        var templateFields = await _context.TemplateFields
            .AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
            .Select(field => new
            {
                field.Id,
                field.Tag,
                DataFieldKey = field.DataField != null ? field.DataField.Key : null,
                Segments = field.Segments
                    .Where(segment => !segment.IsAnnulled)
                    .OrderBy(segment => segment.Sequence)
                    .Select(segment => new { segment.Id, segment.Sequence })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (templateFields.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var storageKeys = templateFields
            .Select(field => PdfWorkspaceStorageKey.ForTemplateField(field.Tag, field.DataFieldKey))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (storageKeys.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var storedByStorageKey = await _context.OrderFieldValues
            .AsNoTracking()
            .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == null)
            .Where(fieldValue => storageKeys.Contains(fieldValue.DataField.Key))
            .Select(fieldValue => new { fieldValue.DataField.Key, fieldValue.StoredValue })
            .ToDictionaryAsync(item => item.Key, item => item.StoredValue, StringComparer.Ordinal, cancellationToken);

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in templateFields)
        {
            var storageKey = PdfWorkspaceStorageKey.ForTemplateField(field.Tag, field.DataFieldKey);
            if (!storedByStorageKey.TryGetValue(storageKey, out var storedValue))
            {
                continue;
            }

            result[field.Tag] = storedValue;
            result[storageKey] = storedValue;

            if (field.Segments.Count <= 1)
            {
                continue;
            }

            var lines = SplitStoredLines(storedValue);
            for (var index = 0; index < field.Segments.Count; index++)
            {
                var line = index < lines.Count ? lines[index] : string.Empty;
                result[$"{field.Tag}#{field.Segments[index].Sequence}"] = line;
            }
        }

        return result;
    }

    private async Task EnsureTemplateVersionExistsAsync(Guid templateVersionId, CancellationToken cancellationToken)
    {
        var exists = await _context.TemplateVersions
            .AsNoTracking()
            .AnyAsync(item => item.Id == templateVersionId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Версію шаблону не знайдено.");
        }
    }

    private async Task<Order> EnsureOrderAsync(
        Guid? orderId,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        if (orderId.HasValue)
        {
            var existing = await _context.Orders
                .Include(order => order.Samples)
                .Include(order => order.OrderDocuments)
                .FirstOrDefaultAsync(order => order.Id == orderId.Value && !order.IsAnnulled, cancellationToken);

            if (existing is not null)
            {
                await EnsureSampleAsync(existing, cancellationToken);
                return existing;
            }
        }

        var branch = await _context.Branches
            .AsNoTracking()
            .OrderBy(branch => branch.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("У системі немає філій. Запустіть seed.");

        var customer = await _context.Customers
            .Where(item => !item.IsAnnulled)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Kind = CustomerKind.Individual,
                FullName = "PDF Workspace (тестовий замовник)"
            };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var order = new Order
        {
            CustomerId = customer.Id,
            BranchId = branch.Id,
            Status = OrderStatus.Draft,
            ReferralNumber = $"PDF-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        await EnsureSampleAsync(order, cancellationToken);
        await EnsureOrderDocumentAsync(order, templateVersionId, cancellationToken);

        return order;
    }

    private async Task<Sample> EnsureSampleAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Samples.Count > 0)
        {
            return order.Samples.First();
        }

        var loadedSamples = await _context.Samples
            .Where(sample => sample.OrderId == order.Id && !sample.IsAnnulled)
            .ToListAsync(cancellationToken);

        if (loadedSamples.Count > 0)
        {
            foreach (var loadedSample in loadedSamples)
            {
                order.Samples.Add(loadedSample);
            }

            return loadedSamples[0];
        }

        var investigationTypeId = await _context.InvestigationTypes
            .OrderBy(type => type.SortOrder)
            .Select(type => type.Id)
            .FirstAsync(cancellationToken);

        var sampleNumber = $"WS-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var sample = new Sample
        {
            OrderId = order.Id,
            InvestigationTypeId = investigationTypeId,
            Number = sampleNumber,
            RegisteredAt = DateTime.UtcNow
        };

        _context.Samples.Add(sample);
        await _context.SaveChangesAsync(cancellationToken);
        order.Samples.Add(sample);

        return sample;
    }

    private async Task EnsureOrderDocumentAsync(
        Order order,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        if (order.OrderDocuments.Any(document => document.TemplateVersionId == templateVersionId && !document.IsAnnulled))
        {
            return;
        }

        var linked = await _context.OrderDocuments
            .AnyAsync(
                document => document.OrderId == order.Id &&
                            document.TemplateVersionId == templateVersionId &&
                            !document.IsAnnulled,
                cancellationToken);

        if (linked)
        {
            return;
        }

        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstAsync(item => item.Id == templateVersionId, cancellationToken);

        var sample = await EnsureSampleAsync(order, cancellationToken);

        _context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = version.TemplateId,
            TemplateVersionId = version.Id,
            TargetBranchId = order.BranchId,
            Status = OrderDocumentStatus.Pending
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ReferralOverlaySegment>> LoadOverlaySegmentsWithValuesAsync(
        Guid templateVersionId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var templateFields = await _context.TemplateFields
            .AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
            .Select(field => new
            {
                field.Id,
                field.Tag,
                field.DataFieldId,
                DataFieldKey = field.DataField != null ? field.DataField.Key : null,
                Segments = field.Segments
                    .Where(segment => !segment.IsAnnulled)
                    .OrderBy(segment => segment.Sequence)
                    .Select(segment => new
                    {
                        segment.PageNumber,
                        segment.PositionX,
                        segment.PositionY,
                        segment.Width,
                        segment.Height,
                        segment.Sequence,
                        segment.TextAlignment,
                        segment.FontName,
                        segment.FontSize
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (templateFields.Count == 0)
        {
            return [];
        }

        var storageKeys = templateFields
            .Select(field => PdfWorkspaceStorageKey.ForTemplateField(field.Tag, field.DataFieldKey))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var storedByStorageKey = storageKeys.Count == 0
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : await _context.OrderFieldValues
                .AsNoTracking()
                .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == null)
                .Where(fieldValue => storageKeys.Contains(fieldValue.DataField.Key))
                .Select(fieldValue => new { fieldValue.DataField.Key, fieldValue.StoredValue })
                .ToDictionaryAsync(item => item.Key, item => item.StoredValue, StringComparer.Ordinal, cancellationToken);

        var overlaySegments = new List<ReferralOverlaySegment>();

        foreach (var field in templateFields)
        {
            if (field.Segments.Count == 0)
            {
                continue;
            }

            var storageKey = PdfWorkspaceStorageKey.ForTemplateField(field.Tag, field.DataFieldKey);
            storedByStorageKey.TryGetValue(storageKey, out var storedValue);
            var lines = SplitStoredLines(storedValue);

            for (var index = 0; index < field.Segments.Count; index++)
            {
                var segment = field.Segments[index];
                string? text = null;
                if (!string.IsNullOrWhiteSpace(storedValue))
                {
                    text = field.Segments.Count == 1
                        ? storedValue
                        : index < lines.Count
                            ? lines[index]
                            : null;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                overlaySegments.Add(new ReferralOverlaySegment
                {
                    DataFieldId = field.DataFieldId,
                    StorageKey = storageKey,
                    Text = text,
                    PageNumber = segment.PageNumber,
                    PositionX = segment.PositionX,
                    PositionY = segment.PositionY,
                    Width = segment.Width,
                    Height = segment.Height,
                    TextAlignment = segment.TextAlignment,
                    FontName = segment.FontName,
                    FontSize = segment.FontSize
                });
            }
        }

        return overlaySegments;
    }

    private async Task<IReadOnlyList<PdfWorkspaceTemplateFieldInfo>> LoadTemplateFieldInfosAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken) =>
        await _context.TemplateFields
            .AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled)
            .Select(field => new PdfWorkspaceTemplateFieldInfo(
                field.Id,
                field.Tag,
                field.NormalizedTag,
                field.Title,
                field.DataField != null ? field.DataField.Key : null))
            .ToListAsync(cancellationToken);

    private static IReadOnlyList<PdfWorkspaceSaveMatchLogEntry> MapMatchLog(PdfWorkspaceMatchDiagnostics diagnostics) =>
        diagnostics.MatchLog
            .Select(entry => new PdfWorkspaceSaveMatchLogEntry
            {
                ClientKey = entry.ClientKey,
                ClientTag = entry.ClientTag,
                ClientDataFieldKey = entry.ClientDataFieldKey,
                MatchedTemplateTag = entry.MatchedTemplateTag,
                MatchedStorageKey = entry.MatchedStorageKey,
                MatchStrategy = entry.MatchStrategy,
                IsMatched = entry.IsMatched
            })
            .ToList();

    private async Task<Dictionary<string, Guid>> EnsureDataFieldIdsAsync(
        IReadOnlyList<string> dataFieldKeys,
        CancellationToken cancellationToken)
    {
        var existing = await _context.DataFields
            .IgnoreQueryFilters()
            .Where(field => dataFieldKeys.Contains(field.Key))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in existing)
        {
            if (!field.IsActive || field.IsAnnulled)
            {
                field.IsAnnulled = false;
                field.AnnulledAtUtc = null;
                field.AnnulledByUserId = null;
                field.AnnulmentReason = null;
                field.IsActive = true;
            }

            result[field.Key] = field.Id;
        }

        foreach (var dataFieldKey in dataFieldKeys)
        {
            if (result.ContainsKey(dataFieldKey))
            {
                continue;
            }

            var created = await CreateDataFieldAsync(dataFieldKey, cancellationToken);
            result[dataFieldKey] = created.Id;
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static List<string> SplitStoredLines(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return [];
        }

        return storedValue
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private async Task<DataField> CreateDataFieldAsync(string dataFieldKey, CancellationToken cancellationToken)
    {
        var field = new DataField
        {
            Key = dataFieldKey,
            DisplayNameUk = PdfWorkspaceFieldKeyResolver.GetDisplayNameUk(dataFieldKey),
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsRequired = false,
            IsSystem = false,
            IsActive = true,
            MaxLength = 2000
        };

        _context.DataFields.Add(field);
        await _context.SaveChangesAsync(cancellationToken);

        return field;
    }

    private async Task LinkTemplateFieldsAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<string, Guid> dataFieldIdByKey,
        CancellationToken cancellationToken)
    {
        var templateFields = await _context.TemplateFields
            .Include(field => field.DataField)
            .Where(field => field.TemplateVersionId == templateVersionId && !field.IsAnnulled && field.DataFieldId == null)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var templateField in templateFields)
        {
            var storageKey = PdfWorkspaceStorageKey.ForTemplateField(
                templateField.Tag,
                templateField.DataField?.Key);
            if (!dataFieldIdByKey.TryGetValue(storageKey, out var dataFieldId))
            {
                continue;
            }

            templateField.DataFieldId = dataFieldId;
            templateField.Status = TemplateFieldStatus.Mapped;
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
