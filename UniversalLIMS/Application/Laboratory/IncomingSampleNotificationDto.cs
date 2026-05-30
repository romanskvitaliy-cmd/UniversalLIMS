namespace UniversalLIMS.Application.Laboratory;

/// <summary>Коротке сповіщення про пробу, нещодавно направлену з реєстратури в лабораторію.</summary>
public sealed class IncomingSampleNotificationDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string CustomerFullName { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string TargetBranchName { get; init; }

    public DateTime RoutedAtUtc { get; init; }
}
