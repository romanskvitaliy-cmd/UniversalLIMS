namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IRegistrationNotificationService
{
    Task<IReadOnlyList<IncomingRegistrarNotificationDto>> GetReadyForPickupSinceAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}
