using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Expert;

public sealed class ExpertPdfFillService : IExpertPdfFillService
{
    private static readonly OrderDocumentStatus[] ExpertFillStatuses =
    [
        OrderDocumentStatus.ResultsEntered
    ];

    private readonly ApplicationDbContext _context;

    public ExpertPdfFillService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SamplePdfFillTargetDto>> GetFillTargetsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var sample = await _context.Samples
            .AsNoTracking()
            .Where(item => item.Id == sampleId && !item.IsAnnulled && !item.Order.IsAnnulled)
            .Select(item => new
            {
                item.Id,
                item.OrderId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sample is null)
        {
            return [];
        }

        return await _context.OrderDocuments
            .AsNoTracking()
            .Where(document =>
                document.SampleId == sampleId
                && !document.IsAnnulled
                && ExpertFillStatuses.Contains(document.Status))
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
    }
}
