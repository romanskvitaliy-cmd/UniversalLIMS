using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Filters;
using UniversalLIMS.ViewModels.Registration;

namespace UniversalLIMS.Controllers;

[Authorize(Policy = LimsPolicies.RegisterSamples)]
[RequireActiveLimsRole]
public sealed class OrdersController : Controller
{
    private static readonly JsonSerializerOptions FieldMappingJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOrderRegistrationService _orderRegistration;
    private readonly IOrderFieldLinkService _orderFieldLinks;
    private readonly ICustomerService _customerService;
    private readonly ITemplateOriginalOpenTokenIssuer _openTokenIssuer;
    private readonly IReferralPdfGenerator _referralPdfGenerator;

    public OrdersController(
        IOrderRegistrationService orderRegistration,
        IOrderFieldLinkService orderFieldLinks,
        ICustomerService customerService,
        ITemplateOriginalOpenTokenIssuer openTokenIssuer,
        IReferralPdfGenerator referralPdfGenerator)
    {
        _orderRegistration = orderRegistration;
        _orderFieldLinks = orderFieldLinks;
        _customerService = customerService;
        _openTokenIssuer = openTokenIssuer;
        _referralPdfGenerator = referralPdfGenerator;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] OrderFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _orderRegistration.GetOrdersAsync(filter, cancellationToken);

