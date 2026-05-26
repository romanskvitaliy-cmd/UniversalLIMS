using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class TemplatePublicationValidator : ITemplatePublicationValidator
{
    private readonly ApplicationDbContext _context;
    private readonly ITemplateDocumentStorage _documentStorage;

    public TemplatePublicationValidator(
        ApplicationDbContext context,
        ITemplateDocumentStorage documentStorage)
    {
        _context = context;
        _documentStorage = documentStorage;
    }

    public Task<PublicationValidationResult> ValidateAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default) =>
        ValidateForStatusesAsync(
            templateVersionId,
            [TemplateVersionStatus.Draft, TemplateVersionStatus.ReadyForPublication],
            "Публікувати можна тільки чернетку або версію, готову до публікації.",
            cancellationToken);

    public Task<PublicationValidationResult> ValidateRepublishAsync(
        Guid templateVersionId,
        CancellationToken cancellationToken = default) =>
        ValidateForStatusesAsync(
            templateVersionId,
            [TemplateVersionStatus.Superseded],
            "Повторно зробити поточною можна лише версію зі статусом «Замінено».",
            cancellationToken);

    private async Task<PublicationValidationResult> ValidateForStatusesAsync(
        Guid templateVersionId,
        IReadOnlyCollection<TemplateVersionStatus> allowedStatuses,
        string invalidStatusMessage,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var version = await _context.TemplateVersions
            .Include(templateVersion => templateVersion.Fields)
                .ThenInclude(field => field.DataField)
            .Include(templateVersion => templateVersion.Fields)
                .ThenInclude(field => field.Permissions)
            .Include(templateVersion => templateVersion.Fields)
                .ThenInclude(field => field.Segments)
            .FirstOrDefaultAsync(templateVersion => templateVersion.Id == templateVersionId, cancellationToken);

        if (version is null)
        {
            return new PublicationValidationResult
            {
                Errors = ["Версію шаблону не знайдено."]
            };
        }

        if (!allowedStatuses.Contains(version.Status))
        {
            errors.Add(invalidStatusMessage);
        }

        if (!await _documentStorage.ExistsAsync(version.StorageKey, cancellationToken))
        {
            errors.Add("Оригінальний файл шаблону не знайдено у сховищі.");
        }
        else if (!await HasExpectedHashAsync(version, cancellationToken))
        {
            errors.Add("Контрольна сума файлу шаблону не збігається із зареєстрованою версією.");
        }

        if (version.Fields.Count == 0)
        {
            errors.Add("У версії шаблону немає зареєстрованих полів для заповнення.");
        }

        var duplicateTags = version.Fields
            .GroupBy(field => field.NormalizedTag, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateTags.Count > 0)
        {
            errors.Add($"У версії є дублікати тегів полів: {string.Join(", ", duplicateTags)}.");
        }

        foreach (var field in version.Fields.OrderBy(field => field.SortOrder))
        {
            if (field.Status != TemplateFieldStatus.Ignored && field.IsRequired && field.DataFieldId is null)
            {
                errors.Add($"Поле \"{field.Tag}\" не замаплене на DataField.");
            }

            if (field.DataFieldId is not null && field.DataField is null)
            {
                errors.Add($"Поле \"{field.Tag}\" посилається на неактивний або анульований DataField.");
            }
            else if (field.DataField is { IsActive: false })
            {
                errors.Add($"DataField для поля \"{field.Tag}\" вимкнений.");
            }

            if (field.DataField is { MaxLength: not null } &&
                field.EstimatedCapacityChars is not null &&
                field.OverflowPolicy == FieldOverflowPolicy.Block &&
                field.DataField.MaxLength > field.EstimatedCapacityChars)
            {
                errors.Add(
                    $"DataField \"{field.DataField.Key}\" має MaxLength {field.DataField.MaxLength}, що перевищує місткість поля \"{field.Tag}\" ({field.EstimatedCapacityChars}).");
            }

            if (version.DocumentFormat == TemplateDocumentFormat.Pdf)
            {
                var primarySegment = field.GetPrimarySegment();
                var hasLayout = primarySegment is not null &&
                                primarySegment.PageNumber > 0 &&
                                primarySegment.Width > 0 &&
                                primarySegment.Height > 0;
                if (!hasLayout)
                {
                    errors.Add($"Для PDF поля \"{field.Tag}\" не задано повну overlay-геометрію (Page/X/Y/Width/Height).");
                }
            }

            var configuredRoles = field.Permissions
                .Select(permission => permission.RoleName)
                .ToHashSet(StringComparer.Ordinal);

            var missingRoles = LimsRoles.All
                .Where(role => !configuredRoles.Contains(role))
                .ToList();

            if (missingRoles.Count > 0)
            {
                errors.Add(
                    $"Для поля \"{field.Tag}\" не налаштовано права ролей: {string.Join(", ", missingRoles)}.");
            }
        }

        return new PublicationValidationResult
        {
            Errors = errors
        };
    }

    private async Task<bool> HasExpectedHashAsync(
        TemplateVersion version,
        CancellationToken cancellationToken)
    {
        await using var stream = await _documentStorage.OpenReadAsync(version.StorageKey, cancellationToken);
        using var sha256 = SHA256.Create();
        var actualHash = Convert.ToHexString(await sha256.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();

        return string.Equals(actualHash, version.Sha256Hash, StringComparison.OrdinalIgnoreCase);
    }
}
