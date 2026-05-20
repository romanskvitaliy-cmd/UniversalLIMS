using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class PdfOverlayTextLayoutTests
{
    [Fact]
    public void GetPreviewBounds_AppliesTextOffsetsAndBaseline()
    {
        var bounds = PdfOverlayTextLayout.GetPreviewBounds(100m, 50m, 220m, 28m, 2.5m, -1m);

        Assert.Equal(102.5m, bounds.X);
        Assert.Equal(52m, bounds.Y); // 50 + (-1) + 3 baseline
        Assert.Equal(220m, bounds.Width);
        Assert.Equal(28m, bounds.Height);
    }

    [Fact]
    public void ToPdfPoints_ScalesByConstructorPreviewScale()
    {
        var bounds = PdfOverlayTextLayout.GetPreviewBounds(135m, 27m, 220m, 28m);
        var pdf = PdfOverlayTextLayout.ToPdfPoints(bounds);

        Assert.Equal(100f, pdf.X, precision: 2);
        Assert.Equal(22.22f, pdf.Y, precision: 1);
    }
}
