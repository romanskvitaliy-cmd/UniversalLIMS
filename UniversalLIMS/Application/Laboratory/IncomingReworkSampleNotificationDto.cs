namespace UniversalLIMS.Application.Laboratory;

/// <summary>Сповіщення лаборанту: експерт повернув пробу на доопрацювання.</summary>
public sealed class IncomingReworkSampleNotificationDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string CustomerFullName { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string TargetBranchName { get; init; }

    public required string ReworkReasonUk { get; init; }

    public DateTime ReturnedForReworkAtUtc { get; init; }
}
