namespace UniversalLIMS.Application.Templates;

public sealed class PublicationValidationResult
{
    public IReadOnlyCollection<string> Errors { get; init; } = [];

    public bool IsValid => Errors.Count == 0;
}
