using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Security;

namespace UniversalLIMS.Tests.Security;

public sealed class ActiveLimsRoleServiceTests
{
    [Fact]
    public void ResolveActiveRole_WhenGuest_ClearsPortalSessionAndReturnsNull()
    {
        var session = new TestSession();
        session.SetString(SessionKeys.ActiveLimsRole, LimsRoles.Registrar);
        session.SetString(SessionKeys.ActiveLaboratoryBranchId, Guid.NewGuid().ToString("D"));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
            Session = session
        };

        var service = new ActiveLimsRoleService(new HttpContextAccessor { HttpContext = httpContext });

        var result = service.ResolveActiveRole(httpContext.User);

        Assert.Null(result);
        Assert.Null(session.GetString(SessionKeys.ActiveLimsRole));
        Assert.Null(session.GetString(SessionKeys.ActiveLaboratoryBranchId));
    }

    [Fact]
    public void GetActiveRole_WhenGuest_ClearsStaleRoleFromSession()
    {
        var session = new TestSession();
        session.SetString(SessionKeys.ActiveLimsRole, LimsRoles.Registrar);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
            Session = session
        };

        var service = new ActiveLimsRoleService(new HttpContextAccessor { HttpContext = httpContext });

        Assert.Null(service.GetActiveRole());
        Assert.Null(session.GetString(SessionKeys.ActiveLimsRole));
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
