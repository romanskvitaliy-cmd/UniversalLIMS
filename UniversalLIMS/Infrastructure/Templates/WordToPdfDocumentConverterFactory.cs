using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniversalLIMS.Application.Templates.Abstractions;

namespace UniversalLIMS.Infrastructure.Templates;

public static class WordToPdfDocumentConverterFactory
{
    public static IWordToPdfDocumentConverter Create(
        IConfiguration configuration,
        ILogger logger)
    {
        var provider = configuration["WordToPdf:Provider"]?.Trim();
        var syncfusionLicenseKey = configuration["Syncfusion:LicenseKey"]?.Trim();
        var hasSyncfusionLicense = !string.IsNullOrWhiteSpace(syncfusionLicenseKey);
        var libreOfficePath = LibreOfficeWordToPdfDocumentConverter.ResolveExecutablePath(
            configuration["WordToPdf:LibreOfficeExecutablePath"]);

        if (string.Equals(provider, "LibreOffice", StringComparison.OrdinalIgnoreCase))
        {
            return CreateLibreOfficeConverter(libreOfficePath);
        }

        if (string.Equals(provider, "Syncfusion", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSyncfusionConverter(hasSyncfusionLicense, logger);
        }

        if (hasSyncfusionLicense)
        {
            logger.LogInformation("Word to PDF conversion uses Syncfusion (registered license).");
            return new SyncfusionWordToPdfDocumentConverter();
        }

        if (!string.IsNullOrWhiteSpace(libreOfficePath))
        {
            logger.LogInformation(
                "Word to PDF conversion uses LibreOffice at {LibreOfficePath} because no Syncfusion license key is configured.",
                libreOfficePath);
            return new LibreOfficeWordToPdfDocumentConverter(libreOfficePath);
        }

        logger.LogWarning(
            "Word to PDF conversion uses Syncfusion trial mode. Configure Syncfusion:LicenseKey or install LibreOffice and set WordToPdf:Provider to LibreOffice to avoid evaluation watermarks.");
        return new SyncfusionWordToPdfDocumentConverter();
    }

    private static IWordToPdfDocumentConverter CreateSyncfusionConverter(
        bool hasSyncfusionLicense,
        ILogger logger)
    {
        if (!hasSyncfusionLicense)
        {
            logger.LogWarning(
                "WordToPdf:Provider is Syncfusion, but Syncfusion:LicenseKey is missing. PDFs may contain evaluation watermarks.");
        }

        return new SyncfusionWordToPdfDocumentConverter();
    }

    private static IWordToPdfDocumentConverter CreateLibreOfficeConverter(string? libreOfficePath)
    {
        if (string.IsNullOrWhiteSpace(libreOfficePath))
        {
            throw new InvalidOperationException(
                "WordToPdf:Provider is LibreOffice, but LibreOffice executable was not found. Install LibreOffice or set WordToPdf:LibreOfficeExecutablePath.");
        }

        return new LibreOfficeWordToPdfDocumentConverter(libreOfficePath);
    }
}
