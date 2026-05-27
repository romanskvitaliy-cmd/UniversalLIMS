using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ISampleWorkflowService
{
    /// <param name="sampleDocuments">Документи проби (той самий <see cref="Sample.Id"/>).</param>
    /// <param name="completedOrderDocumentId">Документ, який позначають як завершений. Null зберігає legacy sample-level complete.</param>
    /// <param name="hadPersistedChanges">Чи були збережені/анульовані значення результатів.</param>
    void ApplyAfterResultSave(
        Sample sample,
        IEnumerable<OrderDocument> sampleDocuments,
        bool markResultsComplete,
        bool hadPersistedChanges,
        Guid? completedOrderDocumentId = null);
}
