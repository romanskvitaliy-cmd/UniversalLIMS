using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

public sealed class OrderFieldValueInput
{
    public Guid DataFieldId { get; init; }

    public Guid? SampleId { get; init; }

    public string? StoredValue { get; init; }
}

public sealed class RegisterSampleRequest
{
    public Guid CustomerId { get; init; }

    public Guid InvestigationTypeId { get; init; }

    public Guid RegistrationBranchId { get; init; }

    public Guid TargetBranchId { get; init; }

    public DateTime? RegisteredAtUtc { get; init; }

    public string? OrderNotes { get; init; }

    public string? SampleNotes { get; init; }

    public IReadOnlyList<OrderFieldValueInput> DynamicFieldValues { get; init; } = [];
}

public sealed class RegisterSampleResult
{
    public Guid OrderId { get; init; }

    public Guid SampleId { get; init; }

    public string ReferralNumber { get; init; } = string.Empty;

    public string SampleNumber { get; init; } = string.Empty;

    public int DocumentsCreated { get; init; }
}

public sealed class OrderDetailsResult
{
    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public OrderStatus Status { get; init; }

    public DateTime? RegisteredAtUtc { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string? CustomerOrganizationName { get; init; }

    public string? CustomerContactPhone { get; init; }

    public string RegistrationBranchName { get; init; } = string.Empty;

    public IReadOnlyList<SampleDetailsResult> Samples { get; init; } = [];

    public IReadOnlyList<OrderDocumentDetailsResult> Documents { get; init; } = [];
}

public sealed class SampleDetailsResult
{
    public Guid SampleId { get; init; }

    public string Number { get; init; } = string.Empty;

    public DateTime RegisteredAt { get; init; }

    public string InvestigationTypeName { get; init; } = string.Empty;

    public SampleStatus Status { get; init; }
}

public sealed class OrderDocumentDetailsResult
{
    public Guid OrderDocumentId { get; init; }

    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public int TemplateVersionNumber { get; init; }

    public string TargetBranchName { get; init; } = string.Empty;

    public OrderDocumentStatus Status { get; init; }
}
