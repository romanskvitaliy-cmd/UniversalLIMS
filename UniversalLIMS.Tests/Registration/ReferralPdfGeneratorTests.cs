using Microsoft.EntityFrameworkCore;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class ReferralPdfGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_WithReferralFilter_IncludesOnlyReferralDocuments()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        await SeedBranchAsync(context, branchId);
        var customer = await SeedCustomerAsync(context);
        var referralVersionId = await SeedPublishedPdfTemplateAsync(
            context,
            "REF-TEST",
            "Тестове направлення",
            TemplatePurpose.Referral);
        var protocolVersionId = await SeedPublishedPdfTemplateAsync(
            context,
            "F327",
            "Тестовий протокол",
            TemplatePurpose.Protocol);

        var order = await SeedOrderWithDocumentsAsync(
            context,
            customer.Id,
            branchId,
            referralVersionId,
            protocolVersionId);
        var storage = new TestTemplateDocumentStorage();
        var generator = new ReferralPdfGenerator(context, storage);

        var allPdf = await generator.GenerateAsync(order.Id);
        var referralPdf = await generator.GenerateAsync(order.Id, TemplatePurpose.Referral);
        var protocolPdf = await generator.GenerateAsync(order.Id, TemplatePurpose.Protocol);

        Assert.True(referralPdf.Length > 0);
        Assert.True(protocolPdf.Length > 0);
        Assert.True(allPdf.Length >= referralPdf.Length);
        Assert.True(allPdf.Length >= protocolPdf.Length);
    }

    [Fact]
    public async Task GenerateAsync_WithReferralFilter_ThrowsWhenNoReferralDocuments()
    {
        await using var context = CreateContext();
        var branchId = Guid.NewGuid();
        await SeedBranchAsync(context, branchId);
        var customer = await SeedCustomerAsync(context);
        var protocolVersionId = await SeedPublishedPdfTemplateAsync(
            context,
            "F327",
            "Тільки протокол",
            TemplatePurpose.Protocol);

        var order = await SeedOrderWithDocumentsAsync(
            context,
            customer.Id,
            branchId,
            protocolVersionId: protocolVersionId);
        var generator = new ReferralPdfGenerator(context, new TestTemplateDocumentStorage());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateAsync(order.Id, TemplatePurpose.Referral));

        Assert.Contains("REF", exception.Message, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedBranchAsync(ApplicationDbContext context, Guid branchId)
    {
        context.Branches.Add(new Branch
        {
            Id = branchId,
            Code = "ZHY",
            Name = "Житомир",
            City = "Житомир"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context)
    {
        var customer = new Customer { FullName = "Тест Друк" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Guid> SeedPublishedPdfTemplateAsync(
        ApplicationDbContext context,
        string code,
        string nameUk,
        TemplatePurpose purpose)
    {
        var template = new Template
        {
            Code = code,
            NameUk = nameUk,
            Status = TemplateStatus.Active,
            Purpose = purpose
        };
        context.Templates.Add(template);

        var version = new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = $"{code}.pdf"
        };
        context.TemplateVersions.Add(version);
        template.CurrentPublishedVersionId = version.Id;
        await context.SaveChangesAsync();
        return version.Id;
    }

    private static async Task<Order> SeedOrderWithDocumentsAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        Guid? referralVersionId = null,
        Guid? protocolVersionId = null)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = "REF-PRINT-001",
            Status = OrderStatus.Registered
        };
        context.Orders.Add(order);

        var sample = new Sample
        {
            OrderId = order.Id,
            Number = "ZHY-2026-00099",
            RegisteredAt = DateTime.UtcNow,
            Status = SampleStatus.Registered
        };
        context.Samples.Add(sample);

        if (referralVersionId is Guid referralId)
        {
            await AddDocumentAsync(context, order, sample, referralId, branchId);
        }

        if (protocolVersionId is Guid protocolId)
        {
            await AddDocumentAsync(context, order, sample, protocolId, branchId);
        }

        await context.SaveChangesAsync();
        return order;
    }

    private static async Task AddDocumentAsync(
        ApplicationDbContext context,
        Order order,
        Sample sample,
        Guid templateVersionId,
        Guid targetBranchId)
    {
        var version = await context.TemplateVersions
            .Include(item => item.Template)
            .SingleAsync(item => item.Id == templateVersionId);

        context.OrderDocuments.Add(new OrderDocument
        {
            OrderId = order.Id,
            SampleId = sample.Id,
            TemplateId = version.TemplateId,
            TemplateVersionId = version.Id,
            TargetBranchId = targetBranchId,
            Status = OrderDocumentStatus.Pending
        });
    }

    private sealed class TestTemplateDocumentStorage : ITemplateDocumentStorage
    {
        private readonly byte[] _pdfBytes = CreateBlankPdf();

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_pdfBytes));

        public Task<StoredTemplateDocument> SaveAsync(
            Guid templateId,
            Guid templateVersionId,
            string originalFileName,
            string contentType,
            Stream documentStream,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static byte[] CreateBlankPdf()
        {
            using var document = new PdfDocument();
            var page = document.Pages.Add();
            var graphics = page.Graphics;
            graphics.DrawString("Test", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 10, 10);
            using var stream = new MemoryStream();
            document.Save(stream);
            return stream.ToArray();
        }
    }
}
