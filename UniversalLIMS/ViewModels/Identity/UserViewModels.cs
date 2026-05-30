using System.ComponentModel.DataAnnotations;
using UniversalLIMS.Application.Identity;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.ViewModels.Identity;

public sealed class UserIndexViewModel
{
    public required IReadOnlyList<UserListItemDto> Users { get; init; }

    public required IReadOnlyList<BranchOptionDto> Branches { get; init; }

    public UserListFiltersViewModel Filters { get; init; } = new();
}

public sealed class UserListFiltersViewModel
{
    [Display(Name = "Пошук")]
    public string? Search { get; set; }

    [Display(Name = "Філія")]
    public Guid? BranchId { get; set; }

    [Display(Name = "Тип філії")]
    public BranchKind? BranchKind { get; set; }

    [Display(Name = "Роль")]
    public string? Role { get; set; }

    [Display(Name = "Показати неактивних")]
    public bool IncludeInactive { get; set; }
}

public sealed class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Email є обов'язковим.")]
    [EmailAddress(ErrorMessage = "Невірний формат email.")]
    [MaxLength(256)]
    [Display(Name = "Email (логін)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "ПІБ є обов'язковим.")]
    [MaxLength(200)]
    [Display(Name = "ПІБ")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Посада")]
    public string? Position { get; set; }

    [Display(Name = "Філія")]
    public Guid? BranchId { get; set; }

    [Display(Name = "Активний")]
    public bool IsActive { get; set; } = true;

    [MinLength(6, ErrorMessage = "Пароль має містити щонайменше 6 символів.")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string? Password { get; set; }

    [MinLength(6, ErrorMessage = "Пароль має містити щонайменше 6 символів.")]
    [DataType(DataType.Password)]
    [Display(Name = "Новий пароль")]
    public string? NewPassword { get; set; }

    public List<RoleSelectionViewModel> RoleSelections { get; set; } = [];

    public IReadOnlyList<BranchOptionDto> Branches { get; set; } = [];

    public bool IsEdit => !string.IsNullOrWhiteSpace(Id);

    public bool IsBranchPortalAccount { get; set; }

    [Display(Name = "Поточний пароль")]
    public string? CurrentPassword { get; set; }

    public bool CanRevealCurrentPassword { get; set; }

    public string? CurrentPasswordStatusMessage { get; set; }
}

public sealed class RoleSelectionViewModel
{
    public required string RoleCode { get; init; }

    public required string DisplayName { get; init; }

    public required string AccentColor { get; init; }

    public required string IconClass { get; init; }

    public required string Summary { get; init; }

    public bool IsSelected { get; set; }
}
