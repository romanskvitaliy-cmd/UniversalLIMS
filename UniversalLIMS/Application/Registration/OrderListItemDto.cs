using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Registration;

/// <summary>Рядок реєстру замовлень для UI та API.</summary>
public sealed class OrderListItemDto
{
    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    /// <summary>SSOT: <see cref="Customer.FullName"/> через <c>Order.CustomerId</c>.</summary>
    public required string CustomerFullName { get; init; }

    public DateTime OrderDate { get; init; }

    public OrderStatus Status { get; init; }

    public int SampleCount { get; init; }

    public required string TargetBranchName { get; init; }

    /// <summary>Останній активний документ замовлення для посилання в PDF Workspace.</summary>
    public Guid? PrimaryTemplateVersionId { get; init; }
}
