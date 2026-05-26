using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
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
    private readonly ICurrentUserService _currentUser;

    public LaboratoryPdfFillService(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var sampleQuery = _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled && !sample.Order.IsAnnulled);

        if (_currentUser.BranchId is Guid branchId)
        {
            sampleQuery = sampleQuery.Where(sample => sample.Order.BranchId == branchId);
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
                && LabFillStatuses.Contains(document.Status))
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

        if (documents.Count > 0)
        {
            return documents;
        }

        var fallback = await (
                from link in _context.InvestigationTypeTemplates.AsNoTracking()
                join version in _context.TemplateVersions.AsNoTracking()
                    on link.TemplateId equals version.TemplateId
                join template in _context.Templates.AsNoTracking()
                    on version.TemplateId equals template.Id
                where link.InvestigationTypeId == sample.InvestigationTypeId
                      && link.IsActive
                      && version.Status == TemplateVersionStatus.Published
                      && !version.IsAnnulled
                      && version.DocumentFormat == TemplateDocumentFormat.Pdf
                orderby version.VersionNumber descending
                select new SamplePdfFillTargetDto
                {
                    SampleId = sample.Id,
                    OrderId = sample.OrderId,
                    TemplateVersionId = version.Id,
                    OrderDocumentId = null,
                    TemplateNameUk = template.NameUk,
                    VersionNumber = version.VersionNumber,
                    DocumentStatus = null
                })
            .FirstOrDefaultAsync(cancellationToken);

        return fallback is null ? [] : [fallback];
    }
}
