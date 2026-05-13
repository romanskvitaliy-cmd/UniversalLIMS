using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class RegisterSampleViewModel
{
    [Required(ErrorMessage = "Оберіть замовника.")]
    [Display(Name = "Замовник")]
    public Guid CustomerId { get; set; }

    public string? SelectedCustomerDisplay { get; set; }

    [Required(ErrorMessage = "Оберіть тип дослідження.")]
    [Display(Name = "Тип дослідження")]
    public Guid InvestigationTypeId { get; set; }

    [Required(ErrorMessage = "Оберіть філію реєстрації.")]
    [Display(Name = "Філія реєстрації")]
    public Guid RegistrationBranchId { get; set; }

    [Required(ErrorMessage = "Оберіть цільову філію.")]
    [Display(Name = "Цільова філія (лабораторія)")]
    public Guid TargetBranchId { get; set; }

    [Display(Name = "Дата реєстрації (UTC)")]
    public DateTime? RegisteredAtUtc { get; set; }

    [StringLength(2000)]
    [Display(Name = "Примітки до замовлення")]
    public string? OrderNotes { get; set; }

    [StringLength(2000)]
    [Display(Name = "Примітки до проби")]
    public string? SampleNotes { get; set; }

    public IReadOnlyList<SelectListItem> InvestigationTypes { get; set; } = [];

    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];

    public IReadOnlyList<SelectListItem> Customers { get; set; } = [];

    public IReadOnlyList<DynamicFieldInputViewModel> DynamicFields { get; set; } = [];
}

public sealed class DynamicFieldInputViewModel
{
    public Guid DataFieldId { get; set; }

    public string DisplayNameUk { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int? MaxLength { get; set; }

    [Display(Name = "Значення")]
    public string? StoredValue { get; set; }
}

public sealed class OrderDetailsViewModel
{
    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public OrderStatus Status { get; init; }

    public DateTime? RegisteredAtUtc { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string? CustomerOrganizationName { get; init; }

    public string? CustomerContactPhone { get; init; }

    public string RegistrationBranchName { get; init; } = string.Empty;

    public IReadOnlyList<OrderSampleItemViewModel> Samples { get; init; } = [];

    public IReadOnlyList<OrderDocumentItemViewModel> Documents { get; init; } = [];
}

public sealed class OrderSampleItemViewModel
{
    public Guid SampleId { get; init; }

    public string Number { get; init; } = string.Empty;

    public DateTime RegisteredAt { get; init; }

    public string InvestigationTypeName { get; init; } = string.Empty;

    public SampleStatus Status { get; init; }
}

public sealed class OrderDocumentItemViewModel
{
    public Guid OrderDocumentId { get; init; }

    public string TemplateCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public int TemplateVersionNumber { get; init; }

    public string TargetBranchName { get; init; } = string.Empty;

    public OrderDocumentStatus Status { get; init; }
}

public sealed class OrderListItemViewModel
{
    public Guid OrderId { get; init; }

    public string? ReferralNumber { get; init; }

    public OrderStatus Status { get; init; }

    public DateTime? RegisteredAtUtc { get; init; }

    public string CustomerFullName { get; init; } = string.Empty;

    public string RegistrationBranchName { get; init; } = string.Empty;
}
