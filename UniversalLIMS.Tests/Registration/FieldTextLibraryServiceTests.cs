using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;
using UniversalLIMS.Infrastructure.Security;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS.Tests.Registration;

public sealed class FieldTextLibraryServiceTests
{
    [Fact]
    public async Task UpsertAsync_IsIdempotentForSameBody_IncrementsUsageCount()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        SeedBranch(context, branchId);
        SeedField(context, versionId, fieldId, dataFieldId, "TransportConditions", branchId);
        SeedPermission(context, fieldId, LimsRoles.LaboratoryTechnician, FieldAccessLevel.Write);

        var service = CreateService(context, LimsRoles.LaboratoryTechnician, branchId);

        var first = await service.UpsertAsync(
            versionId,
            new FieldTextLibraryUpsertRequest
            {
                TemplateFieldId = fieldId,
                Body = "  Доставка   в термоконтейнері  "
            });

        var second = await service.UpsertAsync(
            versionId,
            new FieldTextLibraryUpsertRequest
            {
                TemplateFieldId = fieldId,
                Body = "Доставка в термоконтейнері"
            });

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(2, second.Entry.UsageCount);
        Assert.Equal(1, await context.FieldTextLibraryEntries.CountAsync());

        var stored = await context.FieldTextLibraryEntries.SingleAsync();
        Assert.Null(stored.DataFieldId);
        Assert.Equal("TRANSPORTCONDITIONS", stored.NormalizedTag);
    }

    [Fact]
    public async Task ListForFieldAsync_UsesTagNotSharedDataFieldId()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var sharedDataFieldId = Guid.NewGuid();
        var fieldF327 = Guid.NewGuid();
        var fieldFood = Guid.NewGuid();

        SeedBranch(context, branchId);
        SeedField(context, versionId, fieldF327, sharedDataFieldId, "f327_pH", branchId);
        SeedAdditionalField(context, versionId, fieldFood, sharedDataFieldId, "Food_pH");
        SeedPermission(context, fieldF327, LimsRoles.LaboratoryTechnician, FieldAccessLevel.Write);
        SeedPermission(context, fieldFood, LimsRoles.LaboratoryTechnician, FieldAccessLevel.Write);

        context.FieldTextLibraryEntries.Add(new FieldTextLibraryEntry
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            DataFieldId = null,
            NormalizedTag = "F327_PH",
            Body = "7.2",
            NormalizedBodyHash = "hash-f327",
            UsageCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        context.FieldTextLibraryEntries.Add(new FieldTextLibraryEntry
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            DataFieldId = null,
            NormalizedTag = "FOOD_PH",
            Body = "6.8",
            NormalizedBodyHash = "hash-food",
            UsageCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, LimsRoles.LaboratoryTechnician, branchId);

        var f327List = await service.ListForFieldAsync(versionId, fieldF327, null);
        var foodList = await service.ListForFieldAsync(versionId, fieldFood, null);

        Assert.Single(f327List.Entries);
        Assert.Equal("7.2", f327List.Entries[0].Body);
        Assert.Equal("f327_pH", f327List.FieldTag);

        Assert.Single(foodList.Entries);
        Assert.Equal("6.8", foodList.Entries[0].Body);
        Assert.Equal("Food_pH", foodList.FieldTag);
    }

    [Fact]
    public async Task ListForFieldAsync_IncludesLegacyDataFieldEntriesForSameTag()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var dataFieldId = Guid.NewGuid();

        SeedBranch(context, branchId);
        SeedField(context, versionId, fieldId, dataFieldId, "TransportConditions", branchId);
        SeedPermission(context, fieldId, LimsRoles.LaboratoryTechnician, FieldAccessLevel.Write);

        context.FieldTextLibraryEntries.Add(new FieldTextLibraryEntry
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            DataFieldId = dataFieldId,
            NormalizedTag = null,
            Body = "Старий запис",
            NormalizedBodyHash = "legacy-hash",
            UsageCount = 3,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, LimsRoles.LaboratoryTechnician, branchId);
        var list = await service.ListForFieldAsync(versionId, fieldId, null);

        Assert.Single(list.Entries);
        Assert.Equal("Старий запис", list.Entries[0].Body);
    }

    [Fact]
    public async Task ListForFieldAsync_RequiresReadAccess()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        SeedBranch(context, branchId);
        SeedField(context, versionId, fieldId, null, "FreeText", branchId);
        SeedPermission(context, fieldId, LimsRoles.Registrar, FieldAccessLevel.Write);

        var service = CreateService(context, LimsRoles.LaboratoryTechnician, branchId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ListForFieldAsync(versionId, fieldId, null));
    }

    private static FieldTextLibraryService CreateService(
        ApplicationDbContext context,
        string activeRole,
        Guid branchId)
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
        var activeRoleService = new ActiveLimsRoleService(accessor);
        var permissions = new TemplateFieldPermissionService(context, activeRoleService, accessor);
        var currentUser = new TestCurrentUser(branchId);

        return new FieldTextLibraryService(
            context,
            permissions,
            currentUser,
            new TestDateTimeProvider());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SeedBranch(ApplicationDbContext context, Guid branchId)
    {
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            CreatedAtUtc = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private static void SeedField(
        ApplicationDbContext context,
        Guid versionId,
        Guid fieldId,
        Guid? dataFieldId,
        string tag,
        Guid branchId)
    {
        var templateId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "T1",
            NameUk = "Test",
            CreatedAtUtc = DateTime.UtcNow
        });

        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (dataFieldId.HasValue)
        {
            context.DataFields.Add(new DataField
            {
                Id = dataFieldId.Value,
                Key = tag,
                DisplayNameUk = tag,
                FieldType = DataFieldType.Text,
                Scope = DataFieldScope.Registration,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        context.TemplateFields.Add(new TemplateField
        {
            Id = fieldId,
            TemplateVersionId = versionId,
            Tag = tag,
            NormalizedTag = tag.ToUpperInvariant(),
            DataFieldId = dataFieldId,
            CreatedAtUtc = DateTime.UtcNow
        });

        context.SaveChanges();
    }

    private static void SeedAdditionalField(
        ApplicationDbContext context,
        Guid versionId,
        Guid fieldId,
        Guid? dataFieldId,
        string tag)
    {
        context.TemplateFields.Add(new TemplateField
        {
            Id = fieldId,
            TemplateVersionId = versionId,
            Tag = tag,
            NormalizedTag = tag.ToUpperInvariant(),
            DataFieldId = dataFieldId,
            CreatedAtUtc = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private static void SeedPermission(
        ApplicationDbContext context,
        Guid fieldId,
        string role,
        FieldAccessLevel accessLevel)
    {
        context.TemplateFieldPermissions.Add(new TemplateFieldPermission
        {
            Id = Guid.NewGuid(),
            TemplateFieldId = fieldId,
            RoleName = role,
            AccessLevel = accessLevel,
            CreatedAtUtc = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    private sealed class TestCurrentUser : Application.Abstractions.ICurrentUserService
    {
        public TestCurrentUser(Guid branchId) => BranchId = branchId;

        public string? UserId => "test-user";

        public string? UserName => "test";

        public string? UserFullName => "Test User";

        public Guid? BranchId { get; }

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;

        public bool IsAuthenticated => true;
    }

    private sealed class TestDateTimeProvider : Application.Abstractions.IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
