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
        Console.WriteLine("=== SAVE VALUES CALLED ===");
        Console.WriteLine($"TemplateVersionId: {templateVersionId}");
        Console.WriteLine($"Received items count: {values.Count}");
        foreach (var item in values)
        {
            Console.WriteLine(
                $"Item: {System.Text.Json.JsonSerializer.Serialize(new { item.TemplateFieldId, item.Value })}");
        }

        var receivedCount = values.Count;
        var unmatched = new List<string>();

        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);

        var order = await EnsureOrderAsync(orderId, templateVersionId, cancellationToken);
        await EnsureOrderDocumentAsync(order, templateVersionId, cancellationToken);

        var orderDocument = await _context.OrderDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document => document.OrderId == order.Id &&
                            document.TemplateVersionId == templateVersionId &&
                            !document.IsAnnulled,
                cancellationToken);

        Console.WriteLine($"OrderId: {order.Id}, OrderDocumentId: {orderDocument?.Id}");

        var items = values
            .Where(item => item.TemplateFieldId.HasValue && !string.IsNullOrWhiteSpace(item.Value))
            .ToList();

        if (items.Count == 0)
        {
            return new PdfWorkspaceSaveResult
            {
                OrderId = order.Id,
                SavedCount = receivedCount,
                TotalFields = receivedCount,
                Message = "Немає полів для збереження",
                UnmatchedFields = unmatched
            };
        }

        var templateFieldIds = items.Select(item => item.TemplateFieldId!.Value).Distinct().ToList();
        var templateFields = await _context.TemplateFields
            .Include(field => field.DataField)
            .Where(field => templateFieldIds.Contains(field.Id) &&
                            field.TemplateVersionId == templateVersionId &&
                            !field.IsAnnulled)
            .ToDictionaryAsync(field => field.Id, cancellationToken);

        var valuesByDataFieldId = new Dictionary<Guid, string>();

        foreach (var item in items)
        {
            var templateFieldId = item.TemplateFieldId!.Value;
            if (!templateFields.TryGetValue(templateFieldId, out var field))
            {
                unmatched.Add(templateFieldId.ToString("D"));
                Console.WriteLine($"TemplateField NOT FOUND: {templateFieldId}");
                continue;
            }

            var dataFieldId = await EnsureDataFieldIdForTemplateFieldAsync(field, cancellationToken);
            valuesByDataFieldId[dataFieldId] = item.Value!.Trim();

            Console.WriteLine(
                $"Mapped TemplateFieldId={field.Id}, Tag={field.Tag}, DataFieldId={dataFieldId}, OrderDocumentId={orderDocument?.Id}");
        }

        if (valuesByDataFieldId.Count > 0)
        {
            var dataFieldIds = valuesByDataFieldId.Keys.ToList();
            var existingValues = await _context.OrderFieldValues
                .Where(fieldValue => fieldValue.OrderId == order.Id &&
                                     fieldValue.SampleId == null &&
                                     dataFieldIds.Contains(fieldValue.DataFieldId))
                .ToListAsync(cancellationToken);

            foreach (var (dataFieldId, storedValue) in valuesByDataFieldId)
            {
                var existing = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
                if (existing is null)
                {
                    _context.OrderFieldValues.Add(new OrderFieldValue
                    {
                        OrderId = order.Id,
                        SampleId = null,
                        DataFieldId = dataFieldId,
                        StoredValue = storedValue
                    });
                }
                else
                {
                    existing.StoredValue = storedValue;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            Console.WriteLine($"SaveChanges OK: {valuesByDataFieldId.Count} OrderFieldValues");
        }

        return new PdfWorkspaceSaveResult
        {
            OrderId = order.Id,
            SavedCount = receivedCount,
            TotalFields = receivedCount,
            Message = receivedCount > 0
                ? $"Прийнято {receivedCount} полів (збережено в БД: {receivedCount - unmatched.Count})"
                : "Немає полів для збереження",
            UnmatchedFields = unmatched
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
                    .Select(segment => new { segment.Sequence })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        if (templateFields.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var storedValues = (await _context.OrderFieldValues
            .AsNoTracking()
            .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == null)
            .Select(fieldValue => new
            {
                fieldValue.DataFieldId,
                fieldValue.DataField.Key,
                fieldValue.StoredValue
            })
            .ToListAsync(cancellationToken))
            .Select(item => (item.DataFieldId, item.Key, item.StoredValue))
            .ToList();

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in templateFields)
        {
            var storedValue = FindStoredValue(field.Id, field.DataFieldKey, storedValues);
            if (storedValue is null)
            {
                continue;
            }

            result[field.Id.ToString("D")] = storedValue;
            result[field.Tag] = storedValue;
            if (!string.IsNullOrWhiteSpace(field.DataFieldKey))
            {
                result[field.DataFieldKey] = storedValue;
            }

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

    /// <summary>
    /// Один TemplateField = один DataField (ключ = Id поля), щоб уникнути злиття та дублікатів у OrderFieldValues.
    /// </summary>
    private async Task<Guid> EnsureDataFieldIdForTemplateFieldAsync(
        TemplateField field,
        CancellationToken cancellationToken)
    {
        var storageKey = field.Id.ToString("D");

        if (field.DataField is not null &&
            string.Equals(field.DataField.Key, storageKey, StringComparison.OrdinalIgnoreCase))
        {
            return field.DataFieldId!.Value;
        }

        var existing = await _context.DataFields
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dataField => dataField.Key == storageKey, cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsActive || existing.IsAnnulled)
            {
                existing.IsAnnulled = false;
                existing.AnnulledAtUtc = null;
                existing.AnnulledByUserId = null;
                existing.AnnulmentReason = null;
                existing.IsActive = true;
            }

            field.DataFieldId = existing.Id;
            field.Status = TemplateFieldStatus.Mapped;
            return existing.Id;
        }

        var created = new DataField
        {
            Key = storageKey,
            DisplayNameUk = string.IsNullOrWhiteSpace(field.Title) ? field.Tag : field.Title,
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsRequired = false,
            IsSystem = false,
            IsActive = true,
            MaxLength = 2000
        };

        _context.DataFields.Add(created);
        field.DataFieldId = created.Id;
        field.Status = TemplateFieldStatus.Mapped;

        return created.Id;
    }

    private static string? FindStoredValue(
        Guid templateFieldId,
        string? dataFieldKey,
        IReadOnlyList<(Guid DataFieldId, string Key, string? StoredValue)> storedValues)
    {
        var templateFieldKey = templateFieldId.ToString("D");
        foreach (var item in storedValues)
        {
            if (string.Equals(item.Key, templateFieldKey, StringComparison.OrdinalIgnoreCase))
            {
                return item.StoredValue;
            }
        }

        if (string.IsNullOrWhiteSpace(dataFieldKey))
        {
            return null;
        }

        foreach (var item in storedValues)
        {
            if (string.Equals(item.Key, dataFieldKey, StringComparison.OrdinalIgnoreCase))
            {
                return item.StoredValue;
            }
        }

        return null;
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

        var storedValues = (await _context.OrderFieldValues
            .AsNoTracking()
            .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == null)
            .Select(fieldValue => new
            {
                fieldValue.DataFieldId,
                fieldValue.DataField.Key,
                fieldValue.StoredValue
            })
            .ToListAsync(cancellationToken))
            .Select(item => (item.DataFieldId, item.Key, item.StoredValue))
            .ToList();

        var overlaySegments = new List<ReferralOverlaySegment>();

        foreach (var field in templateFields)
        {
            if (field.Segments.Count == 0)
            {
                continue;
            }

            var storedValue = FindStoredValue(field.Id, field.DataFieldKey, storedValues);
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
                    StorageKey = field.DataFieldKey ?? field.Id.ToString("D"),
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

}
