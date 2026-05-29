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
    public async Task CreateOrderAsync_CreatesMultipleOrderDocumentsWithBranches()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchA, branchB);
        var (investigationTypeId, versionId1, versionId2) = await SeedInvestigationTypeWithTwoPdfTemplatesAsync(context);

        var customer = await SeedCustomerAsync(context, "Мультидок Клієнт");
        var service = CreateService(context, branchA);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId,
            Documents =
            [
                new CreateOrderDocumentRequest { TemplateVersionId = versionId1, TargetBranchId = branchA },
                new CreateOrderDocumentRequest { TemplateVersionId = versionId2, TargetBranchId = branchB }
            ]
        });

        Assert.Equal(2, result.Documents.Count);

        var documents = await context.OrderDocuments
            .Where(document => document.OrderId == result.OrderId && !document.IsAnnulled)
            .ToListAsync();

        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document => document.TemplateVersionId == versionId1 && document.TargetBranchId == branchA);
        Assert.Contains(documents, document => document.TemplateVersionId == versionId2 && document.TargetBranchId == branchB);
        Assert.All(documents, document => Assert.Equal(OrderDocumentStatus.Pending, document.Status));
    }

    [Fact]
    public async Task CreateOrderAsync_CreatesOneOrderWithMultipleSamplesAndDocuments()
    {
        var registrarBranchId = Guid.NewGuid();
        var waterLabId = Guid.NewGuid();
        var foodLabId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, registrarBranchId, waterLabId, foodLabId);
        var (waterTypeId, foodTypeId, _) = await SeedDefaultInvestigationTypesAsync(context);
        var waterVersionId = await SeedActivePublishedPdfTemplateAsync(context, "F327", "Ф327 дослідження питної води");
        var foodVersionId = await SeedActivePublishedPdfTemplateAsync(context, "F343", "Ф343 досліджень проб харчових продуктів");
        var customer = await SeedCustomerAsync(context, "Кілька Досліджень");
        var service = CreateService(context, registrarBranchId);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Samples =
            [
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = waterTypeId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = waterVersionId,
                            TargetBranchId = waterLabId
                        }
                    ]
                },
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = foodTypeId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = foodVersionId,
                            TargetBranchId = foodLabId
                        }
                    ]
                }
            ]
        });

        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(2, result.Documents.Count);

        var order = await context.Orders
            .Include(item => item.Samples)
            .Include(item => item.OrderDocuments)
            .SingleAsync(item => item.Id == result.OrderId);

        Assert.Equal(registrarBranchId, order.BranchId);
        Assert.Equal(2, order.Samples.Count);
        Assert.Equal(2, order.OrderDocuments.Count);
        Assert.Equal(2, order.Samples.Select(sample => sample.Number).Distinct().Count());
        Assert.Contains(order.Samples, sample => sample.InvestigationTypeId == waterTypeId);
        Assert.Contains(order.Samples, sample => sample.InvestigationTypeId == foodTypeId);
        Assert.Contains(order.OrderDocuments, document =>
            document.TemplateVersionId == waterVersionId && document.TargetBranchId == waterLabId);
        Assert.Contains(order.OrderDocuments, document =>
            document.TemplateVersionId == foodVersionId && document.TargetBranchId == foodLabId);
        Assert.All(order.OrderDocuments, document =>
            Assert.Contains(result.Samples, sample => sample.SampleId == document.SampleId));

        var detail = await service.GetOrderDetailAsync(result.OrderId);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Samples.Count);
        Assert.Equal(2, detail.Documents.Select(document => document.SampleId).Distinct().Count());
        Assert.Contains(detail.Samples, sample => sample.InvestigationTypeNameUk.Contains("води"));
        Assert.Contains(detail.Samples, sample => sample.InvestigationTypeNameUk.Contains("харчових"));
    }

    [Fact]
    public async Task SendDocumentsToLabAsync_UpdatesDocumentAndSampleStatus()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var customer = await SeedCustomerAsync(context, "Відправка");
        var service = CreateService(context, branchId);

        var created = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId
        });

        var documentId = created.Documents[0].OrderDocumentId;

        await service.SendDocumentsToLabAsync(
            new SendOrderDocumentsRequest
            {
                OrderId = created.OrderId,
                OrderDocumentIds = [documentId]
            });

        var document = await context.OrderDocuments.SingleAsync(item => item.Id == documentId);
        var sample = await context.Samples.SingleAsync(item => item.Id == created.SampleId);

        Assert.Equal(OrderDocumentStatus.SentToLab, document.Status);
        Assert.NotNull(document.SentToLabAtUtc);
        Assert.Equal(SampleStatus.Routed, sample.Status);
        Assert.NotNull(sample.RoutedAtUtc);
    }

    [Fact]
    public async Task GetOrderDetailAsync_ReturnsDocumentsForOrder()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var customer = await SeedCustomerAsync(context, "Деталі");
        var service = CreateService(context, branchId);

        var created = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId
        });

        var detail = await service.GetOrderDetailAsync(created.OrderId);

        Assert.NotNull(detail);
        Assert.Single(detail!.Documents);
        Assert.True(detail.Documents[0].CanFill);
        Assert.True(detail.Documents[0].CanSendToLab);
    }

    [Fact]
    public async Task AppendSamplesAsync_AddsSamplesToExistingOrder()
    {
        var registrarBranchId = Guid.NewGuid();
        var waterLabId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, registrarBranchId, waterLabId);
        var (waterTypeId, foodTypeId, _) = await SeedDefaultInvestigationTypesAsync(context);
        var waterVersionId = await SeedActivePublishedPdfTemplateAsync(context, "F327", "Ф327 дослідження питної води");
        var foodVersionId = await SeedActivePublishedPdfTemplateAsync(context, "F343", "Ф343 досліджень проб харчових продуктів");
        var customer = await SeedCustomerAsync(context, "Доповнення");
        var service = CreateService(context, registrarBranchId);

        var created = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Samples =
            [
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = waterTypeId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = waterVersionId,
                            TargetBranchId = waterLabId
                        }
                    ]
                }
            ]
        });

        var appendResult = await service.AppendSamplesAsync(new AppendOrderSamplesRequest
        {
            OrderId = created.OrderId,
            Samples =
            [
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = foodTypeId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = foodVersionId,
                            TargetBranchId = waterLabId
                        }
                    ]
                }
            ]
        });

        Assert.Single(appendResult.Samples);
        Assert.Single(appendResult.Documents);
        Assert.NotEqual(created.SampleNumber, appendResult.Samples[0].SampleNumber);

        var detail = await service.GetOrderDetailAsync(created.OrderId);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Samples.Count);
        Assert.Equal(2, detail.Documents.Count);
        Assert.Contains(detail.Samples, sample => sample.InvestigationTypeNameUk.Contains("харчових"));
    }

    [Fact]
    public async Task AppendSamplesAsync_WorksWhenExistingDocumentAlreadySent()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var customer = await SeedCustomerAsync(context, "Часткова відправка");
        var service = CreateService(context, branchId);

        var created = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId
        });

        await service.SendDocumentsToLabAsync(new SendOrderDocumentsRequest
        {
            OrderId = created.OrderId,
            OrderDocumentIds = [created.Documents[0].OrderDocumentId]
        });

        var appendResult = await service.AppendSamplesAsync(new AppendOrderSamplesRequest
        {
            OrderId = created.OrderId,
            Samples =
            [
                new CreateOrderSampleRequest
                {
                    InvestigationTypeId = investigationTypeId,
                    Documents =
                    [
                        new CreateOrderDocumentRequest
                        {
                            TemplateVersionId = created.TemplateVersionId,
                            TargetBranchId = branchId
                        }
                    ]
                }
            ]
        });

        var detail = await service.GetOrderDetailAsync(created.OrderId);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Samples.Count);
        Assert.Equal(OrderDocumentStatus.SentToLab, detail.Documents.First(document => document.OrderDocumentId == created.Documents[0].OrderDocumentId).Status);
        Assert.Equal(OrderDocumentStatus.Pending, detail.Documents.First(document => document.OrderDocumentId == appendResult.Documents[0].OrderDocumentId).Status);
        Assert.True(detail.Documents.First(document => document.OrderDocumentId == appendResult.Documents[0].OrderDocumentId).CanFill);
    }

    [Fact]
    public async Task GetOrderDetailAsync_ReturnsCustomerFieldsForRegistryEditing()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);
        var customer = await SeedCustomerAsync(context, "Редагований Клієнт");
        customer.Kind = CustomerKind.LegalEntity;
        customer.OrganizationName = "ТОВ Тест";
        customer.ContactPhone = "+380500000000";
        customer.Email = "test@example.com";
        customer.Address = "м. Житомир";
        customer.Edrpou = "12345678";
        customer.Rnokpp = "1234567890";
        customer.Notes = "нотатка";
        await context.SaveChangesAsync();

        var service = CreateService(context, branchId);
        var created = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = investigationTypeId
        });

        var detail = await service.GetOrderDetailAsync(created.OrderId);

        Assert.NotNull(detail);
        Assert.Equal(customer.Id, detail!.CustomerId);
        Assert.Equal(CustomerKind.LegalEntity, detail.CustomerKind);
        Assert.Equal("Редагований Клієнт", detail.CustomerFullName);
        Assert.Equal("ТОВ Тест", detail.CustomerOrganizationName);
        Assert.Equal("+380500000000", detail.CustomerContactPhone);
        Assert.Equal("test@example.com", detail.CustomerEmail);
        Assert.Equal("м. Житомир", detail.CustomerAddress);
        Assert.Equal("12345678", detail.CustomerEdrpou);
        Assert.Equal("1234567890", detail.CustomerRnokpp);
        Assert.Equal("нотатка", detail.CustomerNotes);
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

    [Fact]
    public async Task GetCreateFormAsync_ListsActivePdfTemplatesFromConstructorEvenWithoutLinks()
    {
        await using var context = CreateContext();
        var (_, foodTypeId, _) = await SeedDefaultInvestigationTypesAsync(context);
        await SeedActivePublishedPdfTemplateAsync(context, "0004", "Ф343 Досліджень проб харчових продуктів");

        var service = CreateService(context, branchId: null);

        var form = await service.GetCreateFormAsync();

        var option = Assert.Single(form.InvestigationTypes);
        Assert.Equal(foodTypeId, option.Id);
        Assert.Equal("Ф343 Досліджень проб харчових продуктів", option.NameUk);
        Assert.Single(form.TemplateOptions);
        Assert.Equal(option.TemplateVersionId, form.TemplateOptions[0].TemplateVersionId);
    }

    [Fact]
    public async Task CreateOrderAsync_AcceptsActivePdfTemplateMatchedByNameWithoutLink()
    {
        var branchId = Guid.NewGuid();
        await using var context = CreateContext();
        await SeedBranchesAsync(context, branchId);
        var (_, foodTypeId, _) = await SeedDefaultInvestigationTypesAsync(context);
        var versionId = await SeedActivePublishedPdfTemplateAsync(
            context,
            "0004",
            "Ф343 Досліджень проб харчових продуктів");
        var customer = await SeedCustomerAsync(context, "Харчовий клієнт");
        var service = CreateService(context, branchId);

        var result = await service.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = customer.Id,
            InvestigationTypeId = foodTypeId,
            Documents =
            [
                new CreateOrderDocumentRequest { TemplateVersionId = versionId, TargetBranchId = branchId }
            ]
        });

        Assert.Single(result.Documents);
        Assert.Equal(versionId, result.Documents[0].TemplateVersionId);
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

    private static async Task<(Guid WaterTypeId, Guid FoodTypeId, Guid AirTypeId)> SeedDefaultInvestigationTypesAsync(
        ApplicationDbContext context)
    {
        var waterType = new InvestigationType
        {
            Code = "WATER",
            NameUk = "Дослідження води",
            SortOrder = 1
        };
        var foodType = new InvestigationType
        {
            Code = "FOOD",
            NameUk = "Дослідження харчових продуктів",
            SortOrder = 2
        };
        var airType = new InvestigationType
        {
            Code = "INDOOR_AIR",
            NameUk = "Повітря закритих приміщень",
            SortOrder = 3
        };

        context.InvestigationTypes.AddRange(waterType, foodType, airType);
        await context.SaveChangesAsync();

        return (waterType.Id, foodType.Id, airType.Id);
    }

    private static async Task<Guid> SeedActivePublishedPdfTemplateAsync(
        ApplicationDbContext context,
        string code,
        string nameUk)
    {
        var template = new Template
        {
            Code = code,
            NameUk = nameUk,
            Status = TemplateStatus.Active
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

    private static async Task<(Guid InvestigationTypeId, Guid VersionId1, Guid VersionId2)>
        SeedInvestigationTypeWithTwoPdfTemplatesAsync(ApplicationDbContext context)
    {
        var investigationTypeId = await SeedInvestigationTypeWithPdfTemplateAsync(context);

        var versionId1 = await (
                from link in context.InvestigationTypeTemplates.AsNoTracking()
                join template in context.Templates.AsNoTracking() on link.TemplateId equals template.Id
                where link.InvestigationTypeId == investigationTypeId
                select template.CurrentPublishedVersionId!.Value)
            .FirstAsync();

        var template2 = new Template
        {
            Code = "TST-TPL-2",
            NameUk = "Другий шаблон",
            Status = TemplateStatus.Active
        };
        context.Templates.Add(template2);

        var version2 = new TemplateVersion
        {
            TemplateId = template2.Id,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            StorageKey = "test2.pdf"
        };
        context.TemplateVersions.Add(version2);
        template2.CurrentPublishedVersionId = version2.Id;

        context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
        {
            InvestigationTypeId = investigationTypeId,
            TemplateId = template2.Id,
            SortOrder = 2
        });

        await context.SaveChangesAsync();
        return (investigationTypeId, versionId1, version2.Id);
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
            NameUk = "Тестовий шаблон",
            Status = TemplateStatus.Active
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
