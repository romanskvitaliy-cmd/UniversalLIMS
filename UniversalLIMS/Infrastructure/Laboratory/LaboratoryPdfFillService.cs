using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
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
}
