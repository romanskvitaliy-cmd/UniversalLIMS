using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Laboratory.Dtos;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Persistence.Queries;

/// <summary>
/// EF Core query object for laboratory journal read models.
/// </summary>
public sealed class LaboratoryJournalQuery : ILaboratoryJournalQuery
{
    private readonly ApplicationDbContext _context;

    public LaboratoryJournalQuery(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<LaboratoryJournalItemDto> Items, int TotalCount)> SearchAsync(
        LaboratoryJournalFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var query = BuildJournalQuery(filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(sample => sample.RegisteredAt)
            .ThenBy(sample => sample.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sample => new LaboratoryJournalItemDto
            {
                SampleId = sample.Id,
                SampleNumber = sample.Number,
                ReceivedAt = sample.RegisteredAt,
                InvestigationTypeId = sample.InvestigationTypeId,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                Status = sample.Status,
                ReferralNumber = sample.Order.ReferralNumber,
                CustomerFullName = sample.Order.Customer.FullName,
                TargetBranchName = sample.OrderDocuments
                    .OrderBy(document => document.CreatedAtUtc)
                    .Select(document => document.TargetBranch.Name)
                    .FirstOrDefault() ?? string.Empty,
                EnteredResultsCount = sample.ResultValues.Count,
                RequiredResultsCount = sample.OrderDocuments
                    .SelectMany(document => document.TemplateVersion.Fields)
                    .Where(field => field.DataFieldId != null
                        && field.IsRequired
                        && field.DataField!.Scope == DataFieldScope.Result
                        && field.DataField.IsActive)
                    .Select(field => field.DataFieldId!.Value)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public Task<SampleLaboratoryDetailsDto?> GetSampleHeaderAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        return _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId)
            .Select(sample => new SampleLaboratoryDetailsDto
            {
                SampleId = sample.Id,
                SampleNumber = sample.Number,
                ReceivedAt = sample.RegisteredAt,
                RoutedAtUtc = sample.RoutedAtUtc,
                Status = sample.Status,
                Notes = sample.Notes,
                InvestigationTypeId = sample.InvestigationTypeId,
                InvestigationTypeName = sample.InvestigationType.NameUk,
                OrderId = sample.OrderId,
                ReferralNumber = sample.Order.ReferralNumber,
                CustomerFullName = sample.Order.Customer.FullName,
                CustomerOrganizationName = sample.Order.Customer.OrganizationName,
                TargetBranchName = sample.OrderDocuments
                    .OrderBy(document => document.CreatedAtUtc)
                    .Select(document => document.TargetBranch.Name)
                    .FirstOrDefault() ?? string.Empty,
                EnteredResultsCount = sample.ResultValues.Count,
                RequiredResultsCount = sample.OrderDocuments
                    .SelectMany(document => document.TemplateVersion.Fields)
                    .Where(field => field.DataFieldId != null
                        && field.IsRequired
                        && field.DataField!.Scope == DataFieldScope.Result
                        && field.DataField.IsActive)
                    .Select(field => field.DataFieldId!.Value)
                    .Distinct()
                    .Count()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetRequiredResultDataFieldIdsAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OrderDocuments
            .AsNoTracking()
            .Where(document => document.SampleId == sampleId)
            .SelectMany(document => document.TemplateVersion.Fields)
            .Where(field => field.DataFieldId != null
                && field.IsRequired
                && field.DataField!.Scope == DataFieldScope.Result
                && field.DataField.IsActive)
            .Select(field => field.DataFieldId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> IsLaboratorySampleAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        return _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId)
            .AnyAsync(
                sample => sample.Status != SampleStatus.Registered
                    && sample.OrderDocuments.Any(document =>
                        document.TemplateVersion.Fields.Any(field =>
                            field.DataFieldId != null
                            && field.DataField!.Scope == DataFieldScope.Result
                            && field.DataField.IsActive)),
                cancellationToken);
    }

    private IQueryable<Domain.Registration.Sample> BuildJournalQuery(LaboratoryJournalFilter filter)
    {
        var query = _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Status != SampleStatus.Registered)
            .Where(sample => sample.OrderDocuments.Any(document =>
                document.TemplateVersion.Fields.Any(field =>
                    field.DataFieldId != null
                    && field.DataField!.Scope == DataFieldScope.Result
                    && field.DataField.IsActive)));

        if (filter.ReceivedDateFrom.HasValue)
        {
            var fromDate = filter.ReceivedDateFrom.Value.Date;
            query = query.Where(sample => sample.RegisteredAt >= fromDate);
        }

        if (filter.ReceivedDateTo.HasValue)
        {
            var toExclusive = filter.ReceivedDateTo.Value.Date.AddDays(1);
            query = query.Where(sample => sample.RegisteredAt < toExclusive);
        }

        if (filter.InvestigationTypeId.HasValue)
        {
            query = query.Where(sample => sample.InvestigationTypeId == filter.InvestigationTypeId.Value);
        }

        if (filter.SampleStatus.HasValue)
        {
            query = query.Where(sample => sample.Status == filter.SampleStatus.Value);
        }

        if (filter.TargetBranchId.HasValue)
        {
            var branchId = filter.TargetBranchId.Value;
            query = query.Where(sample => sample.OrderDocuments.Any(document => document.TargetBranchId == branchId));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var pattern = $"%{filter.SearchText.Trim()}%";
            query = query.Where(sample =>
                EF.Functions.Like(sample.Number, pattern)
                || EF.Functions.Like(sample.Order.ReferralNumber ?? string.Empty, pattern)
                || EF.Functions.Like(sample.Order.Customer.FullName, pattern)
                || EF.Functions.Like(sample.Order.Customer.OrganizationName ?? string.Empty, pattern));
        }

        return query;
    }
}
