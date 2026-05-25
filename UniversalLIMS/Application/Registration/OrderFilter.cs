using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

/// <summary>Фільтри реєстру замовлень (read-only список).</summary>
public sealed class OrderFilter
{
    public string? ReferralNumber { get; init; }

    public string? CustomerFullName { get; init; }

    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public OrderStatus? Status { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
