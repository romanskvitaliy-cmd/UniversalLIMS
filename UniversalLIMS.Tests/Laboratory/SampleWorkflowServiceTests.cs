using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Laboratory;

namespace UniversalLIMS.Tests.Laboratory;

public sealed class SampleWorkflowServiceTests
{
    private readonly SampleWorkflowService _workflow = new();

    [Fact]
    public void ApplyAfterResultSave_SentToLabDocument_BecomesInProgress_WhenValuesSaved()
    {
        var sampleId = Guid.NewGuid();
        var sample = new Sample
        {
            Id = sampleId,
            Status = SampleStatus.Routed
        };

        var document = new OrderDocument
        {
            Id = Guid.NewGuid(),
            SampleId = sampleId,
            Status = OrderDocumentStatus.SentToLab
        };

        _workflow.ApplyAfterResultSave(
            sample,
            [document],
            markResultsComplete: false,
            hadPersistedChanges: true);

        Assert.Equal(SampleStatus.InProgress, sample.Status);
        Assert.Equal(OrderDocumentStatus.InProgress, document.Status);
    }

    [Fact]
    public void ApplyAfterResultSave_MarkComplete_SetsResultsEntered_OnSampleAndDocument()
    {
        var sampleId = Guid.NewGuid();
        var sample = new Sample
        {
            Id = sampleId,
            Status = SampleStatus.InProgress
        };

        var document = new OrderDocument
        {
            Id = Guid.NewGuid(),
            SampleId = sampleId,
            Status = OrderDocumentStatus.InProgress
        };

        _workflow.ApplyAfterResultSave(
            sample,
            [document],
            markResultsComplete: true,
            hadPersistedChanges: false);

        Assert.Equal(SampleStatus.ResultsEntered, sample.Status);
        Assert.Equal(OrderDocumentStatus.ResultsEntered, document.Status);
    }

    [Fact]
    public void ApplyAfterResultSave_MarkComplete_WithDocumentId_CompletesOnlySelectedDocument()
    {
        var sampleId = Guid.NewGuid();
        var sample = new Sample
        {
            Id = sampleId,
            Status = SampleStatus.InProgress
        };
        var selectedDocumentId = Guid.NewGuid();
        var selectedDocument = new OrderDocument
        {
            Id = selectedDocumentId,
            SampleId = sampleId,
            Status = OrderDocumentStatus.InProgress
        };
        var siblingDocument = new OrderDocument
        {
            Id = Guid.NewGuid(),
            SampleId = sampleId,
            Status = OrderDocumentStatus.SentToLab
        };

        _workflow.ApplyAfterResultSave(
            sample,
            [selectedDocument, siblingDocument],
            markResultsComplete: true,
            hadPersistedChanges: false,
            completedOrderDocumentId: selectedDocumentId);

        Assert.Equal(SampleStatus.InProgress, sample.Status);
        Assert.Equal(OrderDocumentStatus.ResultsEntered, selectedDocument.Status);
        Assert.Equal(OrderDocumentStatus.SentToLab, siblingDocument.Status);
    }

    [Fact]
    public void ApplyAfterResultSave_NoChanges_LeavesDocumentSentToLab()
    {
        var sampleId = Guid.NewGuid();
        var sample = new Sample { Id = sampleId, Status = SampleStatus.Routed };
        var document = new OrderDocument
        {
            Id = Guid.NewGuid(),
            SampleId = sampleId,
            Status = OrderDocumentStatus.SentToLab
        };

        _workflow.ApplyAfterResultSave(
            sample,
            [document],
            markResultsComplete: false,
            hadPersistedChanges: false);

        Assert.Equal(SampleStatus.Routed, sample.Status);
        Assert.Equal(OrderDocumentStatus.SentToLab, document.Status);
    }
}