        return View(new OrdersIndexViewModel
        {
            Filter = filter,
            Result = result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);
        return View(BuildCreateViewModel(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareFieldMapping(
        OrderCreateInputModel input,
        CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);

        ValidateCustomerSelection(input);
        ValidateDocumentSelection(input, form);

        var selectedTemplateVersionIds = GetSelectedTemplateVersionIds(input);
        if (selectedTemplateVersionIds.Count < 2)
        {
            ModelState.AddModelError(
                nameof(input.Samples),
                "Мапінг спільних полів доступний для двох і більше шаблонів.");
        }

        if (!ModelState.IsValid)
        {
            return View("Create", BuildCreateViewModel(form, input));
        }

        try
        {
            var mapping = await _orderFieldLinks.GetMappingPrepareAsync(
                selectedTemplateVersionIds,
                cancellationToken);

            return View("MapOrderFields", await BuildMapOrderFieldsViewModelAsync(form, input, mapping, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Create", BuildCreateViewModel(form, input));
        }
    }

    [HttpGet]
    public async Task<IActionResult> AdaptFieldMapping(
        Guid sourceOrderId,
        [FromQuery] List<Guid> templateVersionIds,
        CancellationToken cancellationToken)
    {
        if (templateVersionIds.Count < 2)
        {
            return BadRequest(new { message = "Потрібно щонайменше два шаблони." });
        }

        try
        {
            var result = await _orderFieldLinks.AdaptFieldLinkGroupsFromOrderAsync(
                sourceOrderId,
                templateVersionIds,
                cancellationToken);

            return Json(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackToCreateFromMapping(
        OrderCreateInputModel input,
        CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);
        return View("Create", BuildCreateViewModel(form, input));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWithFieldMapping(
        OrderCreateInputModel input,
        string? fieldMappingJson,
        CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);

        ValidateCustomerSelection(input);
        ValidateDocumentSelection(input, form);

        if (!TryParseFieldMappingPayload(fieldMappingJson, out var groups, out var sharedValues, out var parseError))
        {
            ModelState.AddModelError(string.Empty, parseError);
        }

        if (!ModelState.IsValid)
        {
            try
            {
                var mapping = await _orderFieldLinks.GetMappingPrepareAsync(
                    GetSelectedTemplateVersionIds(input),
                    cancellationToken);

                return View("MapOrderFields", await BuildMapOrderFieldsViewModelAsync(form, input, mapping, cancellationToken));
            }
            catch
            {
                return View("Create", BuildCreateViewModel(form, input));
            }
        }

        try
        {
            var result = await _orderRegistration.CreateOrderAsync(MapToRequest(input, form), cancellationToken);

            if (groups.Count > 0)
            {
                await _orderFieldLinks.SaveFieldLinkGroupsAsync(result.OrderId, groups, cancellationToken);
                await _orderFieldLinks.ApplySharedFieldValuesAsync(
                    result.OrderId,
                    groups,
                    sharedValues,
                    cancellationToken);
            }

            TempData["OrderCreateSuccess"] =
                BuildOrderCreateSuccessMessage(result) +
                (groups.Count > 0 ? $" Об’єднано груп полів: {groups.Count}." : string.Empty);

            var pdfFillRedirect = TryBuildPostCreatePdfFillRedirect(result, input.OpenPdfAfterCreate);
            if (pdfFillRedirect is not null)
            {
                return pdfFillRedirect;
            }

            return RedirectToAction(nameof(Details), new { id = result.OrderId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            var mapping = await _orderFieldLinks.GetMappingPrepareAsync(
                GetSelectedTemplateVersionIds(input),
                cancellationToken);

            return View("MapOrderFields", await BuildMapOrderFieldsViewModelAsync(form, input, mapping, cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OrderCreateInputModel input,
        CancellationToken cancellationToken)
    {
        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);

        ValidateCustomerSelection(input);
        ValidateDocumentSelection(input, form);

        if (!ModelState.IsValid)
        {
            return View(BuildCreateViewModel(form, input));
        }

        try
        {
            var result = await _orderRegistration.CreateOrderAsync(MapToRequest(input, form), cancellationToken);

            TempData["OrderCreateSuccess"] = BuildOrderCreateSuccessMessage(result);

            var pdfFillRedirect = TryBuildPostCreatePdfFillRedirect(result, input.OpenPdfAfterCreate);
            if (pdfFillRedirect is not null)
            {
                return pdfFillRedirect;
            }

            return RedirectToAction(nameof(Details), new { id = result.OrderId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(BuildCreateViewModel(form, input));
        }
    }

    [HttpGet]
    public async Task<IActionResult> PrintReferralDocuments(Guid id, CancellationToken cancellationToken) =>
        await PrintDocumentsByPurposeAsync(id, TemplatePurpose.Referral, "napravlennia", cancellationToken);

    [HttpGet]
    public async Task<IActionResult> PrintProtocolDocuments(Guid id, CancellationToken cancellationToken) =>
        await PrintDocumentsByPurposeAsync(id, TemplatePurpose.Protocol, "protokoly", cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var detail = await _orderRegistration.GetOrderDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);
        var fieldLinkGroups = await _orderFieldLinks.GetFieldLinkGroupsForOrderAsync(id, cancellationToken);

        return View(new OrderDetailViewModel
        {
            Detail = detail,
            Branches = form.Branches,
            CustomerEdit = BuildCustomerEditModel(detail),
            FieldLinkGroups = fieldLinkGroups,
            AppendForm = EnrichCreateFormWithPreviewUrls(form),
            AppendSamples = new AppendOrderSamplesInputModel { OrderId = detail.OrderId }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AppendSamples(
        [Bind(Prefix = "AppendSamples")] AppendOrderSamplesInputModel input,
        CancellationToken cancellationToken)
    {
        var detail = await _orderRegistration.GetOrderDetailAsync(input.OrderId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        var form = await _orderRegistration.GetCreateFormAsync(cancellationToken);
        ValidateAppendSampleSelection(input, form);

        if (!ModelState.IsValid)
        {
            var fieldLinkGroups = await _orderFieldLinks.GetFieldLinkGroupsForOrderAsync(input.OrderId, cancellationToken);
            TempData["OrderDetailAppendOpen"] = true;
            return View("Details", new OrderDetailViewModel
            {
                Detail = detail,
                Branches = form.Branches,
                CustomerEdit = BuildCustomerEditModel(detail),
                FieldLinkGroups = fieldLinkGroups,
                AppendForm = EnrichCreateFormWithPreviewUrls(form),
                AppendSamples = input
            });
        }

        try
        {
            var result = await _orderRegistration.AppendSamplesAsync(
                new AppendOrderSamplesRequest
                {
                    OrderId = input.OrderId,
                    Samples = MapAppendSamplesToRequest(input, form)
                },
                cancellationToken);

            var sampleNumbers = string.Join(", ", result.Samples.Select(sample => sample.SampleNumber));
            TempData["OrderDetailSuccess"] = result.Samples.Count == 1
                ? $"Додано пробу {sampleNumbers}, документів: {result.Documents.Count}."
                : $"Додано проб: {result.Samples.Count} ({sampleNumbers}), документів: {result.Documents.Count}.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
            TempData["OrderDetailAppendOpen"] = true;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomer(
        [Bind(Prefix = "CustomerEdit")] UpdateOrderCustomerInputModel input,
        CancellationToken cancellationToken)
    {
        var detail = await _orderRegistration.GetOrderDetailAsync(input.OrderId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        if (input.CustomerId == Guid.Empty || input.CustomerId != detail.CustomerId)
        {
            TempData["OrderDetailError"] = "Замовника для редагування не знайдено у цій справі.";
            return RedirectToAction(nameof(Details), new { id = input.OrderId });
        }

        if (string.IsNullOrWhiteSpace(input.FullName))
        {
            TempData["OrderDetailError"] = "ПІБ або назва замовника є обов'язковими.";
            return RedirectToAction(nameof(Details), new { id = input.OrderId });
        }

        try
        {
            await _customerService.UpdateAsync(
                input.CustomerId,
                new UpdateCustomerRequest
                {
                    Kind = input.Kind,
                    FullName = input.FullName,
                    OrganizationName = input.OrganizationName,
                    ContactPhone = input.ContactPhone,
                    Email = input.Email,
                    Address = input.Address,
                    Edrpou = input.Edrpou,
                    Rnokpp = input.Rnokpp,
                    Notes = input.Notes
                },
                cancellationToken);

            TempData["OrderDetailSuccess"] = "Дані замовника оновлено.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDocuments(
        SendOrderDocumentsInputModel input,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderRegistration.SendDocumentsToLabAsync(
                new SendOrderDocumentsRequest
                {
                    OrderId = input.OrderId,
                    OrderDocumentIds = input.OrderDocumentIds
                },
                cancellationToken);

            TempData["OrderDetailSuccess"] = "Обрані документи відправлено в лабораторію.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRouting(
        UpdateOrderDocumentRoutingInputModel input,
        CancellationToken cancellationToken)
    {
        try
        {
            await _orderRegistration.UpdateDocumentRoutingAsync(
                new UpdateOrderDocumentRoutingRequest
                {
                    OrderId = input.OrderId,
                    OrderDocumentId = input.OrderDocumentId,
                    TargetBranchId = input.TargetBranchId
                },
                cancellationToken);

            TempData["OrderDetailSuccess"] = "Маршрут документа оновлено.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = input.OrderId });
    }

    private async Task<OrderMapFieldsViewModel> BuildMapOrderFieldsViewModelAsync(
        OrderCreateFormDto form,
        OrderCreateInputModel input,
        OrderFieldMappingPrepareDto mapping,
        CancellationToken cancellationToken)
    {
        var copySourceOrders = await _orderFieldLinks.GetFieldMappingSourceOrdersAsync(20, cancellationToken);

        return new OrderMapFieldsViewModel
        {
            Form = form,
            CreateInput = input,
            Mapping = mapping,
            CopySourceOrders = copySourceOrders
        };
    }

    private void ValidateCustomerSelection(OrderCreateInputModel input)
    {
        if (string.Equals(input.CustomerMode, "new", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(input.NewCustomerFullName))
            {
                ModelState.AddModelError(nameof(input.NewCustomerFullName), "ПІБ або назва нового замовника є обов'язковими.");
            }

            return;
        }

        if (!input.SelectedCustomerId.HasValue || input.SelectedCustomerId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(input.SelectedCustomerId), "Оберіть замовника з результатів пошуку.");
        }
    }

    private async Task<IActionResult> PrintDocumentsByPurposeAsync(
        Guid orderId,
        TemplatePurpose purpose,
        string fileNameStem,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _orderRegistration.GetOrderDetailAsync(orderId, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            var pdfBytes = await _referralPdfGenerator.GenerateAsync(orderId, purpose, cancellationToken);
            var fileName = BuildOrderPrintFileName(detail.ReferralNumber, fileNameStem);
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            TempData["OrderDetailError"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }

    private static string BuildOrderPrintFileName(string? referralNumber, string stem)
    {
        var safeNumber = string.IsNullOrWhiteSpace(referralNumber)
            ? "sprava"
            : referralNumber.Trim().Replace('/', '-').Replace('\\', '-');
        return $"{stem}-{safeNumber}.pdf";
    }

    private static string BuildOrderCreateSuccessMessage(CreateOrderResult result)
    {
        var referralNumber = string.IsNullOrWhiteSpace(result.ReferralNumber)
            ? result.OrderId.ToString("N")[..8]
            : result.ReferralNumber;

        return result.Samples.Count <= 1
            ? $"Створено справу {referralNumber}, проба {result.SampleNumber}, бланків PDF: {result.Documents.Count}."
            : $"Створено справу {referralNumber}, проб: {result.Samples.Count}, бланків PDF: {result.Documents.Count}.";
    }

    private static RedirectToActionResult? TryBuildPostCreatePdfFillRedirect(
        CreateOrderResult result,
        bool openPdfAfterCreate)
    {
        var route = OrderPostCreateNavigation.TryGetSingleDocumentPdfFillRoute(result, openPdfAfterCreate);
        if (route is null)
        {
            return null;
        }

        return new RedirectToActionResult(
            "Fill",
            "PdfWorkspace",
            new
            {
                templateVersionId = route.TemplateVersionId,
                orderId = route.OrderId,
                orderDocumentId = route.OrderDocumentId
            });
    }

    private void ValidateDocumentSelection(OrderCreateInputModel input, OrderCreateFormDto form)
    {
        ValidateSampleSelection(GetSubmittedSamples(input), form, nameof(input.Samples));
    }

    private void ValidateAppendSampleSelection(AppendOrderSamplesInputModel input, OrderCreateFormDto form)
    {
        ValidateSampleSelection(GetSubmittedAppendSamples(input), form, nameof(input.Samples));
    }

    private void ValidateSampleSelection(
        List<OrderCreateSampleInputModel> samples,
        OrderCreateFormDto form,
        string samplesFieldName)
    {
        if (samples.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Додайте хоча б одну пробу з бланком PDF.");
            return;
        }

        var referralAllowedIds = form.ReferralTemplateOptions
            .Select(option => option.TemplateVersionId)
            .ToHashSet();
        var requireReferralPerSample = form.ReferralTemplateOptions.Count > 0;

        for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
        {
            var sample = samples[sampleIndex];
            var selectedTemplateVersionIds = sample.SelectedTemplateVersionIds
                .Where(id => id != Guid.Empty)
                .ToList();
            var referralTemplateVersionId = sample.ReferralTemplateVersionId ?? Guid.Empty;

            if (sample.InvestigationTypeId == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty, $"У рядку {sampleIndex + 1} оберіть бланк протоколу для проби.");
                continue;
            }

            if (selectedTemplateVersionIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, $"У рядку {sampleIndex + 1} оберіть бланк протоколу PDF.");
                continue;
            }

            if (requireReferralPerSample && referralTemplateVersionId == Guid.Empty)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"У рядку {sampleIndex + 1} оберіть бланк направлення REF-*.");
                continue;
            }

            if (referralTemplateVersionId != Guid.Empty && !referralAllowedIds.Contains(referralTemplateVersionId))
            {
                ModelState.AddModelError(
                    samplesFieldName,
                    $"У рядку {sampleIndex + 1} обрано недоступний бланк направлення REF.");
            }

            var allowedIds = form.TemplateOptions
                .Where(option => option.InvestigationTypeId == sample.InvestigationTypeId)
                .Select(option => option.TemplateVersionId)
                .ToHashSet();

            if (selectedTemplateVersionIds.Any(id => !allowedIds.Contains(id)))
            {
                ModelState.AddModelError(
                    samplesFieldName,
                    $"У рядку {sampleIndex + 1} обрано недоступний бланк протоколу для цієї проби.");
            }

            var distinctTemplateIds = selectedTemplateVersionIds.ToList();
            if (referralTemplateVersionId != Guid.Empty)
            {
                distinctTemplateIds.Add(referralTemplateVersionId);
            }

            if (distinctTemplateIds.Distinct().Count() != distinctTemplateIds.Count)
            {
                ModelState.AddModelError(
                    samplesFieldName,
                    $"У рядку {sampleIndex + 1} кожен шаблон можна обрати лише один раз.");
            }
        }
    }

    private static CreateOrderRequest MapToRequest(OrderCreateInputModel input, OrderCreateFormDto form)
    {
        var useExisting = string.Equals(input.CustomerMode, "existing", StringComparison.OrdinalIgnoreCase);
        var defaultBranchId = form.DefaultBranchId ?? Guid.Empty;

        var samples = GetSubmittedSamples(input)
            .Select(sample => new CreateOrderSampleRequest
            {
                InvestigationTypeId = sample.InvestigationTypeId,
                TemplateVersionId = sample.TemplateVersionId,
                ReferralTemplateVersionId = sample.ReferralTemplateVersionId,
                Documents = MapDocuments(sample, defaultBranchId)
            })
            .ToList();

        return new CreateOrderRequest
        {
            CustomerId = useExisting ? input.SelectedCustomerId : null,
            NewCustomer = useExisting
                ? null
                : new CreateCustomerRequest
                {
                    Kind = input.NewCustomerKind,
                    FullName = input.NewCustomerFullName ?? string.Empty,
                    OrganizationName = input.NewCustomerOrganizationName,
                    ContactPhone = input.NewCustomerContactPhone,
                    Address = input.NewCustomerAddress,
                    Edrpou = input.NewCustomerEdrpou
                },
            Samples = samples
        };
    }

    private static List<CreateOrderDocumentRequest> MapDocuments(
        OrderCreateSampleInputModel sample,
        Guid defaultBranchId)
    {
        var documents = new List<CreateOrderDocumentRequest>();
        for (var index = 0; index < sample.SelectedTemplateVersionIds.Count; index++)
        {
            var templateVersionId = sample.SelectedTemplateVersionIds[index];
            var targetBranchId = index < sample.DocumentTargetBranchIds.Count
                && sample.DocumentTargetBranchIds[index] != Guid.Empty
                ? sample.DocumentTargetBranchIds[index]
                : defaultBranchId;

            documents.Add(new CreateOrderDocumentRequest
            {
                TemplateVersionId = templateVersionId,
                TargetBranchId = targetBranchId
            });
        }

        return documents;
    }

    private static List<OrderCreateSampleInputModel> GetSubmittedSamples(OrderCreateInputModel input)
    {
        return NormalizeSubmittedSamples(input.Samples);
    }

    private static List<OrderCreateSampleInputModel> GetSubmittedAppendSamples(AppendOrderSamplesInputModel input)
    {
        return NormalizeSubmittedSamples(input.Samples);
    }

    private static List<OrderCreateSampleInputModel> NormalizeSubmittedSamples(List<OrderCreateSampleInputModel> samples)
    {
        return samples
            .Where(sample => sample.InvestigationTypeId != Guid.Empty
                || sample.TemplateVersionId.HasValue
                || sample.SelectedTemplateVersionIds.Any(id => id != Guid.Empty))
            .Select(sample =>
            {
                if (sample.SelectedTemplateVersionIds.Count == 0 && sample.TemplateVersionId is Guid versionId)
                {
                    sample.SelectedTemplateVersionIds.Add(versionId);
                }

                return sample;
            })
            .ToList();
    }

    private static IReadOnlyList<CreateOrderSampleRequest> MapAppendSamplesToRequest(
        AppendOrderSamplesInputModel input,
        OrderCreateFormDto form)
    {
        var defaultBranchId = form.DefaultBranchId ?? Guid.Empty;

        return GetSubmittedAppendSamples(input)
            .Select(sample => new CreateOrderSampleRequest
            {
                InvestigationTypeId = sample.InvestigationTypeId,
                TemplateVersionId = sample.TemplateVersionId,
                Documents = MapDocuments(sample, defaultBranchId)
            })
            .ToList();
    }

    private static IReadOnlyList<Guid> GetSelectedTemplateVersionIds(OrderCreateInputModel input) =>
        GetSubmittedSamples(input)
            .SelectMany(sample =>
            {
                var ids = sample.SelectedTemplateVersionIds.Where(id => id != Guid.Empty).ToList();
                if (sample.ReferralTemplateVersionId is Guid referralId && referralId != Guid.Empty)
                {
                    ids.Add(referralId);
                }

                return ids;
            })
            .Distinct()
            .ToList();

    private static bool TryParseFieldMappingPayload(
        string? json,
        out IReadOnlyList<OrderFieldLinkGroupInput> groups,
        out IReadOnlyList<OrderSharedFieldValueInput> sharedValues,
        out string error)
    {
        groups = [];
        sharedValues = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<FieldMappingPayload>(json, FieldMappingJsonOptions);
            if (payload is null)
            {
                return true;
            }

            groups = payload.Groups ?? [];
            sharedValues = payload.SharedValues ?? [];
            return true;
        }
        catch (JsonException)
        {
            error = "Некоректний формат мапінгу полів. Спробуйте об’єднати поля знову.";
            return false;
        }
    }

    private sealed class FieldMappingPayload
    {
        public List<OrderFieldLinkGroupInput>? Groups { get; set; }

        public List<OrderSharedFieldValueInput>? SharedValues { get; set; }
    }

    private OrderCreateViewModel BuildCreateViewModel(
        OrderCreateFormDto form,
        OrderCreateInputModel? input = null) =>
        new()
        {
            Form = EnrichCreateFormWithPreviewUrls(form),
            Input = input ?? new OrderCreateInputModel()
        };

    private OrderCreateFormDto EnrichCreateFormWithPreviewUrls(OrderCreateFormDto form) =>
        new()
        {
            InvestigationTypes = form.InvestigationTypes,
            Branches = form.Branches,
            DefaultBranchId = form.DefaultBranchId,
            TemplateOptions = form.TemplateOptions
                .Select(option => new OrderTemplateOptionDto
                {
                    InvestigationTypeId = option.InvestigationTypeId,
                    TemplateVersionId = option.TemplateVersionId,
                    TemplateNameUk = option.TemplateNameUk,
                    VersionNumber = option.VersionNumber,
                    IsDefault = option.IsDefault,
                    Purpose = option.Purpose,
                    PreviewUrl = BuildTemplatePreviewUrl(option.TemplateVersionId)
                })
                .ToList(),
            ReferralTemplateOptions = form.ReferralTemplateOptions
                .Select(option => new OrderReferralTemplateOptionDto
                {
                    TemplateVersionId = option.TemplateVersionId,
                    TemplateCode = option.TemplateCode,
                    TemplateNameUk = option.TemplateNameUk,
                    VersionNumber = option.VersionNumber,
                    PreviewUrl = BuildTemplatePreviewUrl(option.TemplateVersionId)
                })
                .ToList()
        };

    private string? BuildTemplatePreviewUrl(Guid templateVersionId)
    {
        var token = _openTokenIssuer.CreateToken(templateVersionId);
        return Url.Action(
            nameof(TemplateVersionsController.OpenOriginal),
            "TemplateVersions",
            new { id = templateVersionId, token });
    }

    private static UpdateOrderCustomerInputModel BuildCustomerEditModel(OrderDetailDto detail) =>
        new()
        {
            OrderId = detail.OrderId,
            CustomerId = detail.CustomerId,
            Kind = detail.CustomerKind,
            FullName = detail.CustomerFullName,
            OrganizationName = detail.CustomerOrganizationName,
            ContactPhone = detail.CustomerContactPhone,
            Email = detail.CustomerEmail,
            Address = detail.CustomerAddress,
            Edrpou = detail.CustomerEdrpou,
            Rnokpp = detail.CustomerRnokpp,
            Notes = detail.CustomerNotes
        };
}
