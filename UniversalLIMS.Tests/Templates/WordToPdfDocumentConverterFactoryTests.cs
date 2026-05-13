using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class WordToPdfDocumentConverterFactoryTests
{
    [Fact]
    public void Create_WithoutLicenseAndWithoutLibreOffice_UsesSyncfusion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var converter = WordToPdfDocumentConverterFactory.Create(
            configuration,
            NullLogger.Instance);

        Assert.IsType<SyncfusionWordToPdfDocumentConverter>(converter);
    }

    [Fact]
    public void Create_WithSyncfusionLicense_UsesSyncfusion()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Syncfusion:LicenseKey"] = "test-license-key"
            })
            .Build();

        var converter = WordToPdfDocumentConverterFactory.Create(
            configuration,
            NullLogger.Instance);

        Assert.IsType<SyncfusionWordToPdfDocumentConverter>(converter);
    }

    [Fact]
    public void Create_WithLibreOfficeProviderAndMissingExecutable_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WordToPdf:Provider"] = "LibreOffice",
                ["WordToPdf:LibreOfficeExecutablePath"] = @"C:\missing\soffice.exe"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            WordToPdfDocumentConverterFactory.Create(configuration, NullLogger.Instance));

        Assert.Contains("LibreOffice", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
