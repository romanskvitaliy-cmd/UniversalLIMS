namespace UniversalLIMS.Application.Templates.Abstractions;

public interface IWordToPdfDocumentConverter
{
    Task<MemoryStream> ConvertAsync(
        Stream wordDocumentStream,
        string extension,
        CancellationToken cancellationToken = default);
}
