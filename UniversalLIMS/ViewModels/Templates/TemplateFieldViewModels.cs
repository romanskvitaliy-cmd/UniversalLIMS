using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.ViewModels.Templates;

public sealed class TemplateFieldMappingViewModel
{
    public Guid TemplateVersionId { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public bool IsEditable { get; set; }

    public List<DataFieldOptionViewModel> DataFields { get; set; } = [];

    public List<TemplateFieldMappingItemViewModel> Fields { get; set; } = [];

    public List<Guid> DeletedFieldIds { get; set; } = [];

    public string? OpenInWordUri { get; set; }

    public TemplateDocumentFormat DocumentFormat { get; set; }

    public string? PdfPreviewUrl { get; set; }

    [ValidateNever]
    public CreatePdfTemplateFieldViewModel NewPdfField { get; set; } = new();
}

public sealed class TemplateFieldMappingItemViewModel
{
    public Guid FieldId { get; set; }

    public string Tag { get; set; } = string.Empty;

    public string? Title { get; set; }

    public WordContentControlType WordControlType { get; set; }

    public Guid? DataFieldId { get; set; }

    public bool IsRequired { get; set; }

    public int? EstimatedCapacityChars { get; set; }

    public int? MaxLines { get; set; }

    public bool AllowMultiline { get; set; }

    public FieldOverflowPolicy OverflowPolicy { get; set; }

    [Range(1, 1000)]
    public int? PageNumber { get; set; }

    [Range(0, 100000)]
    public decimal? PositionX { get; set; }

    [Range(0, 100000)]
    public decimal? PositionY { get; set; }

    [Range(0, 100000)]
    public decimal? Width { get; set; }

    [Range(0, 100000)]
    public decimal? Height { get; set; }

    public decimal TextOffsetX { get; set; }

    public decimal TextOffsetY { get; set; }
}

public sealed class DataFieldOptionViewModel
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string DisplayNameUk { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class TemplateFieldPermissionMatrixViewModel
{
    public Guid TemplateVersionId { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; }

    public bool IsEditable { get; set; }

    public bool IsPublishedVersion => Status == TemplateVersionStatus.Published;

    public List<TemplateFieldPermissionRowViewModel> Fields { get; set; } = [];
}

public sealed class TemplateFieldPermissionRowViewModel
{
    public Guid FieldId { get; set; }

    public string Tag { get; set; } = string.Empty;

    public string? Title { get; set; }

    public List<RolePermissionItemViewModel> RolePermissions { get; set; } = [];
}

public sealed class RolePermissionItemViewModel
{
    public string RoleName { get; set; } = string.Empty;

    public FieldAccessLevel AccessLevel { get; set; }
}

public sealed class CreateDataFieldFromTemplateFieldViewModel
{
    public Guid TemplateFieldId { get; set; }

    public Guid TemplateVersionId { get; set; }

    public string Tag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Назва українською обов'язкова.")]
    [StringLength(200)]
    [Display(Name = "Назва українською")]
    public string DisplayNameUk { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Опис")]
    public string? DescriptionUk { get; set; }

    [Display(Name = "Тип поля")]
    public DataFieldType FieldType { get; set; } = DataFieldType.Text;

    [Display(Name = "Область")]
    public DataFieldScope Scope { get; set; } = DataFieldScope.Result;

    [Range(1, 10000, ErrorMessage = "MaxLength має бути від 1 до 10000.")]
    [Display(Name = "Максимальна довжина")]
    public int? MaxLength { get; set; }
}

public sealed class CreatePdfTemplateFieldViewModel
{
    public Guid TemplateVersionId { get; set; }

    [Required(ErrorMessage = "Tag обов'язковий.")]
    [StringLength(128)]
    [Display(Name = "Tag")]
    public string Tag { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Title")]
    public string? Title { get; set; }

    /// <summary>Сторінка PDF для початкового розміщення тега (разом із PositionX/PositionY).</summary>
    [Range(1, 999)]
    public int? PageNumber { get; set; }

    [Range(0, 100000)]
    public decimal? PositionX { get; set; }

    [Range(0, 100000)]
    public decimal? PositionY { get; set; }
}
