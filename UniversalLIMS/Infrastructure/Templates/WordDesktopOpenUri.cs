namespace UniversalLIMS.Infrastructure.Templates;

public static class WordDesktopOpenUri
{
    public static string CreateOpenForEdit(string absoluteDocumentUrl)
    {
        return "ms-word:ofe|u|" + Uri.EscapeDataString(absoluteDocumentUrl);
    }
}
