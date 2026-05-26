using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory;

/// <summary>Посилання на заповнення PDF для проби (через OrderDocument або fallback-шаблон).</summary>
public sealed class SamplePdfFillTargetDto
{
    public Guid SampleId { get; init; }

    public Guid OrderId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public Guid? OrderDocumentId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public OrderDocumentStatus? DocumentStatus { get; init; }
}
