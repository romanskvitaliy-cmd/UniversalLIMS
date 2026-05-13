using UniversalLIMS.Domain.Common;

namespace UniversalLIMS.Domain.Templates;

public class TemplateVersion : BaseEntity, ISoftAnnulled
{
    public Guid TemplateId { get; set; }

    public Template Template { get; set; } = null!;

    public Guid? BasedOnTemplateVersionId { get; set; }

    public TemplateVersion? BasedOnTemplateVersion { get; set; }

    public int VersionNumber { get; set; }

    public TemplateVersionStatus Status { get; set; } = TemplateVersionStatus.Draft;

    public TemplateDocumentFormat DocumentFormat { get; set; } = TemplateDocumentFormat.Pdf;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Sha256Hash { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public string? UploadedByUserId { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public string? PublishedByUserId { get; set; }

    public string? PublicationNotesUk { get; set; }

    public ICollection<TemplateField> Fields { get; set; } = [];

    public bool IsAnnulled { get; set; }

    public DateTime? AnnulledAtUtc { get; set; }

    public string? AnnulledByUserId { get; set; }

    public string? AnnulmentReason { get; set; }
}
