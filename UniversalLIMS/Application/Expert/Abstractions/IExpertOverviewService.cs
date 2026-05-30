namespace UniversalLIMS.Application.Expert.Abstractions;

public interface IExpertOverviewService
{
    Task<ExpertOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
