using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplatePublicationValidatorRepublishTests
{
    [Fact]
    public async Task ValidateRepublishAsync_SupersededVersionWithValidField_IsValid()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fileContent = Encoding.UTF8.GetBytes("template-pdf-content");
        await SeedSupersededVersionAsync(context, versionId, fileContent);

        var validator = new TemplatePublicationValidator(context, new TestTemplateDocumentStorage(fileContent));
        var result = await validator.ValidateRepublishAsync(versionId);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(TemplateVersionStatus.Published)]
    [InlineData(TemplateVersionStatus.Draft)]
    [InlineData(TemplateVersionStatus.ReadyForPublication)]
    public async Task ValidateRepublishAsync_NonSupersededStatus_ReturnsStatusError(TemplateVersionStatus status)
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fileContent = Encoding.UTF8.GetBytes("template-pdf-content");
        await SeedVersionAsync(context, versionId, status, fileContent);

        var validator = new TemplatePublicationValidator(context, new TestTemplateDocumentStorage(fileContent));
        var result = await validator.ValidateRepublishAsync(versionId);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("Замінено", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateRepublishAsync_MissingVersion_ReturnsNotFoundError()
    {
        await using var context = CreateContext();
        var validator = new TemplatePublicationValidator(context, new TestTemplateDocumentStorage([]));

        var result = await validator.ValidateRepublishAsync(Guid.NewGuid());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("не знайдено", StringComparison.OrdinalIgnoreCase));
    }

    private static Task SeedSupersededVersionAsync(
        ApplicationDbContext context,
        Guid versionId,
        byte[] fileContent) =>
        SeedVersionAsync(context, versionId, TemplateVersionStatus.Superseded, fileContent);

    private static async Task SeedVersionAsync(
        ApplicationDbContext context,
        Guid versionId,
        TemplateVersionStatus status,
        byte[] fileContent)
    {
        var templateId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var segmentId = Guid.NewGuid();
        var hash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();

        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T-VAL",
            NameUk = "Validator test",
            Status = TemplateStatus.Active
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 5,
            Status = status,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "v5.pdf",
            StorageKey = "templates/v5.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = fileContent.Length,
            Sha256Hash = hash,
            UploadedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new TemplateField
                {
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "Field1",
                    NormalizedTag = "FIELD1",
                    Title = "Field",
                    IsRequired = false,
                    SortOrder = 1,
                    Segments =
                    [
                        new TemplateFieldSegment
                        {
                            Id = segmentId,
                            TemplateFieldId = fieldId,
                            Sequence = 1,
                            PageNumber = 1,
                            PositionX = 1,
                            PositionY = 1,
                            Width = 50,
                            Height = 20,
                            IsPrimary = true
                        }
                    ],
                    Permissions = LimsRoles.All
                        .Select(role => new TemplateFieldPermission
                        {
                            TemplateFieldId = fieldId,
                            RoleName = role,
                            AccessLevel = FieldAccessLevel.Write
                        })
                        .ToList()
                }
            ]
        });

        await context.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestTemplateDocumentStorage(byte[] content) : ITemplateDocumentStorage
    {
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(content, writable: false));

        public Task<StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
