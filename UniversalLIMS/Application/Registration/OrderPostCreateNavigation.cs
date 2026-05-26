namespace UniversalLIMS.Application.Registration;

public static class OrderPostCreateNavigation
{
    public sealed record PdfFillRouteValues(
        Guid TemplateVersionId,
        Guid OrderId,
        Guid OrderDocumentId);

    public static PdfFillRouteValues? TryGetSingleDocumentPdfFillRoute(
        CreateOrderResult result,
        bool openPdfAfterCreate)
    {
        if (!openPdfAfterCreate || result.Documents.Count != 1)
        {
            return null;
        }

        var document = result.Documents[0];
        return new PdfFillRouteValues(
            document.TemplateVersionId,
            result.OrderId,
            document.OrderDocumentId);
    }
}
