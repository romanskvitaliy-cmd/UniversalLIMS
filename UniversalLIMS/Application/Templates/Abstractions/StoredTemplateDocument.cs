namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record StoredTemplateDocument(
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string Sha256Hash);
