using System.IO.Compression;
using System.Xml.Linq;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class ZipDocxContentControlReader : IDocxContentControlReader
{
    private static readonly XNamespace WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly XName SdtName = WordNamespace + "sdt";
    private static readonly XName SdtPropertiesName = WordNamespace + "sdtPr";
    private static readonly XName TagName = WordNamespace + "tag";
    private static readonly XName AliasName = WordNamespace + "alias";
    private static readonly XName ValueAttributeName = WordNamespace + "val";
    private static readonly XName TextName = WordNamespace + "t";
    private static readonly XName ParagraphName = WordNamespace + "p";

    public Task<IReadOnlyCollection<DocxContentControlInfo>> ReadContentControlsAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(documentStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("Файл не є коректним ZIP/.docx контейнером.", exception);
        }

        using (archive)
        {
            var controls = new List<DocxContentControlInfo>();

            foreach (var entry in archive.Entries.Where(IsWordXmlPart))
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var entryStream = entry.Open();
                var document = XDocument.Load(entryStream);
                var contentControls = document.Descendants(SdtName);

                foreach (var contentControl in contentControls)
                {
                    var properties = contentControl.Element(SdtPropertiesName);
                    var tag = properties?.Element(TagName)?.Attribute(ValueAttributeName)?.Value;
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    controls.Add(new DocxContentControlInfo(
                        tag.Trim(),
                        properties?.Element(AliasName)?.Attribute(ValueAttributeName)?.Value,
                        ResolveControlType(properties),
                        controls.Count + 1,
                        EstimateCapacityChars(contentControl),
                        AllowsMultiline(contentControl)));
                }
            }

            var distinctControls = controls
                .GroupBy(control => control.Tag, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(control => control.SortOrder).First())
                .OrderBy(control => control.SortOrder)
                .Select((control, index) => control with { SortOrder = index + 1 })
                .ToList();

            return Task.FromResult<IReadOnlyCollection<DocxContentControlInfo>>(distinctControls);
        }
    }

    private static WordContentControlType ResolveControlType(XElement? properties)
    {
        if (properties is null)
        {
            return WordContentControlType.Unknown;
        }

        if (properties.Element(WordNamespace + "date") is not null)
        {
            return WordContentControlType.Date;
        }

        if (properties.Element(WordNamespace + "dropDownList") is not null)
        {
            return WordContentControlType.DropDownList;
        }

        if (properties.Element(WordNamespace + "comboBox") is not null)
        {
            return WordContentControlType.ComboBox;
        }

        if (properties.Element(WordNamespace + "checkBox") is not null)
        {
            return WordContentControlType.CheckBox;
        }

        if (properties.Element(WordNamespace + "picture") is not null)
        {
            return WordContentControlType.Picture;
        }

        if (properties.Element(WordNamespace + "richText") is not null)
        {
            return WordContentControlType.RichText;
        }

        if (properties.Element(WordNamespace + "text") is not null)
        {
            return WordContentControlType.Text;
        }

        return WordContentControlType.Text;
    }

    private static int? EstimateCapacityChars(XElement contentControl)
    {
        var placeholderTextLength = contentControl
            .Descendants(TextName)
            .Select(text => text.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Sum(value => value.Length);

        if (placeholderTextLength <= 0)
        {
            return null;
        }

        return Math.Max(placeholderTextLength, 10);
    }

    private static bool AllowsMultiline(XElement contentControl)
    {
        return contentControl.Descendants(ParagraphName).Skip(1).Any();
    }

    private static bool IsWordXmlPart(ZipArchiveEntry entry)
    {
        return entry.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }
}
