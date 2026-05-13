using System.IO.Compression;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class ZipDocxContentControlReaderTests
{
    [Fact]
    public async Task ReadContentControlsAsync_ReturnsTaggedContentControlsOnly()
    {
        await using var document = CreateDocxStream(
            """
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:sdt>
                  <w:sdtPr>
                    <w:alias w:val="pH результат" />
                    <w:tag w:val="result_ph" />
                    <w:text />
                  </w:sdtPr>
                  <w:sdtContent><w:p><w:r><w:t>7.00</w:t></w:r></w:p></w:sdtContent>
                </w:sdt>
                <w:sdt>
                  <w:sdtPr><w:alias w:val="Без Tag" /><w:text /></w:sdtPr>
                  <w:sdtContent><w:p><w:r><w:t>ignored</w:t></w:r></w:p></w:sdtContent>
                </w:sdt>
                <w:p><w:r><w:t>Звичайний текст поза тегами ігнорується</w:t></w:r></w:p>
                <w:sdt>
                  <w:sdtPr>
                    <w:alias w:val="Дата" />
                    <w:tag w:val="sample_registered_at" />
                    <w:date />
                  </w:sdtPr>
                  <w:sdtContent><w:p><w:r><w:t>06.05.2026</w:t></w:r></w:p></w:sdtContent>
                </w:sdt>
              </w:body>
            </w:document>
            """);

        var reader = new ZipDocxContentControlReader();

        var fields = await reader.ReadContentControlsAsync(document);

        Assert.Collection(
            fields,
            field =>
            {
                Assert.Equal("result_ph", field.Tag);
                Assert.Equal("pH результат", field.Title);
                Assert.Equal(WordContentControlType.Text, field.ControlType);
                Assert.Equal(1, field.SortOrder);
            },
            field =>
            {
                Assert.Equal("sample_registered_at", field.Tag);
                Assert.Equal(WordContentControlType.Date, field.ControlType);
                Assert.Equal(2, field.SortOrder);
            });
    }

    [Fact]
    public async Task ReadContentControlsAsync_ThrowsInvalidDataException_WhenStreamIsNotDocxZip()
    {
        await using var document = new MemoryStream("not a docx"u8.ToArray());
        var reader = new ZipDocxContentControlReader();

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadContentControlsAsync(document));
    }

    private static MemoryStream CreateDocxStream(string documentXml)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(documentXml);
        }

        stream.Position = 0;
        return stream;
    }
}
