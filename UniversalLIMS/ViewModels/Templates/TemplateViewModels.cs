using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.ViewModels.Templates;

public sealed class TemplateListItemViewModel
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public TemplateStatus Status { get; set; }

    public TemplatePurpose Purpose { get; set; }

    public int VersionCount { get; set; }

    public int? CurrentPublishedVersionNumber { get; set; }
}

public sealed class TemplateEditViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Код шаблону обов'язковий.")]
    [StringLength(64, ErrorMessage = "Код шаблону не може перевищувати 64 символи.")]
    [Display(Name = "Код")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Назва українською обов'язкова.")]
    [StringLength(200, ErrorMessage = "Назва не може перевищувати 200 символів.")]
    [Display(Name = "Назва українською")]
    public string NameUk { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Опис не може перевищувати 1000 символів.")]
    [Display(Name = "Опис")]
    public string? DescriptionUk { get; set; }

    [Required(ErrorMessage = "Оберіть призначення шаблону.")]
    [Display(Name = "Призначення")]
    public TemplatePurpose Purpose { get; set; } = TemplatePurpose.Protocol;

    public IReadOnlyList<SelectListItem> PurposeOptions { get; set; } = [];
}

public sealed class TemplateDetailsViewModel
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameUk { get; set; } = string.Empty;

    public string? DescriptionUk { get; set; }

    public TemplateStatus Status { get; set; }

    public TemplatePurpose Purpose { get; set; }

    public IReadOnlyCollection<TemplateVersionListItemViewModel> Versions { get; set; } = [];
}

public sealed class TemplateVersionListItemViewModel
{
    public Guid Id { get; set; }

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public int FieldCount { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public DateTime? FirstPublishedAtUtc { get; set; }

    public DateTime? RepublishedAtUtc { get; set; }

    public bool IsCurrentPublished { get; set; }
}

public sealed class TemplateVersionUploadViewModel
{
    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    [Required(ErrorMessage = "Оберіть файл .pdf, .docx або .doc.")]
    [Display(Name = "Файл шаблону (.pdf / .docx / .doc)")]
    public IFormFile? Document { get; set; }

    [Display(Name = "Копіювати теги з версії")]
    public Guid? CopyFieldsFromVersionId { get; set; }

    public IReadOnlyCollection<TemplateVersionTagSourceOptionViewModel> TagSourceVersions { get; set; } = [];
}

public sealed class TemplateVersionTagSourceOptionViewModel
{
    public Guid Id { get; set; }

    public int VersionNumber { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public int FieldCount { get; set; }
}

public sealed class TemplateVersionDetailsViewModel
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; }

    public TemplateDocumentFormat DocumentFormat { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Sha256Hash { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public DateTime? FirstPublishedAtUtc { get; set; }

    public DateTime? RepublishedAtUtc { get; set; }

    public bool IsCurrentPublished { get; set; }

    public bool CanRepublish { get; set; }

    public string? PublicationNotesUk { get; set; }

    public IReadOnlyCollection<TemplateFieldListItemViewModel> Fields { get; set; } = [];

    public IReadOnlyCollection<string> ValidationErrors { get; set; } = [];

    public string? OpenInWordUri { get; set; }

    public string? OpenOriginalUrl { get; set; }

    public bool CanRescanFields { get; set; }

    public bool CanImportTags { get; set; }

    public IReadOnlyCollection<TemplateVersionTagSourceOptionViewModel> TagSourceVersions { get; set; } = [];
}

public sealed class TemplateFieldListItemViewModel
{
    public Guid Id { get; set; }

    public string Tag { get; set; } = string.Empty;

    public string? Title { get; set; }

    public WordContentControlType WordControlType { get; set; }

    public string? DataFieldKey { get; set; }

    public string? DataFieldDisplayNameUk { get; set; }

    public bool IsRequired { get; set; }

    public int? EstimatedCapacityChars { get; set; }

    public int PermissionCount { get; set; }
}

public sealed class AnnulTemplateViewModel
{
    public Guid Id { get; set; }

    public string NameUk { get; set; } = string.Empty;

    [Required(ErrorMessage = "Причина анулювання обов'язкова.")]
    [StringLength(1000, ErrorMessage = "Причина не може перевищувати 1000 символів.")]
    [Display(Name = "Причина анулювання")]
    public string AnnulmentReason { get; set; } = string.Empty;
}

public sealed class AnnulTemplateVersionViewModel
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateNameUk { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; }

    [Required(ErrorMessage = "Причина анулювання обов'язкова.")]
    [StringLength(1000, ErrorMessage = "Причина не може перевищувати 1000 символів.")]
    [Display(Name = "Причина анулювання")]
    public string AnnulmentReason { get; set; } = string.Empty;
}
