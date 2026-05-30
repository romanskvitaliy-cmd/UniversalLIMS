using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UniversalLIMS.Application.Identity;
using UniversalLIMS.Application.Identity.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Infrastructure.Identity;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Tests.Identity;

public sealed class UserManagementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesBranchPortalUserWithRole()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = await CreateServiceAsync(context);
        var userId = await service.CreateAsync(new CreateUserRequest
        {
            Email = BranchPortalAccountConventions.BuildEmail("ZHY"),
            Password = BranchPortalAccountConventions.BuildDefaultPassword("ZHY"),
            FullName = BranchPortalAccountConventions.BuildFullName("Житомир"),
            BranchId = branchId,
            Roles = [LimsRoles.Registrar]
        });

        var user = await context.Users.SingleAsync(item => item.Id == userId);
        Assert.Equal(branchId, user.BranchId);
        Assert.True(user.IsActive);

        var userManager = CreateUserManager(context);
        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(LimsRoles.Registrar, roles);
    }

    [Fact]
    public async Task UpdateAsync_RejectsRemovingLastAdministrator()
    {
        await using var context = CreateContext();
        var service = await CreateServiceAsync(context);

        var admin = new ApplicationUser
        {
            Email = "only-admin@test.local",
            UserName = "only-admin@test.local",
            FullName = "Only Admin",
            EmailConfirmed = true,
            IsActive = true
        };

        var userManager = CreateUserManager(context);
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, LimsRoles.SystemAdministrator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                admin.Id,
                new UpdateUserRequest
                {
                    Email = admin.Email!,
                    FullName = admin.FullName,
                    IsActive = false,
                    Roles = [LimsRoles.SystemAdministrator],
                    BranchId = null
                }));
    }

    [Fact]
    public async Task GetBranchPortalAccountsAsync_ReturnsAccountPerBranch()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "BER",
            Name = "Бердичів",
            City = "Бердичів",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = await CreateServiceAsync(context);
        await service.CreateAsync(new CreateUserRequest
        {
            Email = BranchPortalAccountConventions.BuildEmail("BER"),
            Password = BranchPortalAccountConventions.BuildDefaultPassword("BER"),
            FullName = BranchPortalAccountConventions.BuildFullName("Бердичів"),
            BranchId = branchId,
            Roles = [LimsRoles.Registrar]
        });

        var accounts = await service.GetBranchPortalAccountsAsync();
        Assert.True(accounts.TryGetValue(branchId, out var account));
        Assert.NotNull(account);
        Assert.Equal(BranchPortalAccountConventions.BuildEmail("BER"), account!.Email);
    }

    [Fact]
    public async Task GetRevealablePasswordAsync_ReturnsBranchDefaultPassword()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = await CreateServiceAsync(context);
        var userId = await service.CreateAsync(new CreateUserRequest
        {
            Email = BranchPortalAccountConventions.BuildEmail("ZHY"),
            Password = BranchPortalAccountConventions.BuildDefaultPassword("ZHY"),
            FullName = BranchPortalAccountConventions.BuildFullName("Житомир"),
            BranchId = branchId,
            Roles = [LimsRoles.Registrar]
        });

        var reveal = await service.GetRevealablePasswordAsync(userId);
        Assert.True(reveal.CanReveal);
        Assert.Equal(BranchPortalAccountConventions.BuildDefaultPassword("ZHY"), reveal.Password);
    }

    [Fact]
    public async Task GetRevealablePasswordAsync_ReturnsUnavailableWhenPasswordChanged()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = await CreateServiceAsync(context);
        var userId = await service.CreateAsync(new CreateUserRequest
        {
            Email = BranchPortalAccountConventions.BuildEmail("ZHY"),
            Password = "CustomPass9!",
            FullName = BranchPortalAccountConventions.BuildFullName("Житомир"),
            BranchId = branchId,
            Roles = [LimsRoles.Registrar]
        });

        var reveal = await service.GetRevealablePasswordAsync(userId);
        Assert.False(reveal.CanReveal);
        Assert.Null(reveal.Password);
    }

    private static async Task<UserManagementService> CreateServiceAsync(ApplicationDbContext context)
    {
        foreach (var role in LimsRoles.All)
        {
            var roleManager = CreateRoleManager(context);
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        return new UserManagementService(context, CreateUserManager(context), CreateRoleManager(context));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
    {
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser>(context);
        return new UserManager<ApplicationUser>(
            store,
            null!,
            new PasswordHasher<ApplicationUser>(),
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext context)
    {
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(context);
        return new RoleManager<IdentityRole>(store, null!, null!, null!, null!);
    }
}
