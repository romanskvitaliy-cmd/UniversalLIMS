using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Registration;

/// <summary>Група полів різних шаблонів, які реєстратор вважає однаковими для цього замовлення.</summary>
public sealed class OrderFieldLinkGroup : BaseEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public string? Label { get; set; }

    public int SortOrder { get; set; }

    public ICollection<OrderFieldLinkMember> Members { get; set; } = [];
}
