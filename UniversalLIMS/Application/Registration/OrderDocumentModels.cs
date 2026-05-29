using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public sealed class CreateOrderDocumentRequest
{
    public Guid TemplateVersionId { get; init; }

    public Guid TargetBranchId { get; init; }
}

public sealed class CreatedOrderDocumentDto
{
    public Guid OrderDocumentId { get; init; }

    public Guid SampleId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public Guid TargetBranchId { get; init; }
}

public sealed class CreatedOrderSampleDto
{
    public Guid SampleId { get; init; }

    public Guid InvestigationTypeId { get; init; }

    public required string SampleNumber { get; init; }

    public IReadOnlyList<CreatedOrderDocumentDto> Documents { get; init; } = [];
}

public sealed class BranchOptionDto
{
    public Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}

public sealed class OrderDetailDto
{
    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public Guid CustomerId { get; init; }

    public CustomerKind CustomerKind { get; init; }

    public required string CustomerFullName { get; init; }

    public string? CustomerOrganizationName { get; init; }

    public string? CustomerContactPhone { get; init; }

    public string? CustomerEmail { get; init; }

    public string? CustomerAddress { get; init; }

    public string? CustomerEdrpou { get; init; }

    public string? CustomerRnokpp { get; init; }

    public string? CustomerNotes { get; init; }

    public OrderStatus Status { get; init; }

    public DateTime OrderDate { get; init; }

    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string InvestigationTypeNameUk { get; init; }

    public required string WorkflowSummaryUk { get; init; }

    public required IReadOnlyList<OrderDetailSampleDto> Samples { get; init; }

    public required IReadOnlyList<OrderDocumentItemDto> Documents { get; init; }
}

public sealed class OrderDetailSampleDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string InvestigationTypeNameUk { get; init; }

    public DateTime RegisteredAt { get; init; }
}

public sealed class OrderDocumentItemDto
{
    public Guid OrderDocumentId { get; init; }

    public Guid SampleId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public Guid TargetBranchId { get; init; }

    public required string TargetBranchName { get; init; }

    public OrderDocumentStatus Status { get; init; }

    public bool CanFill { get; init; }

    public bool CanSendToLab { get; init; }
}

public sealed class SendOrderDocumentsRequest
{
    public Guid OrderId { get; init; }

    public required IReadOnlyList<Guid> OrderDocumentIds { get; init; }
}

public sealed class UpdateOrderDocumentRoutingRequest
{
    public Guid OrderId { get; init; }

    public Guid OrderDocumentId { get; init; }

    public Guid TargetBranchId { get; init; }
}
