namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplatePublicationValidator
{
    Task<PublicationValidationResult> ValidateAsync(Guid templateVersionId, CancellationToken cancellationToken = default);
}
