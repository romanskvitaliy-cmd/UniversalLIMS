using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Expert;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ApproveConclusions)]
[RequireActiveLimsRole]
public sealed class ExpertController : Controller
{
    private const int ExpertNotesMaxLength = 2000;

    private readonly IExpertReviewQueueService _queue;
    private readonly IExpertPdfFillService _pdfFill;
    private readonly IExpertConclusionService _conclusion;
    private readonly ApplicationDbContext _context;

    public ExpertController(
        IExpertReviewQueueService queue,
        IExpertPdfFillService pdfFill,
        IExpertConclusionService conclusion,
        ApplicationDbContext context)
    {
        _queue = queue;
        _pdfFill = pdfFill;
        _conclusion = conclusion;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] ExpertReviewQueueFilter filter,
        CancellationToken cancellationToken)
    {
        filter.SampleNumber = string.IsNullOrWhiteSpace(filter.SampleNumber) ? null : filter.SampleNumber.Trim();
        filter.NotesContainsUk = string.IsNullOrWhiteSpace(filter.NotesContainsUk) ? null : filter.NotesContainsUk.Trim();
        var result = await _queue.GetQueueAsync(filter, cancellationToken);
        return View(new ExpertIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    /// <summary>Відкриває PDF Workspace для полів висновку (Conclusion scope) на бланку.</summary>
    [HttpGet]
    public async Task<IActionResult> Review(Guid sampleId, CancellationToken cancellationToken)
    {
        var sampleMeta = await _context.Samples
            .AsNoTracking()
            .Where(sample => sample.Id == sampleId && !sample.IsAnnulled)
            .Select(sample => new
            {
                sample.Number,
                sample.InvestigationType.NameUk
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleMeta is null)
        {
            return NotFound();
        }

        await _conclusion.MarkInProgressAsync(sampleId, cancellationToken);

        var targets = await _pdfFill.GetFillTargetsAsync(sampleId, cancellationToken);
        if (targets.Count == 0)
        {
            TempData["ExpertWarning"] =
                "Для цієї проби немає PDF-документів зі статусом «Результати внесено». Спочатку лаборант має завершити заповнення бланків.";
            return RedirectToAction(nameof(Index));
        }

        if (targets.Count == 1)
        {
            var target = targets[0];
            return RedirectToAction(
                "Fill",
                "PdfWorkspace",
                new
                {
                    templateVersionId = target.TemplateVersionId,
                    orderId = target.OrderId,
                    orderDocumentId = target.OrderDocumentId,
                    sampleId
                });
        }

        return View(
            "ChooseDocument",
            new ExpertChooseDocumentViewModel
            {
                SampleNumber = sampleMeta.Number,
                InvestigationTypeName = sampleMeta.NameUk,
                Targets = targets
            });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid sampleId, CancellationToken cancellationToken)
    {
        var sample = await _context.Samples
            .AsNoTracking()
            .Where(entity => entity.Id == sampleId && !entity.IsAnnulled)
            .Select(entity => new
            {
                entity.Id,
                entity.Number,
                InvestigationTypeName = entity.InvestigationType.NameUk,
                CustomerFullName = entity.Order.Customer.FullName,
                entity.OrderId,
                entity.Order.ReferralNumber,
                entity.RegisteredAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sample is null)
        {
            return NotFound();
        }

        var review = await _context.ExpertConclusionReviews
            .AsNoTracking()
            .Where(entity => entity.SampleId == sampleId)
            .Select(entity => new
            {
                entity.Status,
                entity.ReviewStartedAtUtc,
                entity.ApprovedAtUtc,
                entity.NotesUk
            })
            .FirstOrDefaultAsync(cancellationToken);

        var documents = await _context.OrderDocuments
            .AsNoTracking()
            .Where(document => document.SampleId == sampleId && !document.IsAnnulled)
            .OrderBy(document => document.Template.NameUk)
            .ThenBy(document => document.TemplateVersion.VersionNumber)
            .Select(document => new ExpertSampleDocumentItemViewModel
            {
                OrderDocumentId = document.Id,
                TemplateVersionId = document.TemplateVersionId,
                TemplateName = document.Template.NameUk,
                VersionNumber = document.TemplateVersion.VersionNumber,
                TargetBranchName = document.TargetBranch.Name,
                Status = document.Status,
                SentToLabAtUtc = document.SentToLabAtUtc
            })
            .ToListAsync(cancellationToken);

        var model = new ExpertSampleDetailsViewModel
        {
            SampleId = sample.Id,
            SampleNumber = sample.Number,
            InvestigationTypeName = sample.InvestigationTypeName,
            CustomerFullName = sample.CustomerFullName,
            ReferralNumber = sample.ReferralNumber,
            RegisteredAtUtc = sample.RegisteredAt,
            ReviewStatus = review?.Status ?? ExpertConclusionStatus.PendingReview,
            ReviewStartedAtUtc = review?.ReviewStartedAtUtc,
            ApprovedAtUtc = review?.ApprovedAtUtc,
            NotesUk = review?.NotesUk,
            OrderId = sample.OrderId,
            Documents = documents
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnToQueue(Guid sampleId, CancellationToken cancellationToken)
    {
        var moved = await _conclusion.ReturnToPendingReviewAsync(sampleId, cancellationToken);
        TempData[moved ? "ExpertSuccess" : "ExpertWarning"] = moved
            ? "Пробу повернуто у чергу очікування розгляду."
            : "Пробу не вдалося повернути у чергу (можливо, висновок уже затверджено).";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid sampleId, string? notesUk, CancellationToken cancellationToken)
    {
        var normalizedNotes = NormalizeExpertNotes(notesUk);
        var approved = await _conclusion.ApproveAsync(sampleId, normalizedNotes, cancellationToken);
        if (!approved)
        {
            TempData["ExpertWarning"] =
                "Не вдалося затвердити висновок: проба ще не готова (усі відправлені документи мають бути «Результати внесено»).";
            return RedirectToAction(nameof(Index));
        }

        TempData["ExpertSuccess"] = "Висновок по пробі затверджено.";
        return RedirectToAction(nameof(Index), new ExpertReviewQueueFilter { ReviewStatus = Domain.Laboratory.ExpertConclusionStatus.Approved });
    }

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
