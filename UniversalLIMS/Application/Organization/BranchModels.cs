namespace UniversalLIMS.Application.Organization;

public sealed class BranchListItemDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? Address { get; init; }

    public bool IsActive { get; init; }

    public int WorkflowDocumentCount { get; init; }

    public int PendingDocumentCount { get; init; }

    public int AssignedUserCount { get; init; }
}

public sealed class BranchEditDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string? Address { get; init; }

    public bool IsActive { get; init; }
}

public sealed class CreateBranchRequest
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string City { get; init; }

    public string? Address { get; init; }
}

public sealed class UpdateBranchRequest
{
    public required string Name { get; init; }

    public required string City { get; init; }

    public string? Address { get; init; }

    public bool IsActive { get; init; }
}
