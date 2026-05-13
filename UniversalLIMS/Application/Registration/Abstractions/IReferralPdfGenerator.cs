namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IReferralPdfGenerator
{
    Task<byte[]> GenerateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
