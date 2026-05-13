using System.ComponentModel.DataAnnotations;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class CustomerListItemViewModel
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? OrganizationName { get; init; }

    public string? ContactPhone { get; init; }

    public string? Edrpou { get; init; }
}

public sealed class CustomerSearchViewModel
{
    [Display(Name = "Пошук")]
    public string? Query { get; set; }

    public IReadOnlyList<CustomerListItemViewModel> Results { get; set; } = [];
}

public sealed class CustomerEditViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Тип замовника")]
    public CustomerKind Kind { get; set; } = CustomerKind.Individual;

    [Required(ErrorMessage = "ПІБ є обов'язковим.")]
    [StringLength(300)]
    [Display(Name = "ПІБ")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Назва організації")]
    public string? OrganizationName { get; set; }

    [StringLength(50)]
    [Display(Name = "Телефон")]
    public string? ContactPhone { get; set; }

    [StringLength(256)]
    [EmailAddress(ErrorMessage = "Некоректний email.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(500)]
    [Display(Name = "Адреса")]
    public string? Address { get; set; }

    [StringLength(10)]
    [Display(Name = "ЄДРПОУ")]
    public string? Edrpou { get; set; }

    [StringLength(12)]
    [Display(Name = "РНОКПП")]
    public string? Rnokpp { get; set; }

    [StringLength(2000)]
    [Display(Name = "Примітки")]
    public string? Notes { get; set; }
}

public sealed class CustomerDetailsViewModel
{
    public Guid Id { get; init; }

    public CustomerKind Kind { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? OrganizationName { get; init; }

    public string? ContactPhone { get; init; }

    public string? Email { get; init; }

    public string? Address { get; init; }

    public string? Edrpou { get; init; }

    public string? Rnokpp { get; init; }

    public string? Notes { get; init; }
}

public sealed class CustomerAnnulViewModel
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Причина анулювання є обов'язковою.")]
    [StringLength(1000)]
    [Display(Name = "Причина анулювання")]
    public string Reason { get; set; } = string.Empty;
}
