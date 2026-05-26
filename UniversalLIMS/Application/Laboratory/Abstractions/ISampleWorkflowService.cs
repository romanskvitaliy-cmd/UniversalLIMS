using UniversalLIMS.Domain.Registration;

namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface ISampleWorkflowService
{
    /// <param name="sampleDocuments">Документи проби (той самий <see cref="Sample.Id"/>).</param>
    /// <param name="hadPersistedChanges">Чи були збережені/анульовані значення результатів.</param>
    void ApplyAfterResultSave(
        Sample sample,
        IEnumerable<OrderDocument> sampleDocuments,
        bool markResultsComplete,
        bool hadPersistedChanges);
}
