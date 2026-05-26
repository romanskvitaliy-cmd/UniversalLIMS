using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.ViewModels.Laboratory;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.EnterLaboratoryResults)]
[RequireActiveLimsRole]
public sealed class LaboratoryController : Controller
{
    private readonly ILaboratoryJournalService _journal;
    private readonly ILaboratoryPdfFillService _pdfFill;
    private readonly IResultEntryService _resultEntry;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public LaboratoryController(
        ILaboratoryJournalService journal,
        ILaboratoryPdfFillService pdfFill,
        IResultEntryService resultEntry,
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _journal = journal;
        _pdfFill = pdfFill;
        _resultEntry = resultEntry;
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] SampleJournalFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _journal.GetSamplesAsync(filter, cancellationToken);

        if (_environment.IsDevelopment())
        {
            ViewData["DemoMode"] = true;
        }

        return View(new LaboratoryIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    /// <summary>Табличне внесення показників (DataFieldScope.Result).</summary>
    [HttpGet]
    public async Task<IActionResult> ResultEntry(Guid sampleId, CancellationToken cancellationToken)
    {
        var form = await _resultEntry.GetResultEntryFormAsync(sampleId, cancellationToken);
        if (form is null)
        {
            return NotFound();
        }

        var pdfTargets = await _pdfFill.GetFillTargetsAsync(sampleId, cancellationToken);

        return View(
            "Results",
            new LaboratoryResultsViewModel
            {
                Form = form,
                HasPdfFillTargets = pdfTargets.Count > 0
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResultEntry(
        Guid sampleId,
        LaboratoryResultsViewModel model,
        CancellationToken cancellationToken)
    {
        var save = await _resultEntry.SaveResultValuesAsync(sampleId, model.Input, cancellationToken);
        var form = await _resultEntry.GetResultEntryFormAsync(sampleId, cancellationToken);
        if (form is null)
        {
            return NotFound();
        }

        var pdfTargets = await _pdfFill.GetFillTargetsAsync(sampleId, cancellationToken);
        var statusMessage = save.Success
            ? $"{save.Message} Поточний статус проби: {SampleStatusDisplay.ToUk(form.Status)}."
            : save.Message;

        return View(
            "Results",
            new LaboratoryResultsViewModel
            {
                Form = form,
                Input = model.Input,
                HasPdfFillTargets = pdfTargets.Count > 0,
                StatusMessage = statusMessage,
                StatusType = save.Success ? "success" : "danger"
            });
    }

    /// <summary>Відкриває PDF Workspace для заповнення полів на бланку.</summary>
    [HttpGet]
    public async Task<IActionResult> Results(Guid sampleId, CancellationToken cancellationToken)
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

        var targets = await _pdfFill.GetFillTargetsAsync(sampleId, cancellationToken);
        if (targets.Count == 0)
        {
            TempData["LaboratoryWarning"] =
                "Для цієї проби немає PDF-документів у статусі «У лабораторії» / «В роботі» і не знайдено опублікованого PDF-шаблону типу дослідження.";
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
                    sampleId
                });
        }

        return View(
            "ChooseDocument",
            new LaboratoryChooseDocumentViewModel
            {
                SampleNumber = sampleMeta.Number,
                InvestigationTypeName = sampleMeta.NameUk,
                Targets = targets
            });
    }
}
