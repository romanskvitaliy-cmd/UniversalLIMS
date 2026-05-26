using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class OrderDetailViewModel
{
    public required OrderDetailDto Detail { get; init; }

    public required IReadOnlyList<BranchOptionDto> Branches { get; init; }
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
