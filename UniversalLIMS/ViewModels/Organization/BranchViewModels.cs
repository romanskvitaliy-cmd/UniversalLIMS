using System.ComponentModel.DataAnnotations;
using UniversalLIMS.Application.Organization;

namespace UniversalLIMS.ViewModels.Organization;

public sealed class BranchIndexViewModel
{
    public required IReadOnlyList<BranchListItemDto> Branches { get; init; }
}

public sealed class BranchCreateViewModel
{
    [Required(ErrorMessage = "Код філії є обов'язковим.")]
    [MaxLength(16)]
    [Display(Name = "Код")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Назву філії є обов'язковою.")]
    [MaxLength(200)]
    [Display(Name = "Назва")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Місто є обов'язковим.")]
    [MaxLength(100)]
    [Display(Name = "Місто")]
    public string City { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Адреса")]
    public string? Address { get; set; }
}

public sealed class BranchEditViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Код")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Назву філії є обов'язковою.")]
    [MaxLength(200)]
    [Display(Name = "Назва")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Місто є обов'язковим.")]
    [MaxLength(100)]
    [Display(Name = "Місто")]
    public string City { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Адреса")]
    public string? Address { get; set; }

    [Display(Name = "Активна (доступна в нових замовленнях)")]
    public bool IsActive { get; set; } = true;
}

public sealed class AnnulBranchViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Причина анулювання є обов'язковою.")]
    [MaxLength(1000)]
    [Display(Name = "Причина анулювання")]
    public string AnnulmentReason { get; set; } = string.Empty;
}
