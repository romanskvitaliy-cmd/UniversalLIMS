using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public sealed class CreateOrderRequest
{
    public Guid? CustomerId { get; init; }

    public CreateCustomerRequest? NewCustomer { get; init; }

    public Guid InvestigationTypeId { get; init; }

    /// <summary>Застарілий одиночний вибір; якщо <see cref="Documents"/> порожній — використовується це поле.</summary>
    public Guid? TemplateVersionId { get; init; }

    public IReadOnlyList<CreateOrderDocumentRequest> Documents { get; init; } = [];
}

public sealed class CreateOrderResult
{
    public Guid OrderId { get; init; }

    public Guid SampleId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public required string ReferralNumber { get; init; }

    public required string SampleNumber { get; init; }

    public IReadOnlyList<CreatedOrderDocumentDto> Documents { get; init; } = [];
}

public sealed class InvestigationTypeOptionDto
{
    public Guid Id { get; init; }

    public Guid? TemplateVersionId { get; init; }

    public required string Code { get; init; }

    public required string NameUk { get; init; }
}

public sealed class OrderTemplateOptionDto
{
    public Guid TemplateVersionId { get; init; }

    public Guid InvestigationTypeId { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    public bool IsDefault { get; init; }
}

public sealed class OrderCreateFormDto
{
    public required IReadOnlyList<InvestigationTypeOptionDto> InvestigationTypes { get; init; }

    public required IReadOnlyList<OrderTemplateOptionDto> TemplateOptions { get; init; }

    public required IReadOnlyList<BranchOptionDto> Branches { get; init; }

    public Guid? DefaultBranchId { get; init; }
}
