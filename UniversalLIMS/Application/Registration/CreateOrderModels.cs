using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Registration;

public sealed class CreateOrderRequest
{
    public Guid? CustomerId { get; init; }

    public CreateCustomerRequest? NewCustomer { get; init; }

    /// <summary>Застарілий одиночний вибір; використовується, якщо <see cref="Samples"/> порожній.</summary>
    public Guid InvestigationTypeId { get; init; }

    /// <summary>Застарілий одиночний вибір; якщо <see cref="Documents"/> порожній — використовується це поле.</summary>
    public Guid? TemplateVersionId { get; init; }

    /// <summary>Застарілий список документів для одного дослідження.</summary>
    public IReadOnlyList<CreateOrderDocumentRequest> Documents { get; init; } = [];

    /// <summary>Кілька проб/досліджень в одному замовленні.</summary>
    public IReadOnlyList<CreateOrderSampleRequest> Samples { get; init; } = [];
}

public sealed class CreateOrderSampleRequest
{
    public Guid InvestigationTypeId { get; init; }

    public Guid? TemplateVersionId { get; init; }

    /// <summary>Бланк направлення REF-* (Per Sample, D6a).</summary>
    public Guid? ReferralTemplateVersionId { get; init; }

    public IReadOnlyList<CreateOrderDocumentRequest> Documents { get; init; } = [];
}

public sealed class CreateOrderResult
{
    public Guid OrderId { get; init; }

    public Guid SampleId { get; init; }

    public Guid TemplateVersionId { get; init; }

    public required string ReferralNumber { get; init; }

    public required string SampleNumber { get; init; }

    public IReadOnlyList<CreatedOrderSampleDto> Samples { get; init; } = [];

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

    public TemplatePurpose Purpose { get; init; } = TemplatePurpose.Protocol;

    /// <summary>URL для inline-перегляду оригінального PDF (з токеном).</summary>
    public string? PreviewUrl { get; init; }
}

public sealed class OrderReferralTemplateOptionDto
{
    public Guid TemplateVersionId { get; init; }

    public required string TemplateCode { get; init; }

    public required string TemplateNameUk { get; init; }

    public int VersionNumber { get; init; }

    /// <summary>URL для inline-перегляду оригінального PDF (з токеном).</summary>
    public string? PreviewUrl { get; init; }
}

public sealed class OrderCreateFormDto
{
    public required IReadOnlyList<InvestigationTypeOptionDto> InvestigationTypes { get; init; }

    public required IReadOnlyList<OrderTemplateOptionDto> TemplateOptions { get; init; }

    public required IReadOnlyList<OrderReferralTemplateOptionDto> ReferralTemplateOptions { get; init; }

    public required IReadOnlyList<BranchOptionDto> Branches { get; init; }

    public Guid? DefaultBranchId { get; init; }
}

public sealed class AppendOrderSamplesRequest
{
    public Guid OrderId { get; init; }

    public IReadOnlyList<CreateOrderSampleRequest> Samples { get; init; } = [];
}

public sealed class AppendOrderSamplesResult
{
    public Guid OrderId { get; init; }

    public IReadOnlyList<CreatedOrderSampleDto> Samples { get; init; } = [];

    public IReadOnlyList<CreatedOrderDocumentDto> Documents { get; init; } = [];
}
