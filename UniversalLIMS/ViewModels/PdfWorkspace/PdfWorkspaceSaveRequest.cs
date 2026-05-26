using System.Text.Json.Serialization;

namespace UniversalLIMS.ViewModels.PdfWorkspace;

public sealed class PdfWorkspaceSaveRequest
{
    [JsonPropertyName("orderId")]
    public Guid? OrderId { get; set; }

    [JsonPropertyName("values")]
    public List<PdfWorkspaceSaveFieldRequest>? Values { get; set; }

    /// <summary>Додати тексти до бібліотеки після успішного збереження значень (ідемпотентно).</summary>
    [JsonPropertyName("libraryAdditions")]
    public List<PdfWorkspaceLibraryAdditionRequest>? LibraryAdditions { get; set; }
}

public sealed class PdfWorkspaceLibraryAdditionRequest
{
    [JsonPropertyName("templateFieldId")]
    public string? TemplateFieldId { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("shortLabel")]
    public string? ShortLabel { get; set; }

    [JsonPropertyName("scopeToTemplateVersion")]
    public bool ScopeToTemplateVersion { get; set; }
}

public sealed class PdfWorkspaceSaveFieldRequest
{
    /// <summary>GUID рядком з фронтенду (string, не object).</summary>
    [JsonPropertyName("templateFieldId")]
    public string? TemplateFieldId { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
