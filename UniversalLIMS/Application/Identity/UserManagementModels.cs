namespace UniversalLIMS.Application.Identity;

public sealed class UserListItemDto
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public string? Position { get; init; }

    public Guid? BranchId { get; init; }

    public string? BranchCode { get; init; }

    public string? BranchName { get; init; }

    public bool IsActive { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    public bool IsBranchPortalAccount { get; init; }
}

public sealed class UserEditDto
{
    public required string Id { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public string? Position { get; init; }

    public Guid? BranchId { get; init; }

    public bool IsActive { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    public bool IsBranchPortalAccount { get; init; }
}

public sealed class BranchPortalAccountDto
{
    public required string UserId { get; init; }

    public required string Email { get; init; }

    public required string FullName { get; init; }

    public bool IsActive { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }
}

public sealed class CreateUserRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string FullName { get; init; }

    public string? Position { get; init; }

    public Guid? BranchId { get; init; }

    public bool IsActive { get; init; } = true;

    public required IReadOnlyList<string> Roles { get; init; }
}

public sealed class UpdateUserRequest
{
    public required string Email { get; init; }

    public required string FullName { get; init; }

    public string? Position { get; init; }

    public Guid? BranchId { get; init; }

    public bool IsActive { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    public string? NewPassword { get; init; }
}

public sealed class UserPasswordRevealDto
{
    public bool CanReveal { get; init; }

    public string? Password { get; init; }

    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class UserListQuery
{
    public string? Search { get; init; }

    public Guid? BranchId { get; init; }

    public string? Role { get; init; }

    public bool IncludeInactive { get; init; }
}
