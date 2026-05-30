namespace UniversalLIMS.Application.Registration;

/// <summary>Сповіщення реєстратору: проба готова до видачі після затвердження експертом.</summary>
public sealed class IncomingRegistrarNotificationDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string CustomerFullName { get; init; }

    public required string InvestigationTypeName { get; init; }

    public DateTime ReadyForPickupAtUtc { get; init; }
}
