using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Laboratory;

public sealed class LaboratoryDocumentSubmissionService : ILaboratoryDocumentSubmissionService
{
    private static readonly OrderDocumentStatus[] SendableDocumentStatuses =
    [
        OrderDocumentStatus.InProgress
    ];

    private readonly ApplicationDbContext _context;
    private readonly ILaboratoryBranchContext _laboratoryBranchContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUser;

    public LaboratoryDocumentSubmissionService(
        ApplicationDbContext context,
        ILaboratoryBranchContext laboratoryBranchContext,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUser)
    {
        _context = context;
        _laboratoryBranchContext = laboratoryBranchContext;
        _dateTimeProvider = dateTimeProvider;
        _currentUser = currentUser;
    }

    public async Task<SendDocumentToExpertResult> SendDocumentToExpertAsync(
        Guid orderDocumentId,
        CancellationToken cancellationToken = default)
    {
        var branchContext = await _laboratoryBranchContext.GetStateAsync(cancellationToken);

        var document = await _context.OrderDocuments
            .Include(item => item.Sample)
            .ThenInclude(sample => sample.Order)
            .Include(item => item.Sample)
            .ThenInclude(sample => sample.OrderDocuments)
            .FirstOrDefaultAsync(
                item => item.Id == orderDocumentId && !item.IsAnnulled,
                cancellationToken)
            ?? throw new InvalidOperationException("PDF-документ не знайдено.");

        if (branchContext.ActiveBranchId is Guid branchId
            && document.TargetBranchId != branchId)
        {
            throw new InvalidOperationException("Документ не належить обраній лабораторії.");
        }

        if (!SendableDocumentStatuses.Contains(document.Status))
        {
            throw new InvalidOperationException(
                document.Status == OrderDocumentStatus.ResultsEntered
                    ? "Документ вже відправлено експерту."
                    : "Відправити експерту можна лише після збереження PDF (статус «В роботі»).");
        }

        var now = _dateTimeProvider.UtcNow;
        var userId = _currentUser.UserId;

        document.Status = OrderDocumentStatus.ResultsEntered;
        document.ResultsEnteredAtUtc = now;
        document.UpdatedAtUtc = now;
        document.UpdatedByUserId = userId;

        var sample = document.Sample;
        if (sample.IsAnnulled || sample.Order.IsAnnulled)
        {
            throw new InvalidOperationException("Проба або замовлення анульовано.");
        }

        var activeDocuments = sample.OrderDocuments
            .Where(item => !item.IsAnnulled && item.Status != OrderDocumentStatus.Pending)
            .ToList();

        var sampleReadyForExpert = activeDocuments.Count > 0
            && activeDocuments.All(item => item.Status == OrderDocumentStatus.ResultsEntered);

        if (sampleReadyForExpert)
        {
            sample.Status = SampleStatus.ResultsEntered;
            sample.ResultsEnteredAtUtc = now;
        }
        else if (sample.Status is SampleStatus.Registered or SampleStatus.Routed)
        {
            sample.Status = SampleStatus.InProgress;
        }

        sample.UpdatedAtUtc = now;
        sample.UpdatedByUserId = userId;

        await _context.SaveChangesAsync(cancellationToken);

        var message = sampleReadyForExpert
            ? "Документ відправлено. Усі шаблони проби готові — проба з’явиться в черзі експерта."
            : "Документ відправлено експерту. Заповніть і відправте решту шаблонів цієї проби.";

        return new SendDocumentToExpertResult
        {
            OrderDocumentId = document.Id,
            SampleId = sample.Id,
            DocumentStatus = document.Status,
            SampleStatus = sample.Status,
            SampleReadyForExpert = sampleReadyForExpert,
            Message = message
        };
    }
}
