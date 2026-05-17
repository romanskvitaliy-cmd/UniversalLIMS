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
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("dataFieldKey")]
    public string? DataFieldKey { get; set; }

    [JsonPropertyName("sequence")]
    public int? Sequence { get; set; }
}
