using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryPdfFillService : ILaboratoryPdfFillService
{
    private static readonly OrderDocumentStatus[] LabFillStatuses =
    [
        OrderDocumentStatus.SentToLab,
        OrderDocumentStatus.InProgress
    ];

    private static readonly OrderDocumentStatus[] LabWorkflowStatuses =
    [
        OrderDocumentStatus.SentToLab,
        OrderDocumentStatus.InProgress,
        OrderDocumentStatus.ResultsEntered
    ];

    private readonly ApplicationDbContext _context;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;

    public LaboratoryPdfFillService(
        ApplicationDbContext context,
        ILaboratoryBranchContext laboratoryBranchContext)
    {
        _context = context;
        _laboratoryBranchContext = laboratoryBranchContext;
    }

    public async Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);
        var sampleQuery = _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled && !sample.Order.IsAnnulled);

        if (branchContext.ActiveBranchId is Guid branchId)
        {
            sampleQuery = sampleQuery.Where(sample => sample.OrderDocuments.Any(document =>
                !document.IsAnnulled
                && document.TargetBranchId == branchId
                && LabFillStatuses.Contains(document.Status)));
        }

        var sample = await sampleQuery
            .Select(sample => new
            {
                sample.Id,
                sample.OrderId,
                sample.InvestigationTypeId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sample is null)
        {
            return [];
        }

        var documents = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document =>
                document.SampleId == sampleId
                && !document.IsAnnulled
                && LabFillStatuses.Contains(document.Status)
                && (!branchContext.ActiveBranchId.HasValue
                    || document.TargetBranchId == branchContext.ActiveBranchId.Value))
            .Join(
                _context.TemplateVersions.AsNoTracking(),
                document => document.TemplateVersionId,
                version => version.Id,
                (document, version) => new { document, version })
            .Join(
                _context.Templates.AsNoTracking(),
                row => row.version.TemplateId,
                template => template.Id,
                (row, template) => new SamplePdfFillTargetDto
                {
                    SampleId = sample.Id,
                    OrderId = sample.OrderId,
                    TemplateVersionId = row.document.TemplateVersionId,
                    OrderDocumentId = row.document.Id,
                    TemplateNameUk = template.NameUk,
                    VersionNumber = row.version.VersionNumber,
                    DocumentStatus = row.document.Status
                })
            .OrderBy(target => target.TemplateNameUk)
            .ThenBy(target => target.VersionNumber)
            .ToListAsync(cancellationToken);

        return documents;
    }

    public async Task<LaboratorySampleDetailsDto?> GetSampleDetailsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);

        var sampleQuery = _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled && !sample.Order.IsAnnulled);

        if (branchContext.ActiveBranchId is Guid branchId)
        {
            sampleQuery = sampleQuery.Where(sample => sample.OrderDocuments.Any(document =>
                !document.IsAnnulled
                && LabWorkflowStatuses.Contains(document.Status)
                && document.TargetBranchId == branchId));
        }

        var sample = await sampleQuery
            .Select(sample => new
            {
                sample.Id,
                sample.Number,
                sample.Status,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                CustomerFullName = sample.Order.Customer.FullName,
                ReferralNumber = sample.Order.ReferralNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sample is null)
        {
            return null;
        }

        var documents = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document =>
                document.SampleId == sampleId
                && !document.IsAnnulled
                && LabWorkflowStatuses.Contains(document.Status)
                && (!branchContext.ActiveBranchId.HasValue
                    || document.TargetBranchId == branchContext.ActiveBranchId.Value))
            .Join(
                _context.TemplateVersions.AsNoTracking(),
                document => document.TemplateVersionId,
                version => version.Id,
                (document, version) => new { document, version })
            .Join(
                _context.Templates.AsNoTracking(),
                row => row.version.TemplateId,
                template => template.Id,
                (row, template) => new
                {
                    row.document,
                    row.version,
                    template
                })
            .Join(
                _context.Branches.AsNoTracking(),
                row => row.document.TargetBranchId,
                branch => branch.Id,
                (row, branch) => new LaboratorySampleDocumentItemDto
                {
                    SampleId = sampleId,
                    OrderId = row.document.OrderId,
                    TemplateVersionId = row.document.TemplateVersionId,
                    OrderDocumentId = row.document.Id,
                    TemplateNameUk = row.template.NameUk,
                    VersionNumber = row.version.VersionNumber,
                    DocumentStatus = row.document.Status,
                    TargetBranchName = branch.Name,
                    CanFill = LabFillStatuses.Contains(row.document.Status),
                    CanSendToExpert = row.document.Status == OrderDocumentStatus.InProgress
                })
            .OrderBy(item => item.TemplateNameUk)
            .ThenBy(item => item.VersionNumber)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return null;
        }

        return new LaboratorySampleDetailsDto
        {
            SampleId = sample.Id,
            SampleNumber = sample.Number,
            InvestigationTypeName = sample.InvestigationTypeName,
            CustomerFullName = sample.CustomerFullName,
            ReferralNumber = sample.ReferralNumber,
            SampleStatus = sample.Status,
            WorkflowSummaryUk = OrderDocumentStatusDisplay.SummarizeWorkflow(
                documents.Select(document => document.DocumentStatus).ToList()),
            Documents = documents
        };
    }
}
