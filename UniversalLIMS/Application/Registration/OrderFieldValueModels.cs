namespace UniversalLIMS.Application.Registration;

public sealed class OrderFieldValueInput
{
    public Guid DataFieldId { get; init; }

    public Guid? SampleId { get; init; }

    public string? StoredValue { get; init; }
}
