using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class SampleWorkflowService : ISampleWorkflowService
{
    public void ApplyAfterResultSave(Sample sample, bool markResultsComplete)
    {
        if (sample.Status is SampleStatus.Registered or SampleStatus.Routed)
        {
            sample.Status = SampleStatus.InProgress;
        }

        if (markResultsComplete)
        {
            sample.Status = SampleStatus.ResultsEntered;
        }
    }
}
