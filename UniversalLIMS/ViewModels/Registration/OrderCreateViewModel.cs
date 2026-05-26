using System.ComponentModel.DataAnnotations;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.ViewModels.Registration;

public sealed class OrderCreateViewModel
{
    public required OrderCreateFormDto Form { get; init; }

    public OrderCreateInputModel Input { get; init; } = new();
}

public sealed class OrderCreateInputModel
{
    public string CustomerMode { get; set; } = "existing";

    public Guid? SelectedCustomerId { get; set; }

    public string? CustomerSearchQuery { get; set; }

    public string? SelectedCustomerLabel { get; set; }

    public CustomerKind NewCustomerKind { get; set; } = CustomerKind.Individual;

    [Display(Name = "ПІБ / назва")]
    public string? NewCustomerFullName { get; set; }

    [Display(Name = "Організація")]
    public string? NewCustomerOrganizationName { get; set; }

    [Display(Name = "Телефон")]
    public string? NewCustomerContactPhone { get; set; }

    [Display(Name = "Адреса")]
    public string? NewCustomerAddress { get; set; }

    [Display(Name = "ЄДРПОУ")]
    public string? NewCustomerEdrpou { get; set; }

    [Required(ErrorMessage = "Оберіть тип дослідження.")]
    public Guid InvestigationTypeId { get; set; }

    public Guid? TemplateVersionId { get; set; }

    /// <summary>Обрані PDF-шаблони (версії) для справи.</summary>
    public List<Guid> SelectedTemplateVersionIds { get; set; } = [];

    /// <summary>Паралельний список філій призначення (індекс = SelectedTemplateVersionIds).</summary>
    public List<Guid> DocumentTargetBranchIds { get; set; } = [];

    public bool OpenPdfAfterCreate { get; set; } = true;
}
