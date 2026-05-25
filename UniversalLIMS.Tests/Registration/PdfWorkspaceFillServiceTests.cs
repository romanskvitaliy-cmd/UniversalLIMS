using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class PdfWorkspaceFillServiceTests
{
    [Fact]
    public async Task SaveValuesAsync_UpsertsOrderFieldValueWhenTemplateFieldIsMapped()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-1",
            Name = "Test branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Test customer"
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Sample.SamplingLocation",
            DisplayNameUk = "Місце відбору",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-SAVE",
            NameUk = "PDF save test",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-1",
            NameUk = "Test investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = Guid.NewGuid(),
                    TemplateVersionId = versionId,
                    Tag = "SamplingLocation",
                    Title = "Місце відбору",
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 10,
                            PositionY = 20,
                            Width = 100,
                            Height = 20,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var templateField = await context.TemplateFields.SingleAsync(field => field.TemplateVersionId == versionId);

        var result = await service.SaveValuesAsync(
            versionId,
            null,
            [new PdfWorkspaceFieldValueDto { TemplateFieldId = templateField.Id, Value = "м. Житомир" }]);

        Assert.Equal(1, result.Received);
        Assert.Equal(1, result.Mapped);
        Assert.Equal(1, result.Saved);
        Assert.Equal(0, result.SkippedUnmapped);
        Assert.Equal(0, result.SkippedEmpty);

        var stored = await context.OrderFieldValues
            .SingleAsync(fieldValue => fieldValue.OrderId == result.OrderId);

        var workspaceDataField = await context.DataFields
            .SingleAsync(field => field.Key == templateField.Id.ToString("D"));

        Assert.Equal(workspaceDataField.Id, stored.DataFieldId);
        Assert.Equal("м. Житомир", stored.StoredValue);
    }

    [Fact]
    public async Task SaveValuesAsync_CreatesWorkspaceDataFieldsPerTemplateFieldId()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-2",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-MULTI",
            NameUk = "Multi field",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-2",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        var tags = new[] { "Global.DocNumber", "ProtocolNumber", "Global.FacilityName", "Air.Temperature" };
        var templateFieldIds = tags.Select(_ => Guid.NewGuid()).ToArray();
        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('c', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields = tags.Select((tag, index) => new TemplateField
            {
                Id = templateFieldIds[index],
                TemplateVersionId = versionId,
                Tag = tag,
                Title = tag,
                SortOrder = index + 1,
                Segments =
                [
                    new TemplateFieldSegment
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        PageNumber = 1,
                        PositionX = 10,
                        PositionY = 20 + index * 30,
                        Width = 100,
                        Height = 20,
                        IsPrimary = true
                    }
                ]
            }).ToList()
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var submissions = templateFieldIds
            .Select((id, index) => new PdfWorkspaceFieldValueDto
            {
                TemplateFieldId = id,
                Value = $"value-{index + 1}"
            })
            .ToList();

        var result = await service.SaveValuesAsync(versionId, null, submissions);

        Assert.Equal(tags.Length, result.Received);
        Assert.Equal(tags.Length, result.Mapped);
        Assert.Equal(tags.Length, result.Saved);
        Assert.Equal(0, result.SkippedUnmapped);
        Assert.Empty(result.FailedFields);

        var stored = await context.OrderFieldValues
            .Where(fieldValue => fieldValue.OrderId == result.OrderId)
            .ToListAsync();
        Assert.Equal(tags.Length, stored.Count);
        Assert.All(stored, fieldValue => Assert.False(string.IsNullOrWhiteSpace(fieldValue.StoredValue)));

        var workspaceKeys = templateFieldIds.Select(id => id.ToString("D")).ToHashSet();
        var workspaceDataFields = await context.DataFields
            .Where(field => workspaceKeys.Contains(field.Key))
            .ToListAsync();
        Assert.Equal(tags.Length, workspaceDataFields.Count);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_RendersAllSampleFields()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var fieldIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL",
            NameUk = "Calibration preview",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 32,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('h', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields = fieldIds.Select((id, index) => new TemplateField
            {
                Id = id,
                TemplateVersionId = versionId,
                Tag = $"Field{index + 1}",
                Title = $"Поле {index + 1}",
                TextOffsetX = index,
                TextOffsetY = -index,
                SortOrder = index + 1,
                Segments =
                [
                    new TemplateFieldSegment
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        PageNumber = 1,
                        PositionX = 50,
                        PositionY = 80 + index * 40,
                        Width = 200,
                        Height = 24,
                        IsPrimary = true,
                        FontSize = 10,
                        VerticalAlignment = "Top"
                    }
                ]
            }).ToList()
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var request = new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            IsCalibrationPreview = true,
            Fields = fieldIds.Select((id, index) => new PreviewFieldDto
            {
                TemplateFieldId = id,
                Text = $"текст-{index + 1}",
                Page = 1,
                X = 50,
                Y = 80 + index * 40,
                Width = 200,
                Height = 24,
                FontSize = 10,
                VerticalAlignment = "Top"
            }).ToList()
        };

        var preview = await service.GenerateCalibrationPreviewAsync(request);
        Assert.NotEmpty(preview.PdfBytes);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
        Assert.Equal(3, preview.SegmentsDrawn);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_ClientGeometryWithoutDatabaseSegments_RendersText()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-WYSIWYG",
            NameUk = "Calibration WYSIWYG",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 33,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('w', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var request = new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            IsCalibrationPreview = true,
            Fields =
            [
                new PreviewFieldDto
                {
                    Text = "живий текст з екрана",
                    Page = 1,
                    X = 80,
                    Y = 120,
                    Width = 200,
                    Height = 28,
                    FontSize = 10
                }
            ]
        };

        var preview = await service.GenerateCalibrationPreviewAsync(request);
        Assert.NotEmpty(preview.PdfBytes);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
        Assert.Equal(1, preview.SegmentsDrawn);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_WithoutText_StillReturnsPdf()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-EMPTY",
            NameUk = "Calibration empty geometry",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 34,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            Fields =
            [
                new PreviewFieldDto
                {
                    Text = "   ",
                    Page = 1,
                    X = 80,
                    Y = 120,
                    Width = 200,
                    Height = 28
                }
            ]
        });

        Assert.NotEmpty(preview.PdfBytes);
        Assert.Equal(0, preview.SegmentsDrawn);
        Assert.Equal(1, preview.SegmentsSkippedEmpty);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_TextToDrawProperty_RendersOnPdf()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-TEXT-TO-DRAW",
            NameUk = "Calibration textToDraw",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 36,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('t', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            Fields =
            [
                new PreviewFieldDto
                {
                    Text = string.Empty,
                    TextToDraw = "11",
                    Page = 1,
                    X = 80,
                    Y = 120,
                    Width = 200,
                    Height = 28,
                    FontSize = 14
                }
            ]
        });

        Assert.Equal(1, preview.SegmentsDrawn);
        Assert.Equal(0, preview.SegmentsSkippedEmpty);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_ValuePropertyFromOverlay_RendersOnPdf()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-VALUE",
            NameUk = "Calibration value",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 37,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('v', 64),
            UploadedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            IsCalibrationPreview = true,
            Fields =
            [
                new PreviewFieldDto
                {
                    Text = "текст з overlay input",
                    Page = 1,
                    X = 90,
                    Y = 140,
                    Width = 220,
                    Height = 28,
                    FontSize = 12
                }
            ]
        });

        Assert.Equal(1, preview.SegmentsDrawn);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_UiTextWithMissingLayout_UsesDatabaseSegmentGeometry()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-DB-LAYOUT",
            NameUk = "Calibration DB layout fallback",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 35,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('l', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Field1",
                    Title = "Поле 1",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 90,
                            PositionY = 110,
                            Width = 180,
                            Height = 26,
                            IsPrimary = true,
                            FontSize = 10
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            Fields =
            [
                new PreviewFieldDto
                {
                    TemplateFieldId = fieldId,
                    Text = "текст з UI",
                    Page = 1,
                    X = 0,
                    Y = 0,
                    Width = 0,
                    Height = 0
                }
            ]
        });

        Assert.Equal(1, preview.SegmentsDrawn);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_SameTemplateFieldIdAtDifferentPositions_DrawsAllSegments()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-DUP",
            NameUk = "Calibration duplicate field",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 36,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "DupField",
                    Title = "Дубль",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 50,
                            PositionY = 80,
                            Width = 200,
                            Height = 24,
                            IsPrimary = true,
                            FontSize = 10
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            Fields =
            [
                new PreviewFieldDto
                {
                    TemplateFieldId = fieldId,
                    SegmentSequence = 1,
                    Text = "верх",
                    Page = 1,
                    X = 50,
                    Y = 80,
                    Width = 200,
                    Height = 24
                },
                new PreviewFieldDto
                {
                    TemplateFieldId = fieldId,
                    SegmentSequence = 1,
                    Text = "низ",
                    Page = 1,
                    X = 50,
                    Y = 140,
                    Width = 200,
                    Height = 24
                }
            ]
        });

        Assert.Equal(2, preview.SegmentsDrawn);
    }

    [Fact]
    public async Task GenerateCalibrationPreviewAsync_ClientLayoutMismatchAddsOverlayPositionSegment()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CAL-MISMATCH",
            NameUk = "Calibration layout mismatch",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 37,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('m', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "RegistrationNumber",
                    Title = "Реєстраційний №",
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 295,
                            PositionY = 400,
                            Width = 200,
                            Height = 14,
                            IsPrimary = true,
                            FontSize = 10
                        }
                    ]
                }
            ]
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var preview = await service.GenerateCalibrationPreviewAsync(new PreviewCalibrationRequest
        {
            TemplateVersionId = versionId,
            Fields =
            [
                new PreviewFieldDto
                {
                    TemplateFieldId = fieldId,
                    Text = "REG-123",
                    Page = 1,
                    // DOM drift (як у конструкторі), БД — PositionY = 400.
                    X = 295,
                    Y = 518,
                    Width = 448,
                    Height = 14
                }
            ]
        });

        Assert.Equal(1, preview.SegmentsDrawn);
        Assert.True(preview.PdfBytes.Length > CreateBlankPdf().Length);
    }

    [Fact]
    public async Task SaveValuesAsync_ThenGenerateFilledPdf_RendersAllWorkspaceFields()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var templateFieldIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-10",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-10",
            NameUk = "Ten fields",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-10",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('g', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields = templateFieldIds.Select((id, index) => new TemplateField
            {
                Id = id,
                TemplateVersionId = versionId,
                Tag = $"Field{index + 1}",
                Title = $"Поле {index + 1}",
                SortOrder = index + 1,
                Segments =
                [
                    new TemplateFieldSegment
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        PageNumber = 1,
                        PositionX = 40 + index * 5,
                        PositionY = 40 + index * 28,
                        Width = 120,
                        Height = 22,
                        IsPrimary = true
                    }
                ]
            }).ToList()
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var submissions = templateFieldIds
            .Select((id, index) => new PdfWorkspaceFieldValueDto
            {
                TemplateFieldId = id,
                Value = $"значення-{index + 1}"
            })
            .ToList();

        var saveResult = await service.SaveValuesAsync(versionId, null, submissions);
        Assert.Equal(10, saveResult.Saved);

        var rendered = await service.GenerateFilledPdfAsync(versionId, saveResult.OrderId);
        Assert.NotEmpty(rendered);
        Assert.True(rendered.Length > CreateBlankPdf().Length);
    }

    [Fact]
    public async Task GenerateFilledPdfAsync_IncludesSavedOrderFieldValuesInOverlay()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var templateFieldId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-PDF",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Sample.SamplingLocation",
            DisplayNameUk = "Місце відбору",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-FINAL",
            NameUk = "Final PDF test",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-PDF",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Draft,
            ReferralNumber = "PDF-FINAL-1"
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('e', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = templateFieldId,
                    TemplateVersionId = versionId,
                    Tag = "SamplingLocation",
                    Title = "Місце відбору",
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 135,
                            PositionY = 135,
                            Width = 270,
                            Height = 54,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        var workspaceDataFieldId = Guid.NewGuid();
        context.DataFields.Add(new DataField
        {
            Id = workspaceDataFieldId,
            Key = templateFieldId.ToString("D"),
            DisplayNameUk = "Місце відбору",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        context.OrderFieldValues.Add(new OrderFieldValue
        {
            OrderId = orderId,
            SampleId = null,
            DataFieldId = workspaceDataFieldId,
            StoredValue = "м. Житомир"
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var blankPdf = CreateBlankPdf();
        var rendered = await service.GenerateFilledPdfAsync(versionId, orderId);

        Assert.NotEmpty(rendered);
        Assert.NotEqual(blankPdf.Length, rendered.Length);
    }

    [Fact]
    public async Task GenerateFilledPdfAsync_UsesSampleScopedValueWhenOrderLevelValueMissing()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sampleId = Guid.NewGuid();
        var templateFieldId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var investigationTypeId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-SAMPLE",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "INV-SAMPLE",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Sample.Note",
            DisplayNameUk = "Примітка",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-SAMPLE",
            NameUk = "Sample scoped",
            Status = TemplateStatus.Draft
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Draft,
            ReferralNumber = "PDF-SAMPLE-1"
        });

        context.Samples.Add(new Sample
        {
            Id = sampleId,
            OrderId = orderId,
            InvestigationTypeId = investigationTypeId,
            Number = "S-001",
            RegisteredAt = DateTime.UtcNow
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('f', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = templateFieldId,
                    TemplateVersionId = versionId,
                    Tag = "Note",
                    Title = "Примітка",
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 135,
                            PositionY = 135,
                            Width = 270,
                            Height = 54,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        context.OrderFieldValues.Add(new OrderFieldValue
        {
            OrderId = orderId,
            SampleId = sampleId,
            DataFieldId = dataFieldId,
            StoredValue = "Значення проби"
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(CreateBlankPdf()),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var rendered = await service.GenerateFilledPdfAsync(versionId, orderId);

        Assert.NotEmpty(rendered);
    }

    [Fact]
    public async Task SaveValuesAsync_DeletesStoredValueWhenSubmittedValueIsEmpty()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var templateFieldId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BR-3",
            Name = "Branch",
            City = "Zhytomyr",
            IsActive = true
        });

        context.Customers.Add(new Customer
        {
            Id = customerId,
            Kind = CustomerKind.Individual,
            FullName = "Customer"
        });

        context.DataFields.Add(new DataField
        {
            Id = dataFieldId,
            Key = "Sample.Note",
            DisplayNameUk = "Примітка",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PDF-CLEAR",
            NameUk = "Clear test",
            Status = TemplateStatus.Draft
        });

        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = Guid.NewGuid(),
            Code = "INV-3",
            NameUk = "Investigation",
            SortOrder = 1,
            IsActive = true
        });

        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = branchId,
            Status = OrderStatus.Draft,
            ReferralNumber = "PDF-CLEAR-1"
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Draft,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "template.pdf",
            StorageKey = "templates/template.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('d', 64),
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = templateFieldId,
                    TemplateVersionId = versionId,
                    Tag = "Note",
                    Title = "Примітка",
                    DataFieldId = dataFieldId,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = Guid.NewGuid(),
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 10,
                            PositionY = 20,
                            Width = 100,
                            Height = 20,
                            IsPrimary = true
                        }
                    ]
                }
            ]
        });

        var workspaceDataFieldId = Guid.NewGuid();
        context.DataFields.Add(new DataField
        {
            Id = workspaceDataFieldId,
            Key = templateFieldId.ToString("D"),
            DisplayNameUk = "Примітка",
            FieldType = DataFieldType.Text,
            Scope = DataFieldScope.Registration,
            IsActive = true
        });

        context.OrderFieldValues.Add(new OrderFieldValue
        {
            OrderId = orderId,
            SampleId = null,
            DataFieldId = workspaceDataFieldId,
            StoredValue = "старе значення"
        });

        await context.SaveChangesAsync();

        var service = new PdfWorkspaceFillService(
            context,
            new FakeTemplateDocumentStorage(),
            new OrderFieldValueService(context),
            NullLogger<PdfWorkspaceFillService>.Instance,
            NullLogger<ReferralPdfOverlayRenderer>.Instance);

        var result = await service.SaveValuesAsync(
            versionId,
            orderId,
            [new PdfWorkspaceFieldValueDto { TemplateFieldId = templateFieldId, Value = "   " }]);

        Assert.Equal(1, result.Mapped);
        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.SkippedEmpty);
        Assert.Empty(await context.OrderFieldValues.Where(fieldValue => fieldValue.OrderId == orderId).ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static byte[] CreateBlankPdf()
    {
        using var document = new PdfDocument();
        document.Pages.Add();

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private sealed class FakeTemplateDocumentStorage : Application.Templates.Abstractions.ITemplateDocumentStorage
    {
        private readonly byte[] _pdfBytes;

        public FakeTemplateDocumentStorage(byte[]? pdfBytes = null)
        {
            _pdfBytes = pdfBytes ?? CreateBlankPdf();
        }

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_pdfBytes, writable: false));

        public Task<Application.Templates.Abstractions.StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.Templates.Abstractions.StoredTemplateDocument(
                "templates/test.pdf",
                originalFileName,
                contentType,
                1,
                new string('b', 64)));
    }
}
