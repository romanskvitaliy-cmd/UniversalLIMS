using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class OrderPostCreateNavigationTests
{
    [Fact]
    public void TryGetSingleDocumentPdfFillRoute_ReturnsOrderDocumentId_WhenSingleDocument()
    {
        var orderId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var route = OrderPostCreateNavigation.TryGetSingleDocumentPdfFillRoute(
            new CreateOrderResult
            {
                OrderId = orderId,
                SampleNumber = "SMP-1",
                ReferralNumber = "REF-1",
                Documents =
                [
                    new CreatedOrderDocumentDto
                    {
                        OrderDocumentId = documentId,
                        SampleId = Guid.NewGuid(),
                        TemplateVersionId = versionId,
                        TargetBranchId = Guid.NewGuid()
                    }
                ]
            },
            openPdfAfterCreate: true);

        Assert.NotNull(route);
        Assert.Equal(orderId, route!.OrderId);
        Assert.Equal(versionId, route.TemplateVersionId);
        Assert.Equal(documentId, route.OrderDocumentId);
    }

    [Fact]
    public void TryGetSingleDocumentPdfFillRoute_ReturnsNull_WhenMultipleDocuments()
    {
        var route = OrderPostCreateNavigation.TryGetSingleDocumentPdfFillRoute(
            new CreateOrderResult
            {
                OrderId = Guid.NewGuid(),
                SampleNumber = "SMP-1",
                ReferralNumber = "REF-1",
                Documents =
                [
                    new CreatedOrderDocumentDto
                    {
                        OrderDocumentId = Guid.NewGuid(),
                        SampleId = Guid.NewGuid(),
                        TemplateVersionId = Guid.NewGuid(),
                        TargetBranchId = Guid.NewGuid()
                    },
                    new CreatedOrderDocumentDto
                    {
                        OrderDocumentId = Guid.NewGuid(),
                        SampleId = Guid.NewGuid(),
                        TemplateVersionId = Guid.NewGuid(),
                        TargetBranchId = Guid.NewGuid()
                    }
                ]
            },
            openPdfAfterCreate: true);

        Assert.Null(route);
    }

    [Fact]
    public void TryGetSingleDocumentPdfFillRoute_ReturnsNull_WhenOpenPdfDisabled()
    {
        var route = OrderPostCreateNavigation.TryGetSingleDocumentPdfFillRoute(
            new CreateOrderResult
            {
                OrderId = Guid.NewGuid(),
                SampleNumber = "SMP-1",
                ReferralNumber = "REF-1",
                Documents =
                [
                    new CreatedOrderDocumentDto
                    {
                        OrderDocumentId = Guid.NewGuid(),
                        SampleId = Guid.NewGuid(),
                        TemplateVersionId = Guid.NewGuid(),
                        TargetBranchId = Guid.NewGuid()
                    }
                ]
            },
            openPdfAfterCreate: false);

        Assert.Null(route);
    }
}
