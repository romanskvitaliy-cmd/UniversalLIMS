using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ISampleWorkflowService
{
    void ApplyAfterResultSave(Sample sample, bool markResultsComplete);
}
