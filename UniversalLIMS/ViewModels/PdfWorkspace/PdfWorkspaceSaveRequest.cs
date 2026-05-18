using System.Text.Json.Serialization;

namespace UniversalLIMS.ViewModels.PdfWorkspace;

public sealed class PdfWorkspaceSaveRequest
{
    [JsonPropertyName("orderId")]
    public Guid? OrderId { get; set; }

    [JsonPropertyName("values")]
    public List<PdfWorkspaceSaveFieldRequest>? Values { get; set; }
}

public sealed class PdfWorkspaceSaveFieldRequest
{
    /// <summary>GUID рядком з фронтенду (string, не object).</summary>
    [JsonPropertyName("templateFieldId")]
    public string? TemplateFieldId { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
