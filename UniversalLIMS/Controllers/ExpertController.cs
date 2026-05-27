using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Expert;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.ApproveConclusions)]
[RequireActiveLimsRole]
public sealed class ExpertController : Controller
{
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid sampleId, string? notesUk, CancellationToken cancellationToken)
    {
        var approved = await _conclusion.ApproveAsync(sampleId, notesUk, cancellationToken);
        if (!approved)
        {
            TempData["ExpertWarning"] =
                "Не вдалося затвердити висновок: проба ще не готова (усі відправлені документи мають бути «Результати внесено»).";
            return RedirectToAction(nameof(Index));
        }

        TempData["ExpertSuccess"] = "Висновок по пробі затверджено.";
        return RedirectToAction(nameof(Index), new ExpertReviewQueueFilter { ReviewStatus = Domain.Laboratory.ExpertConclusionStatus.Approved });
    }
}
