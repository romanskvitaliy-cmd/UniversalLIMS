using Microsoft.EntityFrameworkCore;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class ReferralPdfGenerator : IReferralPdfGenerator
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateDocumentStorage _templateDocumentStorage;
    private readonly RegistrationFieldValueResolver _fieldValueResolver;
    private readonly ReferralPdfOverlayRenderer _overlayRenderer;

    public ReferralPdfGenerator(
        ApplicationDbContext context,
        ITemplateDocumentStorage templateDocumentStorage)
    {
        _context = context;
        _templateDocumentStorage = templateDocumentStorage;
        _fieldValueResolver = new RegistrationFieldValueResolver();
        _overlayRenderer = new ReferralPdfOverlayRenderer();
    }

    public async Task<byte[]> GenerateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Branch)
            .Include(item => item.Samples)
            .Include(item => item.FieldValues)
                .ThenInclude(fieldValue => fieldValue.DataField)
            .Include(item => item.OrderDocuments)
                .ThenInclude(document => document.TemplateVersion)
            .Include(item => item.OrderDocuments)
                .ThenInclude(document => document.Sample)
            .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException("Замовлення не знайдено.");
        }

        if (order.OrderDocuments.Count == 0)
        {
            throw new InvalidOperationException("Для замовлення не створено жодного OrderDocument.");
        }

        var dynamicValuesByKey = order.FieldValues
            .Where(fieldValue => fieldValue.DataField is not null)
            .GroupBy(fieldValue => fieldValue.DataField!.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(fieldValue => fieldValue.UpdatedAtUtc ?? fieldValue.CreatedAtUtc)
                    .First()
                    .StoredValue,
                StringComparer.Ordinal);

        using var mergedDocument = new PdfDocument();

        foreach (var orderDocument in order.OrderDocuments.OrderBy(document => document.CreatedAtUtc))
        {
            var templateVersion = orderDocument.TemplateVersion;
            if (templateVersion is null)
            {
                throw new InvalidOperationException("Версія шаблону для документа не знайдена.");
            }

            if (!await _templateDocumentStorage.ExistsAsync(templateVersion.StorageKey, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Оригінальний PDF шаблону не знайдено у сховищі: {templateVersion.StorageKey}");
            }

            await using var originalPdfStream = await _templateDocumentStorage.OpenReadAsync(
                templateVersion.StorageKey,
                cancellationToken);

            var segments = await LoadOverlaySegmentsAsync(templateVersion.Id, cancellationToken);
            var valuesByDataFieldId = await BuildValuesByDataFieldIdAsync(
                order,
                orderDocument.Sample,
                dynamicValuesByKey,
                segments,
                cancellationToken);

            var renderedBytes = _overlayRenderer.Render(originalPdfStream, segments, valuesByDataFieldId);

            using var renderedStream = new MemoryStream(renderedBytes);
            using var renderedDocument = new PdfLoadedDocument(renderedStream);
            mergedDocument.ImportPageRange(renderedDocument, 0, renderedDocument.Pages.Count - 1);
        }

        using var output = new MemoryStream();
        mergedDocument.Save(output);
        return output.ToArray();
    }

    private async Task<IReadOnlyList<ReferralOverlaySegment>> LoadOverlaySegmentsAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken)
    {
        return await _context.TemplateFields
            .AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId && field.DataFieldId != null)
            .SelectMany(field => field.Segments
                .Where(segment => !segment.IsAnnulled)
                .Select(segment => new ReferralOverlaySegment
                {
                    DataFieldId = field.DataFieldId,
                    PageNumber = segment.PageNumber,
                    PositionX = segment.PositionX,
                    PositionY = segment.PositionY,
                    Width = segment.Width,
                    Height = segment.Height,
                    TextAlignment = segment.TextAlignment,
                    HorizontalAlignment = segment.HorizontalAlignment ?? segment.TextAlignment.ToString(),
                    VerticalAlignment = segment.VerticalAlignment,
                    FontName = segment.FontName,
                    FontSize = segment.FontSize,
                    TextOffsetX = field.TextOffsetX,
                    TextOffsetY = field.TextOffsetY
                }))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string?>> BuildValuesByDataFieldIdAsync(
        Domain.Registration.Order order,
        Domain.Registration.Sample sample,
        IReadOnlyDictionary<string, string?> dynamicValuesByKey,
        IReadOnlyList<ReferralOverlaySegment> segments,
        CancellationToken cancellationToken)
    {
        var dataFieldIds = segments
            .Where(segment => segment.DataFieldId.HasValue)
            .Select(segment => segment.DataFieldId!.Value)
            .Distinct()
            .ToList();

        if (dataFieldIds.Count == 0)
        {
            return [];
        }

        var context = new RegistrationRenderContext
        {
            Order = order,
            Customer = order.Customer,
            Branch = order.Branch,
            Sample = sample,
            DynamicValuesByKey = dynamicValuesByKey
        };

        var mappedFields = await _context.DataFields
            .AsNoTracking()
            .Where(field => dataFieldIds.Contains(field.Id))
            .Select(field => new { field.Id, field.Key })
            .ToListAsync(cancellationToken);

        return mappedFields.ToDictionary(
            field => field.Id,
            field => _fieldValueResolver.Resolve(field.Key, context));
    }
}
