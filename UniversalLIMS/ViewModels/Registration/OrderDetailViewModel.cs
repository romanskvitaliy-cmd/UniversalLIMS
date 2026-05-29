using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class OrderDetailViewModel
{
    public required OrderDetailDto Detail { get; init; }

    public required IReadOnlyList<BranchOptionDto> Branches { get; init; }

    public required UpdateOrderCustomerInputModel CustomerEdit { get; init; }

    public OrderFieldLinkGroupsDetailDto FieldLinkGroups { get; init; } = new();

    public required OrderCreateFormDto AppendForm { get; init; }

    public AppendOrderSamplesInputModel AppendSamples { get; init; } = new();
}

public sealed class AppendOrderSamplesInputModel
{
    public Guid OrderId { get; set; }

    public List<OrderCreateSampleInputModel> Samples { get; set; } = [];
}

public sealed class UpdateOrderCustomerInputModel
{
    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public CustomerKind Kind { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? OrganizationName { get; set; }

    public string? ContactPhone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Edrpou { get; set; }

    public string? Rnokpp { get; set; }

    public string? Notes { get; set; }
}

public sealed class SendOrderDocumentsInputModel
{
    public Guid OrderId { get; set; }

    public List<Guid> OrderDocumentIds { get; set; } = [];
}

public sealed class UpdateOrderDocumentRoutingInputModel
{
    public Guid OrderId { get; set; }

    public Guid OrderDocumentId { get; set; }

    public Guid TargetBranchId { get; set; }
}
