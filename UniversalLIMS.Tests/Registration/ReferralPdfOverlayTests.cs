using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class RegistrationFieldValueResolverTests
{
    [Fact]
    public void Resolve_CustomerFullName_ReturnsCustomerColumn()
    {
        var resolver = new RegistrationFieldValueResolver();
        var context = CreateContext();

        var value = resolver.Resolve("Customer.FullName", context);

        Assert.Equal("Петренко Петро", value);
    }

    [Fact]
    public void Resolve_DynamicKey_ReturnsOrderFieldValue()
    {
        var resolver = new RegistrationFieldValueResolver();
        var context = CreateContext();

        var value = resolver.Resolve("Registration.SamplingLocation", context);

        Assert.Equal("Склад №3", value);
    }

    private static RegistrationRenderContext CreateContext() =>
        new()
        {
            Order = new Order { ReferralNumber = "REF-001" },
            Customer = new Customer { FullName = "Петренко Петро" },
            Branch = new Branch { Code = "ZHY", Name = "Житомир" },
            Sample = new Sample
            {
                Number = "ZHY-2026-00001",
                RegisteredAt = new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc)
            },
            DynamicValuesByKey = new Dictionary<string, string?>
            {
                ["Registration.SamplingLocation"] = "Склад №3"
            }
        };
}

public sealed class ReferralPdfOverlayRendererTests
{
    [Fact]
    public void Render_DrawsMappedValueOntoOriginalPdf()
    {
        var dataFieldId = Guid.NewGuid();
        var originalPdf = CreateBlankPdf();
        var renderer = new ReferralPdfOverlayRenderer();

        var rendered = renderer.Render(
            new MemoryStream(originalPdf),
            [
                new ReferralOverlaySegment
                {
                    DataFieldId = dataFieldId,
                    PageNumber = 1,
                    PositionX = 135,
                    PositionY = 135,
                    Width = 270,
                    Height = 54,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left
                }
            ],
            new Dictionary<Guid, string?> { [dataFieldId] = "TEST-VALUE" });

        Assert.NotEmpty(rendered);
        Assert.NotEqual(originalPdf.Length, rendered.Length);
    }

    private static byte[] CreateBlankPdf()
    {
        using var document = new PdfDocument();
        var page = document.Pages.Add();
        page.Graphics.DrawString(
            "TEMPLATE",
            new PdfStandardFont(PdfFontFamily.Helvetica, 12),
            PdfBrushes.LightGray,
            new Syncfusion.Drawing.PointF(20, 20));

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
