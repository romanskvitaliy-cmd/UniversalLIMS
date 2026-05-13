namespace UniversalLIMS.Application.Templates.Abstractions;

public interface IDocxContentControlReader
{
    Task<IReadOnlyCollection<DocxContentControlInfo>> ReadContentControlsAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default);
}
