using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Registration.Abstractions;

public interface IReferralPdfGenerator
{
    /// <param name="purposeFilter">
    /// Якщо задано — лише документи з відповідним <see cref="Template.Purpose"/> (D2/D3).
    /// </param>
    Task<byte[]> GenerateAsync(
        Guid orderId,
        TemplatePurpose? purposeFilter = null,
        CancellationToken cancellationToken = default);
}
