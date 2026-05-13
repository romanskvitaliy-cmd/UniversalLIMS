using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using UniversalLIMS.Application.Templates.Abstractions;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class SyncfusionWordToPdfDocumentConverter : IWordToPdfDocumentConverter
{
    public async Task<MemoryStream> ConvertAsync(
        Stream wordDocumentStream,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wordDocumentStream);

        if (wordDocumentStream.CanSeek)
        {
            wordDocumentStream.Position = 0;
        }

        await using var bufferedWordDocument = new MemoryStream();
        await wordDocumentStream.CopyToAsync(bufferedWordDocument, cancellationToken);
        bufferedWordDocument.Position = 0;

        using var wordDocument = new WordDocument(bufferedWordDocument, ResolveFormatType(extension));
        using var renderer = new DocIORenderer();
        using PdfDocument pdfDocument = renderer.ConvertToPDF(wordDocument);

        var pdfStream = new MemoryStream();
        pdfDocument.Save(pdfStream);
        pdfStream.Position = 0;
        return pdfStream;
    }

    private static FormatType ResolveFormatType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".doc" => FormatType.Doc,
            ".docx" => FormatType.Docx,
            _ => throw new InvalidOperationException("Дозволено конвертувати тільки файли .doc або .docx.")
        };
    }
}
