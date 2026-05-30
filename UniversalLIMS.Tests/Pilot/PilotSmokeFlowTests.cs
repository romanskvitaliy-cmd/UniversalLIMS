using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Expert;
using UniversalLIMS.Application.Laboratory;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;
using UniversalLIMS.Infrastructure.Expert;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Registration;

namespace UniversalLIMS.Tests.Pilot;

/// <summary>
/// C1 — end-to-end smoke на рівні сервісів: 2 Mixed-філії (BER/KOR), ізоляція експертів, rework, видача.
/// UI-перевірку poll/toast див. docs/pilot-smoke-c1-checklist.md.
/// </summary>
public sealed class PilotSmokeFlowTests
{
    private static readonly DateTime PilotUtc = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PilotSmoke_TwoMixedBranches_ExpertsIsolated_ReworkAndIssuanceNotifications()
    {
        var regBranchId = Guid.NewGuid();
        var mixBerId = Guid.NewGuid();
        var mixKorId = Guid.NewGuid();
        var sampleBerId = Guid.NewGuid();
        var sampleKorId = Guid.NewGuid();
        var docBerId = Guid.NewGuid();
        var docKorId = Guid.NewGuid();

        await using var context = await SeedPilotContextAsync(
            regBranchId,
            mixBerId,
            mixKorId,
            sampleBerId,
            sampleKorId,
            docBerId,
            docKorId);

        // --- C1 крок 1: лаборант бачить вхідну пробу своєї філії ---
        var labBerJournal = new LaboratoryJournalService(context, new FixedLaboratoryBranchContext(mixBerId));
        var labIncomingBer = await labBerJournal.GetIncomingSinceAsync(PilotUtc.AddHours(-3));
        Assert.Single(labIncomingBer);
        Assert.Equal("SMP-BER-001", labIncomingBer[0].SampleNumber);

        var labKorJournal = new LaboratoryJournalService(context, new FixedLaboratoryBranchContext(mixKorId));
        var labIncomingKor = await labKorJournal.GetIncomingSinceAsync(PilotUtc.AddHours(-3));
        Assert.Single(labIncomingKor);
        Assert.Equal("SMP-KOR-001", labIncomingKor[0].SampleNumber);

        // --- Проби готові до експертизи (lab заповнив PDF і відправив) ---
        foreach (var sample in await context.Samples.ToListAsync())
        {
            sample.Status = SampleStatus.ResultsEntered;
            sample.ResultsEnteredAtUtc = PilotUtc;
        }

        foreach (var document in await context.OrderDocuments.ToListAsync())
        {
            document.Status = OrderDocumentStatus.ResultsEntered;
            document.ResultsEnteredAtUtc = PilotUtc;
        }

        await context.SaveChangesAsync();

        // --- C1 крок 2: експерт бачить лише «свою» пробу (B1/B2) ---
        var expertQueueBer = new ExpertReviewQueueService(context, new BranchCurrentUser(mixBerId));
        var expertQueueKor = new ExpertReviewQueueService(context, new BranchCurrentUser(mixKorId));

        var queueBer = await expertQueueBer.GetQueueAsync(new ExpertReviewQueueFilter());
        var queueKor = await expertQueueKor.GetQueueAsync(new ExpertReviewQueueFilter());

        Assert.Contains(queueBer.Items, item => item.SampleId == sampleBerId);
        Assert.DoesNotContain(queueBer.Items, item => item.SampleId == sampleKorId);
        Assert.Contains(queueKor.Items, item => item.SampleId == sampleKorId);
        Assert.DoesNotContain(queueKor.Items, item => item.SampleId == sampleBerId);

        var expertNotifyBer = await expertQueueBer.GetIncomingSinceAsync(PilotUtc.AddMinutes(-5));
        var expertNotifyKor = await expertQueueKor.GetIncomingSinceAsync(PilotUtc.AddMinutes(-5));
        Assert.Single(expertNotifyBer);
        Assert.Equal(sampleBerId, expertNotifyBer[0].SampleId);
        Assert.Single(expertNotifyKor);
        Assert.Equal(sampleKorId, expertNotifyKor[0].SampleId);

        // --- Експерт BER затверджує → C1 крок 4: реєстратор отримує «готово до видачі» ---
        var conclusionService = new ExpertConclusionService(
            context,
            new BranchCurrentUser(mixBerId),
            new FixedDateTimeProvider(PilotUtc.AddMinutes(10)));

        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleBerId,
            Status = ExpertConclusionStatus.InProgress,
            ReviewStartedAtUtc = PilotUtc,
            CreatedAtUtc = PilotUtc,
            CreatedByUserId = "expert-ber"
        });
        await context.SaveChangesAsync();

        var approvedBer = await conclusionService.ApproveAsync(sampleBerId, "Відповідає нормам");
        Assert.True(approvedBer);

        var regNotify = new RegistrationNotificationService(context, new BranchCurrentUser(regBranchId));
        var readyForPickup = await regNotify.GetReadyForPickupSinceAsync(PilotUtc);
        Assert.Single(readyForPickup);
        Assert.Equal(sampleBerId, readyForPickup[0].SampleId);

        var deliveryService = new SampleDeliveryService(
            context,
            new BranchCurrentUser(regBranchId),
            new FixedDateTimeProvider(PilotUtc.AddMinutes(15)));
        var issued = await deliveryService.MarkIssuedAsync(sampleBerId);
        Assert.True(issued);

        var berSample = await context.Samples.SingleAsync(item => item.Id == sampleBerId);
        Assert.Equal(SampleDeliveryStatus.Issued, berSample.DeliveryStatus);

        // --- Експерт KOR повертає на rework → лаборант KOR бачить toast-дані (C2) ---
        context.ExpertConclusionReviews.Add(new ExpertConclusionReview
        {
            SampleId = sampleKorId,
            Status = ExpertConclusionStatus.InProgress,
            ReviewStartedAtUtc = PilotUtc,
            CreatedAtUtc = PilotUtc,
            CreatedByUserId = "expert-kor"
        });
        await context.SaveChangesAsync();

        var conclusionKor = new ExpertConclusionService(
            context,
            new BranchCurrentUser(mixKorId),
            new FixedDateTimeProvider(PilotUtc.AddMinutes(20)));

        var returned = await conclusionKor.ReturnForReworkAsync(sampleKorId, "Уточнити формулювання pH");
        Assert.True(returned);

        var queueKorAfterRework = await expertQueueKor.GetQueueAsync(new ExpertReviewQueueFilter());
        Assert.DoesNotContain(queueKorAfterRework.Items, item => item.SampleId == sampleKorId);

        var reworkAt = PilotUtc.AddMinutes(20);
        var reworkBer = await labBerJournal.GetReworkSinceAsync(reworkAt.AddMinutes(-5));
        var reworkKor = await labKorJournal.GetReworkSinceAsync(reworkAt.AddMinutes(-5));

        Assert.Empty(reworkBer);
        Assert.Single(reworkKor);
        Assert.Equal("SMP-KOR-001", reworkKor[0].SampleNumber);
        Assert.Equal("Уточнити формулювання pH", reworkKor[0].ReworkReasonUk);

        var korSample = await context.Samples.SingleAsync(item => item.Id == sampleKorId);
        Assert.Equal(SampleStatus.InProgress, korSample.Status);
        var korDoc = await context.OrderDocuments.SingleAsync(item => item.Id == docKorId);
        Assert.Equal(OrderDocumentStatus.InProgress, korDoc.Status);
    }

    private static async Task<ApplicationDbContext> SeedPilotContextAsync(
        Guid regBranchId,
        Guid mixBerId,
        Guid mixKorId,
        Guid sampleBerId,
        Guid sampleKorId,
        Guid docBerId,
        Guid docKorId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new ApplicationDbContext(options);

        context.Branches.AddRange(
            new Branch
            {
                Id = regBranchId,
                Code = "REG-ZHY",
                Name = "Реєстратура Житомир",
                City = "Житомир",
                Kind = BranchKind.Registration,
                IsActive = true
            },
            new Branch
            {
                Id = mixBerId,
                Code = "MIX-BER",
                Name = "Бердичівський підрозділ",
                City = "Бердичів",
                Kind = BranchKind.Mixed,
                IsActive = true
            },
            new Branch
            {
                Id = mixKorId,
                Code = "MIX-KOR",
                Name = "Корostenський підрозділ",
                City = "Корosten",
                Kind = BranchKind.Mixed,
                IsActive = true
            });

        var customerId = Guid.NewGuid();
        context.Customers.Add(new Customer { Id = customerId, FullName = "Пілотний замовник" });

        var investigationTypeId = Guid.NewGuid();
        context.InvestigationTypes.Add(new InvestigationType
        {
            Id = investigationTypeId,
            Code = "FOOD",
            NameUk = "Харчові продукти",
            IsActive = true
        });

        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        context.Templates.Add(new Template
        {
            Id = templateId,
            Code = "F343",
            NameUk = "Протокол",
            Status = TemplateStatus.Active,
            Purpose = TemplatePurpose.Protocol,
            CurrentPublishedVersionId = versionId
        });
        context.TemplateVersions.Add(new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            DocumentFormat = TemplateDocumentFormat.Pdf,
            OriginalFileName = "p.pdf",
            StorageKey = "k",
            ContentType = "application/pdf",
            FileSizeBytes = 1,
            Sha256Hash = new string('a', 64),
            UploadedAtUtc = PilotUtc
        });

        var orderId = Guid.NewGuid();
        context.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BranchId = regBranchId,
            ReferralNumber = "REF-ZHY-2026-0001",
            Status = OrderStatus.Registered,
            RegisteredAtUtc = PilotUtc.AddDays(-1)
        });

        context.Samples.AddRange(
            new Sample
            {
                Id = sampleBerId,
                OrderId = orderId,
                InvestigationTypeId = investigationTypeId,
                Number = "SMP-BER-001",
                Status = SampleStatus.Routed,
                RegisteredAt = PilotUtc.AddDays(-1),
                RoutedAtUtc = PilotUtc.AddHours(-2)
            },
            new Sample
            {
                Id = sampleKorId,
                OrderId = orderId,
                InvestigationTypeId = investigationTypeId,
                Number = "SMP-KOR-001",
                Status = SampleStatus.Routed,
                RegisteredAt = PilotUtc.AddDays(-1),
                RoutedAtUtc = PilotUtc.AddHours(-2)
            });

        context.OrderDocuments.AddRange(
            new OrderDocument
            {
                Id = docBerId,
                OrderId = orderId,
                SampleId = sampleBerId,
                TemplateId = templateId,
                TemplateVersionId = versionId,
                TargetBranchId = mixBerId,
                Status = OrderDocumentStatus.SentToLab
            },
            new OrderDocument
            {
                Id = docKorId,
                OrderId = orderId,
                SampleId = sampleKorId,
                TemplateId = templateId,
                TemplateVersionId = versionId,
                TargetBranchId = mixKorId,
                Status = OrderDocumentStatus.SentToLab
            });

        await context.SaveChangesAsync();
        return context;
    }

    private sealed class BranchCurrentUser(Guid branchId) : ICurrentUserService
    {
        public string? UserId => "pilot-user";

        public string? UserName => "pilot";

        public string? UserFullName => "Pilot User";

        public Guid? BranchId => branchId;

        public string? IpAddress => "127.0.0.1";

        public string? UserAgent => "tests";

        public string? CorrelationId => "pilot-smoke";

        public bool IsAuthenticated => true;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedLaboratoryBranchContext(Guid? branchId) : ILaboratoryBranchContext
    {
        public Task<LaboratoryBranchContextState> GetStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LaboratoryBranchContextState { ActiveBranchId = branchId });

        public Task SetSelectedBranchAsync(Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
