using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Infrastructure.Diagnostics;
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
        ILogger<PdfWorkspaceFillService> logger,
        ILogger<ReferralPdfOverlayRenderer> overlayLogger)
    {
        _context = context;
        _templateDocumentStorage = templateDocumentStorage;
        _orderFieldValueService = orderFieldValueService;
        _logger = logger;
        _overlayRenderer = new ReferralPdfOverlayRenderer(overlayLogger);
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
        var failures = new List<PdfWorkspaceSaveFieldFailure>();

        _logger.LogInformation(
            "PdfWorkspaceFill SaveValuesAsync: version={VersionId}, order={OrderId}, received={Received}",
            templateVersionId,
            orderId,
            received);

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

        _logger.LogInformation(
            "PdfWorkspaceFill resolved template fields: requested={Requested}, found={Found}",
            templateFieldIds.Count,
            templateFields.Count);

        var workspaceDataFieldIdByTemplateFieldId = await EnsureWorkspaceDataFieldsAsync(
            templateFields.Values,
            cancellationToken);

        foreach (var pair in workspaceDataFieldIdByTemplateFieldId)
        {
            _logger.LogDebug(
                "PdfWorkspaceFill workspace DataField: templateField={TemplateFieldId}, dataField={DataFieldId}",
                pair.Key,
                pair.Value);
        }

        var workspaceDataFieldIds = workspaceDataFieldIdByTemplateFieldId.Values.Distinct().ToList();
        var existingValues = workspaceDataFieldIds.Count == 0
            ? []
            : await _context.OrderFieldValues
                .Where(fieldValue => fieldValue.OrderId == order.Id &&
                                     fieldValue.SampleId == null &&
                                     workspaceDataFieldIds.Contains(fieldValue.DataFieldId))
                .ToListAsync(cancellationToken);

        foreach (var item in values)
        {
            if (!item.TemplateFieldId.HasValue)
            {
                skippedUnmapped++;
                failures.Add(new PdfWorkspaceSaveFieldFailure
                {
                    TemplateFieldId = null,
                    Reason = "templateFieldId порожній або невалідний."
                });
                _logger.LogWarning("PdfWorkspaceFill skip: missing templateFieldId");
                continue;
            }

            var templateFieldId = item.TemplateFieldId.Value;
            if (!templateFields.TryGetValue(templateFieldId, out _))
            {
                skippedUnmapped++;
                failures.Add(new PdfWorkspaceSaveFieldFailure
                {
                    TemplateFieldId = templateFieldId,
                    Reason = "Поле шаблону не знайдено у цій версії."
                });
                _logger.LogWarning(
                    "PdfWorkspaceFill skip: template field not found {TemplateFieldId}",
                    templateFieldId);
                continue;
            }

            if (!workspaceDataFieldIdByTemplateFieldId.TryGetValue(templateFieldId, out var dataFieldId))
            {
                skippedUnmapped++;
                failures.Add(new PdfWorkspaceSaveFieldFailure
                {
                    TemplateFieldId = templateFieldId,
                    Reason = "Не вдалося підготувати DataField для збереження."
                });
                _logger.LogWarning(
                    "PdfWorkspaceFill skip: workspace DataField missing for {TemplateFieldId}",
                    templateFieldId);
                continue;
            }

            mapped++;
            var trimmedValue = item.Value?.Trim();

            if (string.IsNullOrEmpty(trimmedValue))
            {
                var existing = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
                if (existing is not null)
                {
                    _context.OrderFieldValues.Remove(existing);
                    existingValues.Remove(existing);
                    _logger.LogDebug(
                        "PdfWorkspaceFill cleared empty value: templateField={TemplateFieldId}, dataField={DataFieldId}",
                        templateFieldId,
                        dataFieldId);
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
                _logger.LogInformation(
                    "PdfWorkspaceFill insert: order={OrderId}, templateField={TemplateFieldId}, dataField={DataFieldId}, length={Length}",
                    order.Id,
                    templateFieldId,
                    dataFieldId,
                    trimmedValue.Length);
            }
            else
            {
                stored.StoredValue = trimmedValue;
                _logger.LogInformation(
                    "PdfWorkspaceFill update: order={OrderId}, templateField={TemplateFieldId}, dataField={DataFieldId}, length={Length}",
                    order.Id,
                    templateFieldId,
                    dataFieldId,
                    trimmedValue.Length);
            }

            saved++;
        }

        if (mapped > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "PdfWorkspaceFill SaveChanges OK: order={OrderId}, saved={Saved}",
                order.Id,
                saved);
        }

        if (failures.Count > 0)
        {
            _logger.LogWarning(
                "PdfWorkspaceFill save failures: {Failures}",
                string.Join(", ", failures.Select(f => $"{f.TemplateFieldId}:{f.Reason}")));
        }

        _logger.LogInformation(
            "PdfWorkspaceFill save complete: order={OrderId}, received={Received}, mapped={Mapped}, saved={Saved}, failed={Failed}",
            order.Id,
            received,
            mapped,
            saved,
            failures.Count);

        var message = BuildSaveMessage(received, mapped, saved, skippedUnmapped, skippedEmpty, failures);
        return new PdfWorkspaceSaveResult
        {
            OrderId = order.Id,
            Received = received,
            Mapped = mapped,
            Saved = saved,
            SkippedUnmapped = skippedUnmapped,
            SkippedEmpty = skippedEmpty,
            FailedFields = failures,
            Message = message
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

    public async Task<CalibrationPreviewPdfResult> GenerateCalibrationPreviewAsync(
        PreviewCalibrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var templateVersionId = request.TemplateVersionId;
        if (templateVersionId == Guid.Empty)
        {
            throw new InvalidOperationException("templateVersionId не вказано.");
        }

        if (request.Fields is null || request.Fields.Count == 0)
        {
            throw new InvalidOperationException("No fields for preview");
        }

        var firstThreePreview = string.Join(
            "; ",
            request.Fields.Take(3).Select(field =>
            {
                var preview = field.Text.Length <= 40 ? field.Text : $"{field.Text[..40]}…";
                return $"{field.TemplateFieldId?.ToString("D") ?? "no-id"}:'{preview}'";
            }));

        Console.WriteLine($"Preview received {request.Fields.Count} fields");
        _logger.LogInformation(
            "Preview received {FieldCount} fields from UI. First 3: {FirstThree}",
            request.Fields.Count,
            firstThreePreview);

        var clientFields = request.Fields.Select(MapPreviewFieldDto).ToList();

        // WYSIWYG: текст з UI; БД лише для геометрії сегмента (fallback).
        var fields = await EnrichCalibrationPreviewFieldsFromDatabaseAsync(
            templateVersionId,
            clientFields,
            cancellationToken);

        var fieldDetailsForLog = string.Join(
            "; ",
            fields.Select(item =>
            {
                var drawable = item.ResolveDrawableText();
                var previewText = drawable.Length <= 40 ? drawable : $"{drawable[..40]}…";
                return $"{item.TemplateFieldId?.ToString("D") ?? "no-id"}:'{previewText}'@p{item.Page}({item.X},{item.Y})";
            }));

        _logger.LogInformation(
            "Calibration preview WYSIWYG: version={VersionId}, receivedFields={ReceivedCount}, drawableFields={DrawableCount}, fields=[{FieldDetails}]",
            templateVersionId,
            request.Fields.Count,
            fields.Count,
            fieldDetailsForLog);

        // Лише порожній PDF-шаблон зі сховища; layout і текст — виключно з request.Fields.
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

        var textById = BuildPreviewTextById(request.Fields);

        _logger.LogInformation(
            "Preview using UI fields: {FieldCount}, textById keys={KeyCount}",
            request.Fields.Count,
            textById.Count);

        // Геометрія з БД (усі сегменти поля); текст — лише з textById у renderer.
        var overlaySegments = await BuildCalibrationPreviewOverlaySegmentsAsync(
            templateVersionId,
            fields,
            textById,
            cancellationToken);

        // #region agent log
        AgentDebugLog.Write("C", "GenerateCalibrationPreviewAsync", "request vs segments", new
        {
            requestFields = request.Fields.Select(f => new
            {
                id = f.TemplateFieldId,
                textLen = (f.Text ?? "").Length,
                textToDrawLen = (f.TextToDraw ?? "").Length,
                resolvedLen = f.ResolveDrawableText().Length,
                page = f.Page,
                x = f.X,
                y = f.Y
            }),
            segmentCount = overlaySegments.Count,
            emptySegmentText = overlaySegments.Count(s => string.IsNullOrWhiteSpace(s.Text))
        });
        // #endregion

        await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
            version.StorageKey,
            cancellationToken);

        if (originalPdfStream.CanSeek)
        {
            originalPdfStream.Position = 0;
        }

        // Текст з UI за templateFieldId; не підставляти OrderFieldValue.
        var renderStats = _overlayRenderer.RenderWithStats(
            originalPdfStream,
            overlaySegments,
            valuesByDataFieldId: new Dictionary<Guid, string?>(),
            skipEmptyText: false,
            textById: textById);

        _logger.LogInformation(
            "Calibration preview render: drawn={Drawn}, skippedEmpty={SkippedEmpty}, skippedPage={SkippedPage}, pdfPages={PdfPages}",
            renderStats.SegmentsDrawn,
            renderStats.SegmentsSkippedEmpty,
            renderStats.SegmentsSkippedPage,
            renderStats.PdfPageCount);

        return new CalibrationPreviewPdfResult(
            renderStats.PdfBytes,
            renderStats.SegmentsDrawn,
            renderStats.SegmentsSkippedEmpty,
            renderStats.SegmentsSkippedPage,
            renderStats.PdfPageCount);
    }

    /// <summary>
    /// Доповнює геометрію з БД, якщо клієнт надіслав лише текст (або нульові розміри).
    /// Текст завжди з UI; layout — з запиту, інакше primary-сегмент з БД.
    /// </summary>
    private async Task<List<PreviewCalibrationFieldRequest>> EnrichCalibrationPreviewFieldsFromDatabaseAsync(
        Guid templateVersionId,
        IReadOnlyList<PreviewCalibrationFieldRequest> clientFields,
        CancellationToken cancellationToken)
    {
        var fieldIds = clientFields
            .Select(item => item.TemplateFieldId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (fieldIds.Count == 0)
        {
            return clientFields.ToList();
        }

        var dbSegments = await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                      && fieldIds.Contains(field.Id)
                orderby field.SortOrder, segment.Sequence
                select new
                {
                    field.Id,
                    field.TextOffsetX,
                    field.TextOffsetY,
                    segment.PageNumber,
                    segment.PositionX,
                    segment.PositionY,
                    segment.Width,
                    segment.Height,
                    segment.FontSize,
                    segment.FontName,
                    segment.TextAlignment,
                    segment.HorizontalAlignment,
                    segment.VerticalAlignment,
                    segment.Sequence
                })
            .ToListAsync(cancellationToken);

        var segmentsByFieldId = dbSegments
            .GroupBy(item => item.Id)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Sequence).ToList());

        var enriched = new List<PreviewCalibrationFieldRequest>();
        var layoutFallbackCount = 0;

        foreach (var clientField in clientFields)
        {
            var uiText = clientField.Text ?? string.Empty;
            clientField.Text = uiText;
            clientField.Value = uiText;
            clientField.TextToDraw = uiText;

            if (!clientField.TemplateFieldId.HasValue ||
                !segmentsByFieldId.TryGetValue(clientField.TemplateFieldId.Value, out var dbSegmentsForField) ||
                dbSegmentsForField.Count == 0)
            {
                enriched.Add(clientField);
                continue;
            }

            if (!NeedsDatabaseLayoutFallback(clientField))
            {
                enriched.Add(clientField);
                continue;
            }

            var dbSegment = clientField.SegmentSequence > 0
                ? dbSegmentsForField.FirstOrDefault(item => item.Sequence == clientField.SegmentSequence)
                  ?? dbSegmentsForField[0]
                : dbSegmentsForField[0];

            layoutFallbackCount++;
            enriched.Add(new PreviewCalibrationFieldRequest
            {
                TemplateFieldId = clientField.TemplateFieldId,
                Value = uiText,
                Text = uiText,
                TextToDraw = uiText,
                SegmentSequence = dbSegment.Sequence,
                OffsetX = clientField.OffsetX != 0 ? clientField.OffsetX : dbSegment.TextOffsetX,
                OffsetY = clientField.OffsetY != 0 ? clientField.OffsetY : dbSegment.TextOffsetY,
                Page = clientField.Page > 0 ? clientField.Page : dbSegment.PageNumber,
                X = dbSegment.PositionX,
                Y = dbSegment.PositionY,
                Width = dbSegment.Width,
                Height = dbSegment.Height,
                FontSize = clientField.FontSize ?? dbSegment.FontSize,
                FontName = clientField.FontName ?? dbSegment.FontName,
                Alignment = clientField.Alignment
                    ?? dbSegment.HorizontalAlignment
                    ?? dbSegment.TextAlignment.ToString(),
                VerticalAlignment = clientField.VerticalAlignment ?? dbSegment.VerticalAlignment
            });
        }

        if (layoutFallbackCount > 0)
        {
            _logger.LogInformation(
                "Calibration preview layout fallback from DB: version={VersionId}, fields={Count}",
                templateVersionId,
                layoutFallbackCount);
        }

        return enriched;
    }

    private static bool NeedsDatabaseLayoutFallback(PreviewCalibrationFieldRequest field) =>
        field.Width <= 0 || field.Height <= 0;

    private static bool HasClientCalibrationStyle(PreviewCalibrationFieldRequest? clientField) =>
        clientField is not null;

    private static PreviewCalibrationFieldRequest? ResolveBestClientFieldForDbSegment(
        Guid fieldId,
        int segmentSequence,
        decimal dbPositionX,
        decimal dbPositionY,
        IReadOnlyDictionary<Guid, List<PreviewCalibrationFieldRequest>> clientFieldsByFieldId)
    {
        if (!clientFieldsByFieldId.TryGetValue(fieldId, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        PreviewCalibrationFieldRequest? best = null;
        var bestDistance = decimal.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate.SegmentSequence > 0 && candidate.SegmentSequence != segmentSequence)
            {
                continue;
            }

            var distance = Math.Abs(candidate.X - dbPositionX) + Math.Abs(candidate.Y - dbPositionY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best ?? candidates[^1];
    }

    private static Dictionary<string, string> BuildPreviewTextById(IReadOnlyList<PreviewFieldDto> fields)
    {
        var textById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (!field.TemplateFieldId.HasValue || field.TemplateFieldId.Value == Guid.Empty)
            {
                continue;
            }

            var key = field.TemplateFieldId.Value.ToString("D");
            var text = field.ResolveDrawableText();
            if (!textById.TryGetValue(key, out var existing)
                || text.Length > existing.Length)
            {
                textById[key] = text;
            }
        }

        return textById;
    }

    private static string ResolveCalibrationPreviewText(
        Guid fieldId,
        IReadOnlyDictionary<string, string> textById,
        PreviewCalibrationFieldRequest? clientField = null)
    {
        var key = fieldId.ToString("D");
        if (textById.TryGetValue(key, out var uiText) && !string.IsNullOrWhiteSpace(uiText))
        {
            return uiText.Trim();
        }

        return clientField?.ResolveDrawableText() ?? string.Empty;
    }

    /// <summary>
    /// Сегменти для preview: усі сегменти з БД для полів з UI; текст підставляє renderer через textById.
    /// </summary>
    private async Task<List<ReferralOverlaySegment>> BuildCalibrationPreviewOverlaySegmentsAsync(
        Guid templateVersionId,
        IReadOnlyList<PreviewCalibrationFieldRequest> clientFields,
        IReadOnlyDictionary<string, string> textById,
        CancellationToken cancellationToken)
    {
        var fieldIds = clientFields
            .Where(item => item.TemplateFieldId.HasValue && item.TemplateFieldId.Value != Guid.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item.ResolveDrawableText()))
            .Select(item => item.TemplateFieldId!.Value)
            .Concat(
                textById
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => Guid.TryParse(pair.Key, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty))
            .Distinct()
            .ToList();

        if (fieldIds.Count == 0)
        {
            return clientFields.Select(MapPreviewCalibrationField).ToList();
        }

        var dbRows = await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                      && fieldIds.Contains(field.Id)
                orderby field.SortOrder, segment.Sequence
                select new
                {
                    field.Id,
                    field.TextOffsetX,
                    field.TextOffsetY,
                    segment.PageNumber,
                    segment.PositionX,
                    segment.PositionY,
                    segment.Width,
                    segment.Height,
                    segment.FontSize,
                    segment.FontName,
                    segment.TextAlignment,
                    segment.HorizontalAlignment,
                    segment.VerticalAlignment,
                    segment.Sequence
                })
            .ToListAsync(cancellationToken);

        var clientFieldsByFieldId = clientFields
            .Where(item => item.TemplateFieldId.HasValue && item.TemplateFieldId.Value != Guid.Empty)
            .GroupBy(item => item.TemplateFieldId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var clientByFieldId = clientFieldsByFieldId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value[^1]);

        var segments = new List<ReferralOverlaySegment>();
        var fieldsWithDbSegments = new HashSet<Guid>();

        foreach (var group in dbRows.GroupBy(row => row.Id))
        {
            fieldsWithDbSegments.Add(group.Key);
            foreach (var dbSegment in group.OrderBy(row => row.Sequence))
            {
                var clientField = ResolveBestClientFieldForDbSegment(
                    group.Key,
                    dbSegment.Sequence,
                    dbSegment.PositionX,
                    dbSegment.PositionY,
                    clientFieldsByFieldId);
                var useClientStyle = clientField is not null;
                var useClientLayout = useClientStyle && !NeedsDatabaseLayoutFallback(clientField!);
                var drawText = ResolveCalibrationPreviewText(group.Key, textById, clientField);

                // WYSIWYG: координати з UI, якщо overlay надіслав layout; інакше — сегмент з БД.
                segments.Add(new ReferralOverlaySegment
                {
                    TemplateFieldId = group.Key,
                    SegmentSequence = dbSegment.Sequence,
                    Text = drawText,
                    TextToDraw = drawText,
                    DataFieldId = null,
                    PageNumber = useClientLayout && clientField!.Page > 0 ? clientField.Page : dbSegment.PageNumber,
                    PositionX = useClientLayout ? clientField!.X : dbSegment.PositionX,
                    PositionY = useClientLayout ? clientField!.Y : dbSegment.PositionY,
                    Width = useClientLayout ? clientField!.Width : dbSegment.Width,
                    Height = useClientLayout ? clientField!.Height : dbSegment.Height,
                    TextAlignment = ParseTextAlignment(
                        useClientStyle
                            ? clientField!.Alignment ?? dbSegment.HorizontalAlignment ?? dbSegment.TextAlignment.ToString()
                            : dbSegment.HorizontalAlignment ?? dbSegment.TextAlignment.ToString()),
                    HorizontalAlignment = useClientStyle
                        ? clientField!.Alignment ?? dbSegment.HorizontalAlignment ?? dbSegment.TextAlignment.ToString()
                        : dbSegment.HorizontalAlignment ?? dbSegment.TextAlignment.ToString(),
                    VerticalAlignment = useClientStyle
                        ? clientField!.VerticalAlignment ?? dbSegment.VerticalAlignment ?? "Top"
                        : dbSegment.VerticalAlignment ?? "Top",
                    FontName = useClientStyle ? clientField!.FontName ?? dbSegment.FontName : dbSegment.FontName,
                    FontSize = useClientStyle ? clientField!.FontSize ?? dbSegment.FontSize : dbSegment.FontSize,
                    TextOffsetX = useClientLayout ? clientField!.OffsetX : dbSegment.TextOffsetX,
                    TextOffsetY = useClientLayout ? clientField!.OffsetY : dbSegment.TextOffsetY,
                    TextColor = useClientStyle ? clientField!.TextColor : null
                });
            }
        }

        foreach (var fieldId in fieldIds.Where(id => !fieldsWithDbSegments.Contains(id)))
        {
            if (!clientByFieldId.TryGetValue(fieldId, out var clientField))
            {
                continue;
            }

            segments.Add(MapPreviewCalibrationField(clientField));
        }

        // Додаткові позиції з UI (той самий templateFieldId, інші X/Y) — без другого сегмента в БД.
        foreach (var clientField in clientFields)
        {
            if (!clientField.TemplateFieldId.HasValue
                || clientField.TemplateFieldId.Value == Guid.Empty
                || string.IsNullOrWhiteSpace(clientField.ResolveDrawableText())
                || NeedsDatabaseLayoutFallback(clientField))
            {
                continue;
            }

            var fieldId = clientField.TemplateFieldId.Value;
            var hasMatchingSegment = segments.Any(segment =>
                segment.TemplateFieldId == fieldId
                && segment.SegmentSequence == (clientField.SegmentSequence > 0 ? clientField.SegmentSequence : 1)
                && Math.Abs(segment.PositionX - clientField.X) < 1m
                && Math.Abs(segment.PositionY - clientField.Y) < 1m);

            if (hasMatchingSegment)
            {
                continue;
            }

            // DOM-бокс на інших X/Y (зсув конструктора) — додатковий сегмент з координат UI.
            segments.Add(MapPreviewCalibrationField(clientField));
        }

        if (segments.Count == 0)
        {
            return clientFields.Select(MapPreviewCalibrationField).ToList();
        }

        _logger.LogInformation(
            "Calibration preview segments: dbFields={DbFieldCount}, segmentCount={SegmentCount}, clientOnly={ClientOnly}",
            fieldsWithDbSegments.Count,
            segments.Count,
            fieldIds.Count(id => !fieldsWithDbSegments.Contains(id)));

        return segments;
    }

    private static (
        Dictionary<string, string> BySegment,
        Dictionary<Guid, string> ByTemplateFieldId) BuildUiTextLookups(
        IReadOnlyList<PreviewFieldDto> uiFields)
    {
        var bySegment = new Dictionary<string, string>(StringComparer.Ordinal);
        var byTemplateFieldId = new Dictionary<Guid, string>();
        foreach (var field in uiFields)
        {
            if (!field.TemplateFieldId.HasValue || field.TemplateFieldId.Value == Guid.Empty)
            {
                continue;
            }

            var text = field.ResolveDrawableText();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var fieldId = field.TemplateFieldId.Value;
            var sequence = field.SegmentSequence > 0 ? field.SegmentSequence : 1;
            var positionKey = ReferralPdfOverlayRenderer.BuildUiTextLookupKey(
                fieldId,
                sequence,
                field.X,
                field.Y);
            var sequenceKey = ReferralPdfOverlayRenderer.BuildUiTextLookupKey(fieldId, sequence);
            bySegment[positionKey] = text;
            bySegment[sequenceKey] = text;
            byTemplateFieldId[fieldId] = text;
        }

        return (bySegment, byTemplateFieldId);
    }

    private static PreviewCalibrationFieldRequest MapPreviewFieldDto(PreviewFieldDto field)
    {
        var text = field.ResolveDrawableText();
        return new PreviewCalibrationFieldRequest
        {
            TemplateFieldId = field.TemplateFieldId,
            SegmentSequence = field.SegmentSequence,
            Text = text,
            Value = text,
            TextToDraw = text,
            Page = field.Page,
            X = field.X,
            Y = field.Y,
            Width = field.Width,
            Height = field.Height,
            OffsetX = field.OffsetX,
            OffsetY = field.OffsetY,
            FontSize = field.FontSize,
            FontName = field.FontName,
            Alignment = field.Alignment,
            VerticalAlignment = field.VerticalAlignment,
            TextColor = field.TextColor
        };
    }

    private static ReferralOverlaySegment MapPreviewCalibrationField(PreviewCalibrationFieldRequest field)
    {
        var horizontal = string.IsNullOrWhiteSpace(field.Alignment) ? "Left" : field.Alignment.Trim();

        var drawableText = field.Text ?? field.Value ?? field.TextToDraw ?? string.Empty;
        if (string.IsNullOrWhiteSpace(drawableText))
        {
            drawableText = string.Empty;
        }
        else
        {
            drawableText = drawableText.Trim();
        }

        return new ReferralOverlaySegment
        {
            Text = drawableText,
            TextToDraw = drawableText,
            TemplateFieldId = field.TemplateFieldId,
            SegmentSequence = field.SegmentSequence > 0 ? field.SegmentSequence : 1,
            DataFieldId = null,
            PageNumber = field.Page < 1 ? 1 : field.Page,
            PositionX = field.X,
            PositionY = field.Y,
            Width = field.Width > 0 ? field.Width : 220,
            Height = field.Height > 0 ? field.Height : 28,
            TextAlignment = ParseTextAlignment(horizontal),
            HorizontalAlignment = horizontal,
            VerticalAlignment = field.VerticalAlignment ?? "Top",
            FontName = field.FontName,
            FontSize = field.FontSize,
            TextOffsetX = field.OffsetX,
            TextOffsetY = field.OffsetY,
            TextColor = field.TextColor
        };
    }

    private static TextAlignment ParseTextAlignment(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            _ => TextAlignment.Left
        };

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

        var workspaceKeys = templateFields.Select(field => WorkspaceDataFieldKey(field.Id)).ToList();
        var workspaceDataFieldIds = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .Select(dataField => new { dataField.Key, dataField.Id })
            .ToDictionaryAsync(item => item.Key, item => item.Id, cancellationToken);

        var dataFieldIds = workspaceDataFieldIds.Values
            .Concat(templateFields.Where(field => field.DataFieldId.HasValue).Select(field => field.DataFieldId!.Value))
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
            var workspaceKey = WorkspaceDataFieldKey(field.Id);
            var dataFieldId = workspaceDataFieldIds.TryGetValue(workspaceKey, out var workspaceId)
                ? workspaceId
                : field.DataFieldId;

            if (!dataFieldId.HasValue ||
                !storedByDataFieldId.TryGetValue(dataFieldId.Value, out var storedValue) ||
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

    private static string WorkspaceDataFieldKey(Guid templateFieldId) =>
        templateFieldId.ToString("D");

    private async Task<Dictionary<Guid, Guid>> EnsureWorkspaceDataFieldsAsync(
        IEnumerable<TemplateField> fields,
        CancellationToken cancellationToken)
    {
        var fieldList = fields.ToList();
        if (fieldList.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var keys = fieldList.Select(field => WorkspaceDataFieldKey(field.Id)).ToList();
        var existingByKey = await _context.DataFields
            .Where(dataField => keys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, cancellationToken);

        var result = new Dictionary<Guid, Guid>();
        var created = 0;

        foreach (var field in fieldList)
        {
            var key = WorkspaceDataFieldKey(field.Id);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                result[field.Id] = existing.Id;
                if (!field.DataFieldId.HasValue)
                {
                    field.DataFieldId = existing.Id;
                }

                continue;
            }

            var dataField = new DataField
            {
                Key = key,
                DisplayNameUk = string.IsNullOrWhiteSpace(field.Title) ? field.Tag : field.Title.Trim(),
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Registration,
                IsActive = true,
                IsRequired = field.IsRequired
            };

            _context.DataFields.Add(dataField);
            existingByKey[key] = dataField;
            result[field.Id] = dataField.Id;
            if (!field.DataFieldId.HasValue)
            {
                field.DataFieldId = dataField.Id;
            }

            created++;
            _logger.LogInformation(
                "PdfWorkspaceFill created workspace DataField for TemplateField {TemplateFieldId}, key={Key}",
                field.Id,
                key);
        }

        if (created > 0 || fieldList.Any(field => _context.Entry(field).State == EntityState.Modified))
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static string BuildSaveMessage(
        int received,
        int mapped,
        int saved,
        int skippedUnmapped,
        int skippedEmpty,
        IReadOnlyList<PdfWorkspaceSaveFieldFailure> failures)
    {
        var message =
            $"Прийнято: {received}, зіставлено: {mapped}, збережено: {saved}, " +
            $"пропущено (без мапінгу): {skippedUnmapped}, очищено порожніх: {skippedEmpty}.";

        if (failures.Count == 0)
        {
            return message;
        }

        var details = string.Join(
            "; ",
            failures.Take(5).Select(failure =>
                $"{failure.TemplateFieldId?.ToString("D") ?? "(null)"}: {failure.Reason}"));

        return $"{message} Помилки полів: {details}{(failures.Count > 5 ? "…" : "")}.";
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

        if (layoutRows.Count == 0)
        {
            return ([], []);
        }

        var templateFieldIds = layoutRows.Select(row => row.TemplateFieldId).Distinct().ToList();
        var workspaceKeys = templateFieldIds.Select(WorkspaceDataFieldKey).ToList();
        var workspaceDataFieldIdByTemplateFieldId = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .Select(dataField => new { dataField.Key, dataField.Id })
            .ToDictionaryAsync(
                item => Guid.Parse(item.Key),
                item => item.Id,
                cancellationToken);

        var dataFieldIds = layoutRows
            .Select(row =>
            {
                if (workspaceDataFieldIdByTemplateFieldId.TryGetValue(row.TemplateFieldId, out var workspaceId))
                {
                    return workspaceId;
                }

                return row.DataFieldId != Guid.Empty ? row.DataFieldId : (Guid?)null;
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

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
            var templateFieldId = fieldGroup.Key;
            var rowDataFieldId = fieldGroup.First().DataFieldId;
            var dataFieldId = workspaceDataFieldIdByTemplateFieldId.TryGetValue(templateFieldId, out var workspaceId)
                ? workspaceId
                : rowDataFieldId != Guid.Empty
                    ? rowDataFieldId
                    : (Guid?)null;

            if (!dataFieldId.HasValue ||
                !valuesByDataFieldId.TryGetValue(dataFieldId.Value, out var storedValue) ||
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
                    DataFieldId = dataFieldId,
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
