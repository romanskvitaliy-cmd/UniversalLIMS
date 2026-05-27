using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class SampleWorkflowService : ISampleWorkflowService
{
    public void ApplyAfterResultSave(
        Sample sample,
        IEnumerable<OrderDocument> sampleDocuments,
        bool markResultsComplete,
        bool hadPersistedChanges,
        Guid? completedOrderDocumentId = null)
    {
        if (!hadPersistedChanges && !markResultsComplete)
        {
            return;
        }

        var documents = sampleDocuments
            .Where(document => !document.IsAnnulled)
            .ToList();

        if (sample.Status is SampleStatus.Registered or SampleStatus.Routed)
        {
            sample.Status = SampleStatus.InProgress;
        }

        foreach (var document in documents)
        {
            if (markResultsComplete)
            {
                var shouldCompleteDocument = completedOrderDocumentId is null
                    || document.Id == completedOrderDocumentId.Value;

                if (shouldCompleteDocument
                    && document.Status is OrderDocumentStatus.SentToLab or OrderDocumentStatus.InProgress)
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

        if (markResultsComplete && IsAllLabWorkflowComplete(documents))
        {
            sample.Status = SampleStatus.ResultsEntered;
        }
    }

    private static bool IsAllLabWorkflowComplete(IReadOnlyCollection<OrderDocument> documents)
    {
        var labDocuments = documents
            .Where(document => document.Status != OrderDocumentStatus.Pending)
            .ToList();

        return labDocuments.Count > 0
            && labDocuments.All(document => document.Status == OrderDocumentStatus.ResultsEntered);
    }
}
