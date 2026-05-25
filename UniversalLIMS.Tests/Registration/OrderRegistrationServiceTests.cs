using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class OrderRegistrationServiceTests
{
    [Fact]
    public async Task GetOrdersAsync_FiltersByCurrentUserBranch()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var customer = await SeedCustomerAsync(context, "Іваненко Петро");
        await SeedOrderAsync(context, customer.Id, branchA, "REF-A");
        await SeedOrderAsync(context, customer.Id, branchB, "REF-B");

        var service = CreateService(context, branchA);

        var result = await service.GetOrdersAsync(new OrderFilter());

        Assert.Single(result.Items);
        Assert.Equal("REF-A", result.Items[0].ReferralNumber);
    }

    [Fact]
    public async Task GetOrdersAsync_UsesCustomerFullName_NotOrderFieldValue()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var customer = await SeedCustomerAsync(context, "Коваль Олена");
        var orderId = await SeedOrderAsync(context, customer.Id, branchId, "REF-SSOT");

        var service = CreateService(context, branchId);

        var result = await service.GetOrdersAsync(new OrderFilter { CustomerFullName = "Коваль" });

        Assert.Single(result.Items);
        Assert.Equal("Коваль Олена", result.Items[0].CustomerFullName);
        Assert.Equal(orderId, result.Items[0].OrderId);
        Assert.DoesNotContain(
            context.OrderFieldValues,
            fieldValue => fieldValue.OrderId == orderId);
    }

    [Fact]
    public async Task GetOrdersAsync_ExcludesAnnulledOrdersAndCustomers()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var activeCustomer = await SeedCustomerAsync(context, "Активний Клієнт");
        var annulledCustomer = new Customer
        {
            FullName = "Анульований Клієнт",
            IsAnnulled = true
        };
        context.Customers.Add(annulledCustomer);
        await context.SaveChangesAsync();

        await SeedOrderAsync(context, activeCustomer.Id, branchId, "REF-ACTIVE");
        var annulledOrder = new Order
        {
            CustomerId = activeCustomer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-ANNULLED-ORDER",
            IsAnnulled = true
        };
        context.Orders.Add(annulledOrder);
        context.Orders.Add(new Order
        {
            CustomerId = annulledCustomer.Id,
            BranchId = branchId,
            ReferralNumber = "REF-ANNULLED-CUSTOMER"
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);

        var result = await service.GetOrdersAsync(new OrderFilter { PageSize = 50 });

        Assert.Single(result.Items);
        Assert.Equal("REF-ACTIVE", result.Items[0].ReferralNumber);
    }

    [Fact]
    public async Task CreateOrderAsync_AssignsNumbersAndLinksCustomerSsot()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);

        var customer = await SeedCustomerAsync(context, "Шевченко Тарас");
        var service = CreateService(context, branchId);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId
        });

        var order = await context.Orders
            .Include(item => item.Customer)
            .Include(item => item.Samples)
            .Include(item => item.OrderDocuments)
            .SingleAsync(item => item.Id == result.OrderId);

        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal("Шевченко Тарас", order.Customer.FullName);
        Assert.StartsWith("REF-", order.ReferralNumber);
        Assert.Single(order.Samples);
        Assert.Matches(@"^[a-zA-Z0-9]+-\d{4}-\d{5}$", order.Samples.First().Number);
        Assert.NotEqual(Guid.Empty, result.TemplateVersionId);
        Assert.Single(order.OrderDocuments);
    }

    [Fact]
    public async Task CreateOrderAsync_CreatesNewCustomerWhenRequested()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var service = CreateService(context, branchId);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            NewCustomer = new CreateCustomerRequest
            {
                Kind = CustomerKind.Individual,
                FullName = "Новий Замовник"
            },
            InvestigationTypeId = investigationTypeId
        });

        var order = await context.Orders.Include(item => item.Customer).SingleAsync(item => item.Id == result.OrderId);
        Assert.Equal("Новий Замовник", order.Customer.FullName);
    }

    [Fact]
    public async Task CreateOrderAsync_RequiresUserBranch()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var service = CreateService(context, branchId: null);

        var customer = await SeedCustomerAsync(context, "Тест");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync(new CreateOrderRequest
            {
                CustomerId = customer.Id,
                InvestigationTypeId = investigationTypeId
            }));
    }

    [Fact]
    public async Task GetOrdersAsync_FiltersByReferralNumber()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var customer = await SeedCustomerAsync(context, "Тест");
        await SeedOrderAsync(context, customer.Id, branchId, "PDF-20250101-001");
        await SeedOrderAsync(context, customer.Id, branchId, "PDF-20250102-002");

        var service = CreateService(context, branchId);

        var result = await service.GetOrdersAsync(new OrderFilter { ReferralNumber = "20250101" });

        Assert.Single(result.Items);
        Assert.Contains("20250101", result.Items[0].ReferralNumber);
    }

    private static async Task SeedBranchesAsync(ApplicationDbContext context, params Guid[] branchIds)
    {
        foreach (var branchId in branchIds)
        {
            context.Branches.Add(new Branch
            {
                Id = branchId,
                Code = branchId.ToString("N")[..6],
                Name = $"Філія {branchId.ToString("N")[..4]}",
                City = "Житомир"
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Customer> SeedCustomerAsync(ApplicationDbContext context, string fullName)
    {
        var customer = new Customer { FullName = fullName };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    private static async Task<Guid> SeedOrderAsync(
        ApplicationDbContext context,
        Guid customerId,
        Guid branchId,
        string referralNumber)
    {
        var order = new Order
        {
            CustomerId = customerId,
            BranchId = branchId,
            ReferralNumber = referralNumber,
            Status = OrderStatus.Draft
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order.Id;
    }

    private static async Task<Guid> SeedInvestigationTypeWithPdfTemplateAsync(ApplicationDbContext context)
    {
        var investigationType = new InvestigationType
        {
            Code = "TST",
            NameUk = "Тестовий тип",
            SortOrder = 1
        };
        context.InvestigationTypes.Add(investigationType);

        var template = new Template
        {
            Code = "TST-TPL",
            NameUk = "Тестовий шаблон"
        };
        context.Templates.Add(template);

        var version = new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = "test.pdf"
        };
        context.TemplateVersions.Add(version);
        template.CurrentPublishedVersionId = version.Id;

        context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
        {
            InvestigationTypeId = investigationType.Id,
            TemplateId = template.Id,
            SortOrder = 1
        });

        await context.SaveChangesAsync();
        return investigationType.Id;
    }

    private static OrderRegistrationService CreateService(
        ApplicationDbContext context,
        Guid? branchId) =>
        new(
            context,
            new FixedBranchUserService(branchId),
            new CustomerService(context),
            new NumberingService(context, new FixedDateTimeProvider()),
            new FixedDateTimeProvider());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FixedBranchUserService : ICurrentUserService
    {
        public FixedBranchUserService(Guid? branchId) => BranchId = branchId;

        public string? UserId => "test-user";

        public string? UserName => "test";

        public string? UserFullName => "Test User";

        public Guid? BranchId { get; }

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? CorrelationId => null;

        public bool IsAuthenticated => true;
    }
}
