using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class SampleWorkflowService : ISampleWorkflowService
{
    public void ApplyAfterResultSave(
        Sample sample,
        IEnumerable<OrderDocument> sampleDocuments,
        bool markResultsComplete,
        bool hadPersistedChanges)
    {
        if (!hadPersistedChanges && !markResultsComplete)
        {
            return;
        }

        if (sample.Status is SampleStatus.Registered or SampleStatus.Routed)
        {
            sample.Status = SampleStatus.InProgress;
        }

        if (markResultsComplete)
        {
            sample.Status = SampleStatus.ResultsEntered;
        }

        foreach (var document in sampleDocuments.Where(document => !document.IsAnnulled))
        {
            if (markResultsComplete)
            {
                if (document.Status is OrderDocumentStatus.SentToLab or OrderDocumentStatus.InProgress)
                {
                    document.Status = OrderDocumentStatus.ResultsEntered;
                }

                continue;
            }

            if (hadPersistedChanges && document.Status == OrderDocumentStatus.SentToLab)
            {
                document.Status = OrderDocumentStatus.InProgress;
            }
        }
    }
}
