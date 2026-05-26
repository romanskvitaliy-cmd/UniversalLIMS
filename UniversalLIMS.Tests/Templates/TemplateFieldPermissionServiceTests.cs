using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Security;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplateFieldPermissionServiceTests
{
    [Fact]
    public async Task GetFieldAccessLevelsForVersionAsync_ReturnsWriteForRegistrarWhenConfigured()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        SeedVersionWithPermission(context, versionId, fieldId, LimsRoles.Registrar, FieldAccessLevel.Write);

        var service = CreateService(context, LimsRoles.Registrar);
        var levels = await service.GetFieldAccessLevelsForVersionAsync(versionId);

        Assert.Equal(FieldAccessLevel.Write, levels[fieldId]);
    }

    [Fact]
    public async Task GetFieldAccessLevelsForVersionAsync_ReturnsNoneWhenRoleHasNoPermissionRow()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        SeedVersionWithPermission(context, versionId, fieldId, LimsRoles.Registrar, FieldAccessLevel.Write);

        var service = CreateService(context, LimsRoles.LaboratoryTechnician);
        var levels = await service.GetFieldAccessLevelsForVersionAsync(versionId);

        Assert.Equal(FieldAccessLevel.None, levels[fieldId]);
    }

    [Fact]
    public async Task GetFieldAccessLevelsForVersionAsync_FallsBackToWriteWhenMatrixNotConfigured()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        SeedVersionWithoutPermissions(context, versionId, fieldId);

        var service = CreateService(context, LimsRoles.LaboratoryTechnician);
        var levels = await service.GetFieldAccessLevelsForVersionAsync(versionId);

        Assert.Equal(FieldAccessLevel.Write, levels[fieldId]);
    }

    [Fact]
    public async Task GetFieldAccessLevelsForVersionAsync_AdministratorAlwaysHasWrite()
    {
        await using var context = CreateContext();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        SeedVersionWithPermission(context, versionId, fieldId, LimsRoles.Registrar, FieldAccessLevel.Read);

        var service = CreateService(context, LimsRoles.SystemAdministrator);
        var levels = await service.GetFieldAccessLevelsForVersionAsync(versionId);

        Assert.Equal(FieldAccessLevel.Write, levels[fieldId]);
    }

    private static TemplateFieldPermissionService CreateService(ApplicationDbContext context, string activeRole)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, activeRole)],
                authenticationType: "Test"))
        };
        httpContext.Session = new TestSession();
        httpContext.Session.SetString(SessionKeys.ActiveLimsRole, activeRole);

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new TemplateFieldPermissionService(context, new ActiveLimsRoleService(accessor), accessor);
    }

    private static void SeedVersionWithPermission(
        ApplicationDbContext context,
        Guid versionId,
        Guid fieldId,
        string roleName,
        FieldAccessLevel accessLevel)
    {
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "PERM",
            NameUk = "Permission test",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
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
                    Tag = "FieldA",
                    SortOrder = 1,
                    Permissions =
                    [
                        new TemplateFieldPermission
                        {
                            RoleName = roleName,
                            AccessLevel = accessLevel
                        }
                    ]
                }
            ]
        });

        context.SaveChanges();
    }

    private static void SeedVersionWithoutPermissions(
        ApplicationDbContext context,
        Guid versionId,
        Guid fieldId)
    {
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "OPEN",
            NameUk = "Open template",
            Status = TemplateStatus.Draft
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
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
                    Id = fieldId,
                    TemplateVersionId = versionId,
                    Tag = "OpenField",
                    SortOrder = 1
                }
            ]
        });

        context.SaveChanges();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Load() { }

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
