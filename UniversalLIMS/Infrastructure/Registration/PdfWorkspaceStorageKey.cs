namespace UniversalLIMS.Infrastructure.Registration;

/// <summary>
/// Ключ збереження значення PDF Workspace: спочатку канонічний DataField, інакше унікальний тег поля.
/// </summary>
internal static class PdfWorkspaceStorageKey
{
    public static string ForTemplateField(string tag, string? dataFieldKey) =>
        !string.IsNullOrWhiteSpace(dataFieldKey)
            ? dataFieldKey.Trim()
            : tag.Trim();

    public static string ForUnmatchedClient(string? dataFieldKey, string? tag, string clientKey)
    {
        if (!string.IsNullOrWhiteSpace(dataFieldKey))
        {
            return dataFieldKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            return tag.Trim();
        }

        return clientKey.Trim();
    }
}
