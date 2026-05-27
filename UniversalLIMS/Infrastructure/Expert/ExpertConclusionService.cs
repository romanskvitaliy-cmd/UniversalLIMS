using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Expert;

public sealed class ExpertConclusionService : IExpertConclusionService
{
    private const int ExpertNotesMaxLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ExpertConclusionService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<ExpertConclusionReview?> GetReviewAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default) =>
        _context.ExpertConclusionReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(review => review.SampleId == sampleId, cancellationToken);

    public async Task MarkInProgressAsync(Guid sampleId, CancellationToken cancellationToken = default)
    {
        if (!await IsSampleReadyForExpertAsync(sampleId, cancellationToken))
        {
            return;
        }

        var review = await _context.ExpertConclusionReviews
            .FirstOrDefaultAsync(item => item.SampleId == sampleId, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        if (review is null)
        {
            review = new ExpertConclusionReview
            {
                SampleId = sampleId,
                Status = ExpertConclusionStatus.InProgress,
                ReviewStartedAtUtc = now,
                CreatedAtUtc = now,
                CreatedByUserId = _currentUser.UserId
            };
            _context.ExpertConclusionReviews.Add(review);
        }
        else if (review.Status == ExpertConclusionStatus.PendingReview)
        {
            review.Status = ExpertConclusionStatus.InProgress;
            review.ReviewStartedAtUtc ??= now;
            review.UpdatedAtUtc = now;
            review.UpdatedByUserId = _currentUser.UserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ReturnToPendingReviewAsync(
        Guid sampleId,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.ExpertConclusionReviews
            .FirstOrDefaultAsync(item => item.SampleId == sampleId, cancellationToken);

        if (review is null || review.Status == ExpertConclusionStatus.Approved)
        {
            return false;
        }

        if (review.Status == ExpertConclusionStatus.PendingReview)
        {
            return true;
        }

        review.Status = ExpertConclusionStatus.PendingReview;
        review.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        review.UpdatedByUserId = _currentUser.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAsync(
        Guid sampleId,
        string? notesUk,
        CancellationToken cancellationToken = default)
    {
        if (!await IsSampleReadyForExpertAsync(sampleId, cancellationToken))
        {
            return false;
        }

        var review = await _context.ExpertConclusionReviews
            .FirstOrDefaultAsync(item => item.SampleId == sampleId, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var userId = _currentUser.UserId;
        var normalizedNotes = NormalizeExpertNotes(notesUk);

        if (review is null)
        {
            review = new ExpertConclusionReview
            {
                SampleId = sampleId,
                Status = ExpertConclusionStatus.Approved,
                ReviewStartedAtUtc = now,
                ApprovedAtUtc = now,
                ApprovedByUserId = userId,
                NotesUk = normalizedNotes,
                CreatedAtUtc = now,
                CreatedByUserId = userId
            };
            _context.ExpertConclusionReviews.Add(review);
        }
        else
        {
            review.Status = ExpertConclusionStatus.Approved;
            review.ReviewStartedAtUtc ??= now;
            review.ApprovedAtUtc = now;
            review.ApprovedByUserId = userId;
            review.NotesUk = normalizedNotes;

            review.UpdatedAtUtc = now;
            review.UpdatedByUserId = userId;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> IsSampleReadyForExpertAsync(Guid sampleId, CancellationToken cancellationToken) =>
        await _context.Samples
            .AsNoTracking()
            .Where(sample =>
                sample.Id == sampleId
                && !sample.IsAnnulled
                && !sample.Order.IsAnnulled
                && sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending)
                && !sample.OrderDocuments.Any(document =>
                    !document.IsAnnulled
                    && document.Status != OrderDocumentStatus.Pending
                    && document.Status != OrderDocumentStatus.ResultsEntered))
            .AnyAsync(cancellationToken);

    private static string? NormalizeExpertNotes(string? notesUk)
    {
        if (string.IsNullOrWhiteSpace(notesUk))
        {
            return null;
        }

        var trimmed = notesUk.Trim();
        return trimmed.Length <= ExpertNotesMaxLength
            ? trimmed
            : trimmed[..ExpertNotesMaxLength];
    }
}
