using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class PdfWorkspaceFillService : IPdfWorkspaceFillService
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateDocumentStorage _templateDocumentStorage;
    private readonly IOrderFieldValueService _orderFieldValueService;
    private readonly ITemplateFieldPermissionService _fieldPermissions;
    private readonly ICurrentUserService _currentUser;
    private readonly ReferralPdfOverlayRenderer _overlayRenderer;
    private readonly ILogger<PdfWorkspaceFillService> _logger;
    private readonly bool _allowImplicitOrderCreation;

    public PdfWorkspaceFillService(
        ApplicationDbContext context,
        ITemplateDocumentStorage templateDocumentStorage,
        IOrderFieldValueService orderFieldValueService,
        ITemplateFieldPermissionService fieldPermissions,
        ICurrentUserService currentUser,
        ILogger<PdfWorkspaceFillService> logger,
        ILogger<ReferralPdfOverlayRenderer> overlayLogger,
        IHostEnvironment hostEnvironment)
    {
        _context = context;
        _templateDocumentStorage = templateDocumentStorage;
        _orderFieldValueService = orderFieldValueService;
        _fieldPermissions = fieldPermissions;
        _currentUser = currentUser;
        _logger = logger;
        _overlayRenderer = new ReferralPdfOverlayRenderer(overlayLogger);
        _allowImplicitOrderCreation = hostEnvironment.IsDevelopment();
    }

    public Task<PdfWorkspaceSaveResult> SaveValuesAsync(
        Guid templateVersionId,
        Guid? orderId,
        IReadOnlyList<PdfWorkspaceFieldValueDto> values,
        CancellationToken cancellationToken = default) =>
        SaveValuesAsync(templateVersionId, orderId, null, values, cancellationToken);

    public async Task<PdfWorkspaceSaveResult> SaveValuesAsync(
        Guid templateVersionId,
        Guid? orderId,
        Guid? orderDocumentId,
        IReadOnlyList<PdfWorkspaceFieldValueDto> values,
        CancellationToken cancellationToken = default)
    {
        var received = values.Count;
        var mapped = 0;
        var saved = 0;
        var skippedUnmapped = 0;
        var cleared = 0;
        var failures = new List<PdfWorkspaceSaveFieldFailure>();

        _logger.LogInformation(
            "PdfWorkspaceFill SaveValuesAsync: version={VersionId}, order={OrderId}, received={Received}",
            templateVersionId,
            orderId,
            received);

        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);

        var order = await EnsureOrderAsync(orderId, templateVersionId, cancellationToken);
        var sampleId = orderDocumentId.HasValue
            ? (Guid?)(await ResolveOrderDocumentAsync(order, templateVersionId, orderDocumentId, cancellationToken)).SampleId
            : null;

        var accessByFieldId = await _fieldPermissions.GetFieldAccessLevelsForVersionAsync(
            templateVersionId,
            cancellationToken);

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

        var workspaceDataFieldIdByTemplateFieldId = await ResolveStorageDataFieldIdsAsync(
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
        var dataFieldsById = workspaceDataFieldIds.Count == 0
            ? new Dictionary<Guid, DataField>()
            : await _context.DataFields
                .Where(dataField => workspaceDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
                .ToDictionaryAsync(dataField => dataField.Id, cancellationToken);

        var existingValues = workspaceDataFieldIds.Count == 0
            ? []
            : await _context.OrderFieldValues
                .Where(fieldValue => fieldValue.OrderId == order.Id &&
                                     fieldValue.SampleId == sampleId &&
                                     workspaceDataFieldIds.Contains(fieldValue.DataFieldId))
                .ToListAsync(cancellationToken);

        var resultDataFieldIds = workspaceDataFieldIds
            .Where(id => dataFieldsById.TryGetValue(id, out var dataField)
                         && dataField.Scope == DataFieldScope.Result)
            .ToList();

        var existingResultValues = sampleId.HasValue && resultDataFieldIds.Count > 0
            ? await _context.SampleResultValues
                .Where(resultValue => resultValue.SampleId == sampleId.Value
                                      && resultDataFieldIds.Contains(resultValue.DataFieldId))
                .ToListAsync(cancellationToken)
            : [];

        Guid? defaultEquipmentId = null;
        var enteredByUserId = string.IsNullOrWhiteSpace(_currentUser.UserId)
            ? "system"
            : _currentUser.UserId;

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

            if (!accessByFieldId.TryGetValue(templateFieldId, out var accessLevel)
                || accessLevel < FieldAccessLevel.Write)
            {
                skippedUnmapped++;
                failures.Add(new PdfWorkspaceSaveFieldFailure
                {
                    TemplateFieldId = templateFieldId,
                    Reason = "Немає права на запис у це поле для активної ролі."
                });
                _logger.LogWarning(
                    "PdfWorkspaceFill skip: write denied for {TemplateFieldId}, access={Access}",
                    templateFieldId,
                    accessByFieldId.GetValueOrDefault(templateFieldId, FieldAccessLevel.None));
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
            dataFieldsById.TryGetValue(dataFieldId, out var dataField);
            var persistAsSampleResult = sampleId.HasValue
                && dataField?.Scope == DataFieldScope.Result;

            if (persistAsSampleResult)
            {
                if (string.IsNullOrEmpty(trimmedValue))
                {
                    var activeResult = existingResultValues
                        .FirstOrDefault(resultValue => resultValue.DataFieldId == dataFieldId);
                    if (activeResult is not null)
                    {
                        activeResult.Annul("Очищено у PDF Workspace", enteredByUserId);
                        cleared++;
                        _logger.LogDebug(
                            "PdfWorkspaceFill annulled result: sample={SampleId}, dataField={DataFieldId}",
                            sampleId,
                            dataFieldId);
                    }

                    continue;
                }

                defaultEquipmentId ??= await ResolveDefaultEquipmentIdAsync(cancellationToken);
                var active = existingResultValues
                    .FirstOrDefault(resultValue => resultValue.DataFieldId == dataFieldId);
                if (active is not null
                    && string.Equals(active.StoredValue, trimmedValue, StringComparison.Ordinal))
                {
                    saved++;
                    continue;
                }

                if (active is not null)
                {
                    active.Annul("Оновлено у PDF Workspace", enteredByUserId);
                    existingResultValues.Remove(active);
                }

                var resultValue = new SampleResultValue(
                    sampleId!.Value,
                    dataFieldId,
                    trimmedValue,
                    ResolveResultUnit(dataField!),
                    0m,
                    defaultEquipmentId.Value,
                    DateTime.UtcNow,
                    enteredByUserId);
                _context.SampleResultValues.Add(resultValue);
                existingResultValues.Add(resultValue);
                saved++;
                _logger.LogInformation(
                    "PdfWorkspaceFill insert result: sample={SampleId}, templateField={TemplateFieldId}, dataField={DataFieldId}, length={Length}",
                    sampleId,
                    templateFieldId,
                    dataFieldId,
                    trimmedValue.Length);
                continue;
            }

            if (string.IsNullOrEmpty(trimmedValue))
            {
                var existing = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
                if (existing is not null && !string.IsNullOrEmpty(existing.StoredValue))
                {
                    existing.StoredValue = null;
                    cleared++;
                    _logger.LogDebug(
                        "PdfWorkspaceFill cleared empty value: templateField={TemplateFieldId}, dataField={DataFieldId}",
                        templateFieldId,
                        dataFieldId);
                }
                continue;
            }

            var stored = existingValues.FirstOrDefault(fieldValue => fieldValue.DataFieldId == dataFieldId);
            if (stored is null)
            {
                var created = new OrderFieldValue
                {
                    OrderId = order.Id,
                    SampleId = sampleId,
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

        var promoteOrderToRegistered = failures.Count == 0
            && (saved > 0 || cleared > 0)
            && order.Status == OrderStatus.Draft;
        if (promoteOrderToRegistered)
        {
            order.Status = OrderStatus.Registered;
            order.RegisteredAtUtc ??= DateTime.UtcNow;
            _logger.LogInformation(
                "PdfWorkspaceFill promoted order to Registered: order={OrderId}",
                order.Id);
        }

        if (orderDocumentId.HasValue
            && failures.Count == 0
            && (saved > 0 || cleared > 0))
        {
            var document = await ResolveOrderDocumentAsync(
                order,
                templateVersionId,
                orderDocumentId,
                cancellationToken);
            if (document.Status == OrderDocumentStatus.SentToLab)
            {
                document.Status = OrderDocumentStatus.InProgress;
                _logger.LogInformation(
                    "PdfWorkspaceFill promoted document to InProgress: orderDocument={OrderDocumentId}",
                    document.Id);
            }

            var sample = order.Samples.FirstOrDefault(item => item.Id == document.SampleId);
            if (sample?.Status == SampleStatus.Routed)
            {
                sample.Status = SampleStatus.InProgress;
            }
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

        var message = BuildSaveMessage(received, mapped, saved, skippedUnmapped, cleared, failures);
        return new PdfWorkspaceSaveResult
        {
            OrderId = order.Id,
            Received = received,
            Mapped = mapped,
            Saved = saved,
            SkippedUnmapped = skippedUnmapped,
            SkippedEmpty = cleared,
            FailedFields = failures,
            Message = message
        };
    }

    public async Task<byte[]> GenerateFilledPdfAsync(
        Guid templateVersionId,
        Guid orderId,
        Guid? orderDocumentId = null,
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
            orderDocumentId,
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

        _logger.LogInformation(
            "Preview using UI fields: {FieldCount} (WYSIWYG — один сегмент на запис з UI)",
            request.Fields.Count);

        // WYSIWYG: один overlay-сегмент на кожен запис з UI; БД лише fallback геометрії в Enrich*.
        var overlaySegments = await BuildCalibrationPreviewOverlaySegmentsAsync(
            templateVersionId,
            fields,
            cancellationToken);

        await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
            version.StorageKey,
            cancellationToken);

        if (originalPdfStream.CanSeek)
        {
            originalPdfStream.Position = 0;
        }

        // Текст уже на кожному сегменті (Text/TextToDraw) — не підміняти одним textById на поле.
        var renderStats = _overlayRenderer.RenderWithStats(
            originalPdfStream,
            overlaySegments,
            valuesByDataFieldId: new Dictionary<Guid, string?>(),
            skipEmptyText: false,
            textById: null);

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

    /// <summary>
    /// WYSIWYG preview: рівно один PDF-сегмент на кожен запис з UI (після Enrich* fallback).
    /// Не змішує всі сегменти з БД — інакше подвоєння тексту при локальному зсуві тега.
    /// </summary>
    private async Task<List<ReferralOverlaySegment>> BuildCalibrationPreviewOverlaySegmentsAsync(
        Guid templateVersionId,
        IReadOnlyList<PreviewCalibrationFieldRequest> clientFields,
        CancellationToken cancellationToken)
    {
        var drawableFields = clientFields
            .Where(item => !string.IsNullOrWhiteSpace(item.ResolveDrawableText()))
            .ToList();

        if (drawableFields.Count == 0)
        {
            return [];
        }

        var fieldIdsNeedingStyle = drawableFields
            .Where(item => item.TemplateFieldId.HasValue && item.TemplateFieldId.Value != Guid.Empty)
            .Where(item => !NeedsDatabaseLayoutFallback(item))
            .Where(item => item.FontSize is null or <= 0 || string.IsNullOrWhiteSpace(item.FontName))
            .Select(item => item.TemplateFieldId!.Value)
            .Distinct()
            .ToList();

        var dbStyleByFieldId = fieldIdsNeedingStyle.Count == 0
            ? new Dictionary<Guid, (decimal? FontSize, string? FontName, string? HorizontalAlignment, string? VerticalAlignment)>()
            : (await (
                    from segment in _context.TemplateFieldSegments.AsNoTracking()
                    join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                    where field.TemplateVersionId == templateVersionId
                          && !field.IsAnnulled
                          && !segment.IsAnnulled
                          && fieldIdsNeedingStyle.Contains(field.Id)
                    orderby field.Id, segment.IsPrimary descending, segment.Sequence
                    select new
                    {
                        field.Id,
                        segment.FontSize,
                        segment.FontName,
                        segment.HorizontalAlignment,
                        segment.VerticalAlignment,
                        segment.TextAlignment
                    })
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.Id)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var item = group.First();
                        return (
                            FontSize: item.FontSize,
                            FontName: item.FontName,
                            HorizontalAlignment: item.HorizontalAlignment ?? item.TextAlignment.ToString(),
                            VerticalAlignment: item.VerticalAlignment);
                    });

        var segments = new List<ReferralOverlaySegment>(drawableFields.Count);
        foreach (var clientField in drawableFields)
        {
            if (clientField.TemplateFieldId is Guid fieldId
                && dbStyleByFieldId.TryGetValue(fieldId, out var dbStyle))
            {
                clientField.FontSize ??= dbStyle.FontSize;
                clientField.FontName ??= dbStyle.FontName;
                clientField.Alignment ??= dbStyle.HorizontalAlignment;
                clientField.VerticalAlignment ??= dbStyle.VerticalAlignment;
            }

            segments.Add(MapPreviewCalibrationField(clientField));
        }

        _logger.LogInformation(
            "Calibration preview segments (WYSIWYG): clientFields={ClientCount}, segmentCount={SegmentCount}",
            drawableFields.Count,
            segments.Count);

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

    public async Task<int> GetLayoutSegmentCountAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                select segment.Id)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PdfWorkspaceFillSegmentDto>> GetFillSegmentsAsync(
        Guid templateVersionId,
        Guid? orderDocumentId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);

        var orderOverrides = await LoadOrderDocumentLayoutOverridesAsync(orderDocumentId, cancellationToken);

        var accessByFieldId = await _fieldPermissions.GetFieldAccessLevelsForVersionAsync(
            templateVersionId,
            cancellationToken);

        var rows = await (
                from segment in _context.TemplateFieldSegments.AsNoTracking()
                join field in _context.TemplateFields.AsNoTracking() on segment.TemplateFieldId equals field.Id
                join dataField in _context.DataFields.AsNoTracking()
                    on field.DataFieldId equals dataField.Id into dataFieldGroup
                from dataField in dataFieldGroup.DefaultIfEmpty()
                where field.TemplateVersionId == templateVersionId
                      && !field.IsAnnulled
                      && !segment.IsAnnulled
                orderby field.SortOrder, segment.Sequence
                select new
                {
                    Segment = segment,
                    Field = field,
                    DataFieldKey = dataField != null ? dataField.Key : null,
                    DataFieldId = field.DataFieldId
                })
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => accessByFieldId.TryGetValue(row.Field.Id, out var accessLevel)
                          && accessLevel >= FieldAccessLevel.Read)
            .Select(row =>
            {
                orderOverrides.TryGetValue(row.Segment.Id, out var layoutOverride);
                var segment = new PdfWorkspaceFillSegmentDto
                {
                    SegmentId = row.Segment.Id,
                    TemplateFieldId = row.Field.Id,
                    Tag = row.Field.Tag,
                    Title = row.Field.Title,
                    DataFieldId = row.DataFieldId,
                    DataFieldKey = row.DataFieldKey ?? row.Field.Tag,
                    Sequence = row.Segment.Sequence,
                    PageNumber = row.Segment.PageNumber,
                    PositionX = row.Segment.PositionX,
                    PositionY = row.Segment.PositionY,
                    Width = row.Segment.Width,
                    Height = row.Segment.Height,
                    AllowMultiline = row.Field.AllowMultiline,
                    TextOffsetX = row.Field.TextOffsetX,
                    TextOffsetY = row.Field.TextOffsetY,
                    FontSize = row.Segment.FontSize,
                    FontName = row.Segment.FontName,
                    HorizontalAlignment = row.Segment.HorizontalAlignment ?? row.Segment.TextAlignment.ToString(),
                    VerticalAlignment = row.Segment.VerticalAlignment,
                    TextAlignment = row.Segment.TextAlignment.ToString(),
                    LineHeight = row.Segment.LineHeight,
                    SvgPathData = row.Segment.SvgPathData,
                    IsPrimary = row.Segment.IsPrimary,
                    SegmentRowVersion = row.Segment.RowVersion,
                    AccessLevel = accessByFieldId[row.Field.Id]
                };

                return OrderDocumentLayoutOverridesJson.Apply(segment, layoutOverride);
            })
            .ToList();
    }

    public async Task<PdfWorkspaceFillLayoutSaveResult> SaveOrderDocumentLayoutOverridesAsync(
        Guid templateVersionId,
        Guid orderId,
        Guid? orderDocumentId,
        IReadOnlyList<PdfWorkspaceFillLayoutFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0)
        {
            return new PdfWorkspaceFillLayoutSaveResult
            {
                Saved = 0,
                Message = "Немає змін макету для збереження."
            };
        }

        await EnsureTemplateVersionExistsAsync(templateVersionId, cancellationToken);
        var order = await EnsureOrderAsync(orderId, templateVersionId, cancellationToken);
        var document = await ResolveOrderDocumentAsync(order, templateVersionId, orderDocumentId, cancellationToken);

        var existing = OrderDocumentLayoutOverridesJson.Deserialize(document.SegmentLayoutOverridesJson);
        var saved = 0;

        foreach (var update in updates)
        {
            if (update.SegmentId == Guid.Empty)
            {
                continue;
            }

            existing[update.SegmentId] = OrderDocumentLayoutOverridesJson.FromFieldUpdate(update);
            saved++;
        }

        if (saved == 0)
        {
            return new PdfWorkspaceFillLayoutSaveResult
            {
                Saved = 0,
                Message = "Жодне поле не оновлено (перевірте segmentId)."
            };
        }

        document.SegmentLayoutOverridesJson = OrderDocumentLayoutOverridesJson.Serialize(existing);
        document.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new PdfWorkspaceFillLayoutSaveResult
        {
            Saved = saved,
            Message = $"Макет збережено для цього замовлення: полів {saved}."
        };
    }

    private async Task<Dictionary<Guid, OrderSegmentLayoutOverride>> LoadOrderDocumentLayoutOverridesAsync(
        Guid? orderDocumentId,
        CancellationToken cancellationToken)
    {
        if (!orderDocumentId.HasValue || orderDocumentId.Value == Guid.Empty)
        {
            return new Dictionary<Guid, OrderSegmentLayoutOverride>();
        }

        var json = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document => document.Id == orderDocumentId.Value && !document.IsAnnulled)
            .Select(document => document.SegmentLayoutOverridesJson)
            .FirstOrDefaultAsync(cancellationToken);

        return OrderDocumentLayoutOverridesJson.Deserialize(json);
    }

    private static OverlaySegmentJoinRow ApplyOrderLayoutOverride(
        OverlaySegmentJoinRow row,
        IReadOnlyDictionary<Guid, OrderSegmentLayoutOverride> overrides)
    {
        if (!overrides.TryGetValue(row.SegmentId, out var ov))
        {
            return row;
        }

        return row with
        {
            TextOffsetX = ov.TextOffsetX,
            TextOffsetY = ov.TextOffsetY,
            FontSize = ov.FontSize ?? row.FontSize,
            FontName = ov.FontName ?? row.FontName,
            HorizontalAlignment = ov.HorizontalAlignment ?? row.HorizontalAlignment,
            VerticalAlignment = ov.VerticalAlignment ?? row.VerticalAlignment,
            TextAlignment = ov.TextAlignment?.Trim().ToLowerInvariant() switch
            {
                "center" => TextAlignment.Center,
                "right" => TextAlignment.Right,
                "left" => TextAlignment.Left,
                _ => row.TextAlignment
            }
        };
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetSavedValuesByKeyAsync(
        Guid orderId,
        Guid templateVersionId,
        Guid? orderDocumentId = null,
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
        var workspaceDataFieldIdsByKey = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, dataField => dataField.Id, cancellationToken);

        var dataFieldIds = templateFields
            .Select(field => ResolveStorageDataFieldId(
                field.Id,
                field.DataFieldId,
                field.DataFieldKey,
                workspaceDataFieldIdsByKey))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (dataFieldIds.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var sampleId = orderDocumentId.HasValue
            ? await ResolveOrderDocumentSampleIdAsync(
                orderId,
                templateVersionId,
                orderDocumentId,
                cancellationToken)
            : (Guid?)null;

        var storedByDataFieldId = await ResolveStoredValuesByDataFieldIdAsync(
            orderId,
            sampleId,
            dataFieldIds,
            cancellationToken);

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in templateFields)
        {
            var storageDataFieldId = ResolveStorageDataFieldId(
                field.Id,
                field.DataFieldId,
                field.DataFieldKey,
                workspaceDataFieldIdsByKey);

            if (!storageDataFieldId.HasValue ||
                !storedByDataFieldId.TryGetValue(storageDataFieldId.Value, out var storedValue) ||
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
                result[$"{field.Id:D}#{field.Segments[index].Sequence}"] = line;
                result[$"{field.Tag}#{field.Segments[index].Sequence}"] = line;
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, string?>> ResolveStoredValuesByDataFieldIdAsync(
        Guid orderId,
        Guid? sampleId,
        IReadOnlyList<Guid> dataFieldIds,
        CancellationToken cancellationToken)
    {
        if (dataFieldIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var orderFieldValues = await _context.OrderFieldValues
            .AsNoTracking()
            .Where(fieldValue => fieldValue.OrderId == orderId &&
                                 (!sampleId.HasValue || fieldValue.SampleId == null || fieldValue.SampleId == sampleId) &&
                                 dataFieldIds.Contains(fieldValue.DataFieldId))
            .Select(fieldValue => new OrderFieldValueCandidate(
                fieldValue.DataFieldId,
                fieldValue.SampleId,
                fieldValue.StoredValue,
                fieldValue.UpdatedAtUtc,
                fieldValue.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var storedByDataFieldId = OrderFieldValueSelection.ResolveByDataFieldId(orderFieldValues, sampleId);

        if (!sampleId.HasValue)
        {
            return storedByDataFieldId;
        }

        var resultFieldIds = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => dataFieldIds.Contains(dataField.Id)
                                && dataField.IsActive
                                && dataField.Scope == DataFieldScope.Result)
            .Select(dataField => dataField.Id)
            .ToListAsync(cancellationToken);

        if (resultFieldIds.Count == 0)
        {
            return storedByDataFieldId;
        }

        var sampleResults = await _context.SampleResultValues
            .AsNoTracking()
            .Where(resultValue => resultValue.SampleId == sampleId.Value
                                  && resultFieldIds.Contains(resultValue.DataFieldId))
            .Select(resultValue => new
            {
                resultValue.DataFieldId,
                resultValue.StoredValue
            })
            .ToListAsync(cancellationToken);

        foreach (var sampleResult in sampleResults)
        {
            storedByDataFieldId[sampleResult.DataFieldId] = sampleResult.StoredValue;
        }

        return storedByDataFieldId;
    }

    private async Task<Guid> ResolveDefaultEquipmentIdAsync(CancellationToken cancellationToken)
    {
        var equipmentId = await _context.Equipment
            .AsNoTracking()
            .Where(equipment => equipment.IsActive && !equipment.IsAnnulled)
            .OrderBy(equipment => equipment.Code)
            .Select(equipment => equipment.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (equipmentId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Немає активного обладнання для запису результатів лабораторії.");
        }

        return equipmentId;
    }

    private static string ResolveResultUnit(DataField dataField) =>
        string.IsNullOrWhiteSpace(dataField.Unit) ? "—" : dataField.Unit.Trim();

    private static string WorkspaceDataFieldKey(Guid templateFieldId) =>
        templateFieldId.ToString("D");

    private static bool IsWorkspaceDataFieldKey(string dataFieldKey, Guid templateFieldId) =>
        string.Equals(dataFieldKey, WorkspaceDataFieldKey(templateFieldId), StringComparison.Ordinal);

    private static Guid? ResolveStorageDataFieldId(
        Guid templateFieldId,
        Guid? dataFieldId,
        string? dataFieldKey,
        IReadOnlyDictionary<string, Guid> workspaceDataFieldIdsByKey)
    {
        if (dataFieldId.HasValue
            && !string.IsNullOrWhiteSpace(dataFieldKey)
            && !IsWorkspaceDataFieldKey(dataFieldKey, templateFieldId))
        {
            return dataFieldId;
        }

        return workspaceDataFieldIdsByKey.TryGetValue(WorkspaceDataFieldKey(templateFieldId), out var workspaceId)
            ? workspaceId
            : dataFieldId;
    }

    private async Task<Dictionary<Guid, Guid>> ResolveStorageDataFieldIdsAsync(
        IEnumerable<TemplateField> fields,
        CancellationToken cancellationToken)
    {
        var fieldList = fields.ToList();
        if (fieldList.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var mappedDataFieldIds = fieldList
            .Where(field => field.DataFieldId.HasValue)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToList();

        var dataFieldKeysById = mappedDataFieldIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.DataFields
                .Where(dataField => mappedDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
                .ToDictionaryAsync(dataField => dataField.Id, dataField => dataField.Key, cancellationToken);

        var workspaceKeys = fieldList.Select(field => WorkspaceDataFieldKey(field.Id)).ToList();
        var existingWorkspaceByKey = await _context.DataFields
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, cancellationToken);

        var result = new Dictionary<Guid, Guid>();
        var created = 0;

        foreach (var field in fieldList)
        {
            if (field.DataFieldId.HasValue
                && dataFieldKeysById.TryGetValue(field.DataFieldId.Value, out var mappedKey)
                && !IsWorkspaceDataFieldKey(mappedKey, field.Id))
            {
                result[field.Id] = field.DataFieldId.Value;
                continue;
            }

            var workspaceKey = WorkspaceDataFieldKey(field.Id);
            if (existingWorkspaceByKey.TryGetValue(workspaceKey, out var existingWorkspace))
            {
                result[field.Id] = existingWorkspace.Id;
                if (!field.DataFieldId.HasValue
                    || (dataFieldKeysById.TryGetValue(field.DataFieldId.Value, out var currentKey)
                        && IsWorkspaceDataFieldKey(currentKey, field.Id)))
                {
                    field.DataFieldId = existingWorkspace.Id;
                }

                continue;
            }

            var dataField = new DataField
            {
                Key = workspaceKey,
                DisplayNameUk = string.IsNullOrWhiteSpace(field.Title) ? field.Tag : field.Title.Trim(),
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Registration,
                IsActive = true,
                IsRequired = field.IsRequired
            };

            _context.DataFields.Add(dataField);
            existingWorkspaceByKey[workspaceKey] = dataField;
            result[field.Id] = dataField.Id;
            if (!field.DataFieldId.HasValue)
            {
                field.DataFieldId = dataField.Id;
            }

            created++;
            _logger.LogInformation(
                "PdfWorkspaceFill created workspace DataField for TemplateField {TemplateFieldId}, key={Key}",
                field.Id,
                workspaceKey);
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
        int cleared,
        IReadOnlyList<PdfWorkspaceSaveFieldFailure> failures)
    {
        var message =
            $"Прийнято: {received}, зіставлено: {mapped}, збережено: {saved}, " +
            $"пропущено (без мапінгу): {skippedUnmapped}, очищено: {cleared}.";

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
                await EnsureOrderDocumentAsync(existing, templateVersionId, cancellationToken);
                return existing;
            }
        }

        if (!_allowImplicitOrderCreation)
        {
            throw new InvalidOperationException(
                "Спочатку створіть замовлення в реєстрі, потім відкрийте PDF Workspace з посиланням «Відкрити PDF».");
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

    private async Task<OrderDocument> EnsureOrderDocumentAsync(
        Order order,
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        var existingDocument = order.OrderDocuments
            .FirstOrDefault(document => document.TemplateVersionId == templateVersionId && !document.IsAnnulled);
        if (existingDocument is not null)
        {
            return existingDocument;
        }

        var linked = await _context.OrderDocuments
            .FirstOrDefaultAsync(
                document => document.OrderId == order.Id &&
                            document.TemplateVersionId == templateVersionId &&
                            !document.IsAnnulled,
                cancellationToken);

        if (linked is not null)
        {
            order.OrderDocuments.Add(linked);
            return linked;
        }

        var version = await _context.TemplateVersions
            .AsNoTracking()
            .FirstAsync(item => item.Id == templateVersionId, cancellationToken);

        var sample = await EnsureSampleAsync(order, cancellationToken);

        var created = new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = version.TemplateId,
            TemplateVersionId = version.Id,
            TargetBranchId = order.BranchId,
            Status = OrderDocumentStatus.Pending
        };

        _context.OrderDocuments.Add(created);
        await _context.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task<OrderDocument> ResolveOrderDocumentAsync(
        Order order,
        Guid templateVersionId,
        Guid? orderDocumentId,
        CancellationToken cancellationToken)
    {
        if (orderDocumentId is Guid documentId)
        {
            var document = order.OrderDocuments
                .FirstOrDefault(item => item.Id == documentId && !item.IsAnnulled)
                ?? await _context.OrderDocuments
                    .FirstOrDefaultAsync(
                        item => item.Id == documentId &&
                                item.OrderId == order.Id &&
                                !item.IsAnnulled,
                        cancellationToken);

            if (document is null || document.TemplateVersionId != templateVersionId)
            {
                throw new InvalidOperationException("PDF-документ не належить цьому замовленню або шаблону.");
            }

            return document;
        }

        var matchingDocuments = order.OrderDocuments
            .Where(document => document.TemplateVersionId == templateVersionId && !document.IsAnnulled)
            .GroupBy(document => document.Id)
            .Select(group => group.First())
            .ToList();
        if (matchingDocuments.Count == 1)
        {
            return matchingDocuments[0];
        }

        if (matchingDocuments.Count > 1)
        {
            throw new InvalidOperationException("Для цього шаблону в замовленні є кілька документів. Відкрийте конкретний документ зі сторінки справи.");
        }

        return await EnsureOrderDocumentAsync(order, templateVersionId, cancellationToken);
    }

    private async Task<Guid> ResolveOrderDocumentSampleIdAsync(
        Guid orderId,
        Guid templateVersionId,
        Guid? orderDocumentId,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(item => item.OrderDocuments)
            .FirstOrDefaultAsync(item => item.Id == orderId && !item.IsAnnulled, cancellationToken)
            ?? throw new InvalidOperationException("Замовлення не знайдено.");

        var document = await ResolveOrderDocumentAsync(order, templateVersionId, orderDocumentId, cancellationToken);
        return document.SampleId;
    }

    private async Task<(IReadOnlyList<ReferralOverlaySegment> Segments, Dictionary<Guid, string?> ValuesByDataFieldId)>
        LoadOverlayRenderDataAsync(
            Guid templateVersionId,
            Guid orderId,
            Guid? orderDocumentId,
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
                    segment.Id,
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

        var orderOverrides = await LoadOrderDocumentLayoutOverridesAsync(orderDocumentId, cancellationToken);
        if (orderOverrides.Count > 0)
        {
            layoutRows = layoutRows
                .Select(row => ApplyOrderLayoutOverride(row, orderOverrides))
                .ToList();
        }

        var templateFieldIds = layoutRows.Select(row => row.TemplateFieldId).Distinct().ToList();
        var workspaceKeys = templateFieldIds.Select(WorkspaceDataFieldKey).ToList();
        var workspaceDataFieldIdsByKey = await _context.DataFields
            .AsNoTracking()
            .Where(dataField => workspaceKeys.Contains(dataField.Key) && dataField.IsActive)
            .ToDictionaryAsync(dataField => dataField.Key, dataField => dataField.Id, cancellationToken);

        var mappedDataFieldIds = layoutRows
            .Where(row => row.DataFieldId != Guid.Empty)
            .Select(row => row.DataFieldId)
            .Distinct()
            .ToList();

        var dataFieldKeysById = mappedDataFieldIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.DataFields
                .AsNoTracking()
                .Where(dataField => mappedDataFieldIds.Contains(dataField.Id) && dataField.IsActive)
                .ToDictionaryAsync(dataField => dataField.Id, dataField => dataField.Key, cancellationToken);

        var dataFieldIds = layoutRows
            .Select(row =>
            {
                dataFieldKeysById.TryGetValue(row.DataFieldId, out var mappedKey);
                return ResolveStorageDataFieldId(
                    row.TemplateFieldId,
                    row.DataFieldId != Guid.Empty ? row.DataFieldId : null,
                    mappedKey,
                    workspaceDataFieldIdsByKey);
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var sampleId = orderDocumentId.HasValue
            ? await ResolveOrderDocumentSampleIdAsync(
                orderId,
                templateVersionId,
                orderDocumentId,
                cancellationToken)
            : (Guid?)null;

        var valuesByDataFieldId = await ResolveStoredValuesByDataFieldIdAsync(
            orderId,
            sampleId,
            dataFieldIds,
            cancellationToken);

        if (valuesByDataFieldId.Count == 0)
        {
            return ([], valuesByDataFieldId);
        }

        var overlaySegments = new List<ReferralOverlaySegment>();

        foreach (var fieldGroup in layoutRows.GroupBy(row => row.TemplateFieldId))
        {
            var templateFieldId = fieldGroup.Key;
            var rowDataFieldId = fieldGroup.First().DataFieldId;
            dataFieldKeysById.TryGetValue(rowDataFieldId, out var mappedKey);
            var dataFieldId = ResolveStorageDataFieldId(
                templateFieldId,
                rowDataFieldId != Guid.Empty ? rowDataFieldId : null,
                mappedKey,
                workspaceDataFieldIdsByKey);

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
        Guid SegmentId,
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
