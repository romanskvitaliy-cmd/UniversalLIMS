using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

public sealed class SendDocumentToExpertResult
{
    public Guid OrderDocumentId { get; init; }

    public Guid SampleId { get; init; }

    public OrderDocumentStatus DocumentStatus { get; init; }

    public SampleStatus SampleStatus { get; init; }

    public bool SampleReadyForExpert { get; init; }

    public required string Message { get; init; }
}
