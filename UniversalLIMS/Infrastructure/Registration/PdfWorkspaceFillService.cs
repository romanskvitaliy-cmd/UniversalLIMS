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
        var received = values.Count;
        var mapped = 0;
        var saved = 0;
        var skippedUnmapped = 0;
        var skippedEmpty = 0;

        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);

        var order = await EnsureOrderAsync(orderId, templateVersionId, cancellationToken);
        await EnsureOrderDocumentAsync(order, templateVersionId, cancellationToken);

        var templateFieldIds = values
            .Where(item => item.TemplateFieldId.HasValue)
            .Select(item => item.TemplateFieldId!.Value)
            .Distinct()
            .ToList();

        var templateFields = templateFieldIds.Count == 0
            ? new Dictionary<Guid, TemplateField>()
            : await _context.TemplateFields
                .Where(field => templateFieldIds.Contains(field.Id) &&
                                field.TemplateVersionId == templateVersionId &&
                                !field.IsAnnulled)
                .ToDictionaryAsync(field => field.Id, cancellationToken);

        var mappedDataFieldIds = templateFields.Values
            .Where(field => field.DataFieldId.HasValue)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToList();

        var existingValues = mappedDataFieldIds.Count == 0
            ? []
            : await _context.OrderFieldValues
                .Where(fieldValue => fieldValue.OrderId == order.Id &&
                                     fieldValue.SampleId == null &&
                                     mappedDataFieldIds.Contains(fieldValue.DataFieldId))
                .ToListAsync(cancellationToken);

        foreach (var item in values)
        {
            if (!item.TemplateFieldId.HasValue ||
                !templateFields.TryGetValue(item.TemplateFieldId.Value, out var field) ||
                !field.DataFieldId.HasValue)
            {
                skippedUnmapped++;
                continue;
            }

            mapped++;
            var dataFieldId = field.DataFieldId.Value;
            var trimmedValue = item.Value?.Trim();

            if (string.IsNullOrEmpty(trimmedValue))
            {
                var existing = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
                if (existing is not null)
                {
                    _context.OrderFieldValues.Remove(existing);
                    existingValues.Remove(existing);
                }

                skippedEmpty++;
                continue;
            }

            var stored = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
            if (stored is null)
            {
                var created = new OrderFieldValue
                {
                    OrderId = order.Id,
                    SampleId = null,
                    DataFieldId = dataFieldId,
                    StoredValue = trimmedValue
                };
                _context.OrderFieldValues.Add(created);
                existingValues.Add(created);
            }
            else
            {
                stored.StoredValue = trimmedValue;
            }

            saved++;
        }

        if (mapped > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new PdfWorkspaceSaveResult
        {
            OrderId = order.Id,
            Received = received,
            Mapped = mapped,
            Saved = saved,
            SkippedUnmapped = skippedUnmapped,
            SkippedEmpty = skippedEmpty,
            Message = BuildSaveMessage(received, mapped, saved, skippedUnmapped, skippedEmpty)
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

        var (segments, valuesByDataFieldId) = await LoadOverlayRenderDataAsync(
            templateVersionId,
            orderId,
            cancellationToken);

        await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
            version.StorageKey,
            cancellationToken);

        if (originalPdfStream.CanSeek)
        {
            originalPdfStream.Position = 0;
        }

        return _overlayRenderer.Render(originalPdfStream, segments, valuesByDataFieldId);
    }

    public async Task<byte[]> GenerateCalibrationPreviewPdfAsync(
        Guid templateVersionId,
        IReadOnlyDictionary<Guid, string> sampleTextsByFieldId,
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

        var layoutRows = await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                orderby field.SortOrder, segment.Sequence
                select new OverlaySegmentJoinRow(
                    field.Id,
                    field.DataFieldId ?? Guid.Empty,
                    field.TextOffsetX,
                    field.TextOffsetY,
                    segment.PageNumber,
                    segment.PositionX,
                    segment.PositionY,
                    segment.Width,
                    segment.Height,
                    segment.Sequence,
                    segment.TextAlignment,
                    segment.HorizontalAlignment,
                    segment.VerticalAlignment,
                    segment.FontName,
                    segment.FontSize))
            .ToListAsync(cancellationToken);

        var overlaySegments = new List<ReferralOverlaySegment>();
        foreach (var fieldGroup in layoutRows.GroupBy(row => row.TemplateFieldId))
        {
            if (!sampleTextsByFieldId.TryGetValue(fieldGroup.Key, out var sampleText) ||
                string.IsNullOrWhiteSpace(sampleText))
            {
                continue;
            }

            var orderedSegments = fieldGroup.OrderBy(row => row.Sequence).ToList();
            var lines = SplitStoredLines(sampleText);

            for (var index = 0; index < orderedSegments.Count; index++)
            {
                var row = orderedSegments[index];
                var text = orderedSegments.Count == 1
                    ? sampleText.Trim()
                    : index < lines.Count
                        ? lines[index]
                        : null;

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                overlaySegments.Add(new ReferralOverlaySegment
                {
                    Text = text,
                    PageNumber = row.PageNumber,
                    PositionX = row.PositionX,
                    PositionY = row.PositionY,
                    Width = row.Width,
                    Height = row.Height,
                    TextAlignment = row.TextAlignment,
                    HorizontalAlignment = row.HorizontalAlignment ?? row.TextAlignment.ToString(),
                    VerticalAlignment = row.VerticalAlignment,
                    FontName = row.FontName,
                    FontSize = row.FontSize,
                    TextOffsetX = row.TextOffsetX,
                    TextOffsetY = row.TextOffsetY
                });
            }
        }

        await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
            version.StorageKey,
            cancellationToken);

        if (originalPdfStream.CanSeek)
        {
            originalPdfStream.Position = 0;
        }

        return _overlayRenderer.Render(originalPdfStream, overlaySegments, new Dictionary<Guid, string?>());
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
                field.DataFieldId,
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

        var dataFieldIds = templateFields
            .Where(field => field.DataFieldId.HasValue)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToList();

        if (dataFieldIds.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var storedByDataFieldId = OrderFieldValueSelection.ResolveByDataFieldId(
            await _context.OrderFieldValues
                .AsNoTracking()
                .Where(fieldValue => fieldValue.OrderId == orderId &&
                                     dataFieldIds.Contains(fieldValue.DataFieldId))
                .Select(fieldValue => new OrderFieldValueCandidate(
                    fieldValue.DataFieldId,
                    fieldValue.SampleId,
                    fieldValue.StoredValue,
                    fieldValue.UpdatedAtUtc,
                    fieldValue.CreatedAtUtc))
                .ToListAsync(cancellationToken));

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in templateFields)
        {
            if (!field.DataFieldId.HasValue ||
                !storedByDataFieldId.TryGetValue(field.DataFieldId.Value, out var storedValue) ||
                string.IsNullOrWhiteSpace(storedValue))
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

    private static string BuildSaveMessage(
        int received,
        int mapped,
        int saved,
        int skippedUnmapped,
        int skippedEmpty) =>
        $"Прийнято: {received}, зіставлено: {mapped}, збережено: {saved}, " +
        $"пропущено (без мапінгу): {skippedUnmapped}, очищено порожніх: {skippedEmpty}.";

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

    private async Task<(IReadOnlyList<ReferralOverlaySegment> Segments, Dictionary<Guid, string?> ValuesByDataFieldId)>
        LoadOverlayRenderDataAsync(
            Guid templateVersionId,
            Guid orderId,
            CancellationToken cancellationToken)
    {
        var layoutRows = await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                      && field.DataFieldId != null
                orderby field.SortOrder, segment.Sequence
                select new OverlaySegmentJoinRow(
                    field.Id,
                    field.DataFieldId!.Value,
                    field.TextOffsetX,
                    field.TextOffsetY,
                    segment.PageNumber,
                    segment.PositionX,
                    segment.PositionY,
                    segment.Width,
                    segment.Height,
                    segment.Sequence,
                    segment.TextAlignment,
                    segment.HorizontalAlignment,
                    segment.VerticalAlignment,
                    segment.FontName,
                    segment.FontSize))
            .ToListAsync(cancellationToken);

        if (layoutRows.Count == 0)
        {
            return ([], []);
        }

        var dataFieldIds = layoutRows.Select(row => row.DataFieldId).Distinct().ToList();

        var valuesByDataFieldId = OrderFieldValueSelection.ResolveByDataFieldId(
            await _context.OrderFieldValues
                .AsNoTracking()
                .Where(fieldValue => fieldValue.OrderId == orderId &&
                                     dataFieldIds.Contains(fieldValue.DataFieldId))
                .Select(fieldValue => new OrderFieldValueCandidate(
                    fieldValue.DataFieldId,
                    fieldValue.SampleId,
                    fieldValue.StoredValue,
                    fieldValue.UpdatedAtUtc,
                    fieldValue.CreatedAtUtc))
                .ToListAsync(cancellationToken));

        if (valuesByDataFieldId.Count == 0)
        {
            return ([], valuesByDataFieldId);
        }

        var overlaySegments = new List<ReferralOverlaySegment>();

        foreach (var fieldGroup in layoutRows.GroupBy(row => row.TemplateFieldId))
        {
            var dataFieldId = fieldGroup.First().DataFieldId;
            if (!valuesByDataFieldId.TryGetValue(dataFieldId, out var storedValue) ||
                string.IsNullOrWhiteSpace(storedValue))
            {
                continue;
            }

            var orderedSegments = fieldGroup.OrderBy(row => row.Sequence).ToList();
            var lines = SplitStoredLines(storedValue);

            for (var index = 0; index < orderedSegments.Count; index++)
            {
                var row = orderedSegments[index];
                var text = orderedSegments.Count == 1
                    ? storedValue
                    : index < lines.Count
                        ? lines[index]
                        : null;

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                overlaySegments.Add(new ReferralOverlaySegment
                {
                    DataFieldId = row.DataFieldId,
                    Text = text,
                    PageNumber = row.PageNumber,
                    PositionX = row.PositionX,
                    PositionY = row.PositionY,
                    Width = row.Width,
                    Height = row.Height,
                    TextAlignment = row.TextAlignment,
                    HorizontalAlignment = row.HorizontalAlignment ?? row.TextAlignment.ToString(),
                    VerticalAlignment = row.VerticalAlignment,
                    FontName = row.FontName,
                    FontSize = row.FontSize,
                    TextOffsetX = row.TextOffsetX,
                    TextOffsetY = row.TextOffsetY
                });
            }
        }

        return (overlaySegments, valuesByDataFieldId);
    }

    private sealed record OverlaySegmentJoinRow(
        Guid TemplateFieldId,
        Guid DataFieldId,
        decimal TextOffsetX,
        decimal TextOffsetY,
        int PageNumber,
        decimal PositionX,
        decimal PositionY,
        decimal Width,
        decimal Height,
        int Sequence,
        TextAlignment TextAlignment,
        string? HorizontalAlignment,
        string? VerticalAlignment,
        string? FontName,
        decimal? FontSize);

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
