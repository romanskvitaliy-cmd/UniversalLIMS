namespace UniversalLIMS.Application.Registration;

public sealed class FieldTextLibraryUsageRequest
{
    public Guid TemplateFieldId { get; init; }

    public Guid? OrderId { get; init; }
}
