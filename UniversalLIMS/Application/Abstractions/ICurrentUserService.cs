namespace UniversalLIMS.Application.Abstractions;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? UserName { get; }

    string? UserFullName { get; }

    Guid? BranchId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? CorrelationId { get; }

    bool IsAuthenticated { get; }
}
