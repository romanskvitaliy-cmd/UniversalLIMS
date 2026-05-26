namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ILaboratoryOverviewService
{
    Task<LaboratoryOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
