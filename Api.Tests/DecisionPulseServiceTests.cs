using System.Data.Common;
using Application.Analytics.DecisionPulse;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Api.Services.Analytics;
using Domain.Model;
using Domain.Model.Analytics;
using Domain.Model.Documents;
using Domain.Model.Prodaja;
using Domain.Model.Povracaj;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class DecisionPulseServiceTests
{
    [Fact]
    public void NormalizePeriod_UsesInclusiveWholeDayBoundariesForRequestedDates()
    {
        var (from, to) = DecisionPulseService.NormalizePeriod(
            new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 20, 2, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1), to);
    }

    [Fact]
    public async Task GetFeedAsync_RejectsUnknownScopeInsteadOfBroadeningToAll()
    {
        var (service, _) = CreateService([]);
        var response = await service.GetFeedAsync(null, null, null, null, "private-scope-code", CancellationToken.None);

        Assert.False(response.Meta.Success);
        Assert.Equal("unsupported_data_scope", response.Meta.ErrorCode);
        Assert.Equal("private-scope-code", response.Meta.RequestedDataScope);
        Assert.Null(response.Meta.EffectiveDataScope);
        Assert.Contains("nije proširen", response.Meta.ErrorMessage);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, null)]
    [InlineData(null, -1)]
    [InlineData(null, 0)]
    public async Task GetFeedAsync_RejectsInvalidStoreOrSupplierIds(int? storeId, int? supplierId)
    {
        var (service, _) = CreateService([]);
        var response = await service.GetFeedAsync(null, null, storeId, supplierId, "imported", CancellationToken.None);
        Assert.False(response.Meta.Success);
        Assert.Equal("invalid_filter_id", response.Meta.ErrorCode);
        Assert.Null(response.Meta.EffectiveDataScope);
        Assert.Empty(response.Items);
        Assert.Contains("nije proširen", response.Meta.ErrorMessage);
    }

    [Fact]
    public void ToResponse_ReportsRequestedAndEffectiveFiltersAndUnsupportedInventoryPeriod()
    {
        var projection = new DecisionPulseProjection(
            true,
            null,
            null,
            Array.Empty<DecisionPulseItem>(),
            0,
            DecisionPulseProjector.DedicatedTenantScope);
        var requestedFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var requestedTo = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = requestedTo.Date.AddDays(1).AddTicks(-1);

        var response = DecisionPulseService.ToResponse(
            projection,
            requestedFrom,
            effectiveTo,
            null,
            [],
            [],
            null,
            null,
            dataScope: "imported",
            requestedPeriodFromUtc: requestedFrom,
            requestedPeriodToUtc: requestedTo,
            inventoryPeriodNotApplied: true,
            suppressInventoryForRequestedPeriod: true);

        Assert.Equal(requestedFrom, response.Meta.RequestedPeriodFromUtc);
        Assert.Equal(requestedTo, response.Meta.RequestedPeriodToUtc);
        Assert.Equal(requestedFrom, response.Meta.EffectivePeriodFromUtc);
        Assert.Equal(effectiveTo, response.Meta.EffectivePeriodToUtc);
        Assert.Equal("imported", response.Meta.RequestedDataScope);
        Assert.Equal("imported", response.Meta.EffectiveDataScope);
        Assert.Equal(["period:inventory"], response.Meta.NotAppliedDimensions);
        Assert.Equal(["inventory"], response.Meta.SuppressedSources);
        Assert.True(response.Meta.IsPartial);
        Assert.Equal("PULSE_FILTER_NOT_APPLIED", response.Meta.WarningCode);
    }

    [Fact]
    public void BuildResponseMeta_MarksEmptyProjectionAsPartialWhenSomeSourcesFailed()
    {
        var projection = new DecisionPulseProjection(
            true,
            null,
            null,
            Array.Empty<DecisionPulseItem>(),
            0,
            DecisionPulseProjector.DedicatedTenantScope);

        var meta = DecisionPulseService.BuildResponseMeta(
            projection,
            generatedAtUtc: DateTime.UtcNow,
            sourceFailures: ["supplier_source_unavailable"],
            sourceFailureMessages: ["Supplier decision hub nije dostupan."]);

        Assert.True(meta.Success);
        Assert.True(meta.IsPartial);
        Assert.Equal("no_pulse_items", meta.EmptyReason);
        Assert.Equal("PULSE_PARTIAL", meta.WarningCode);
        Assert.Equal("Supplier decision hub nije dostupan.", meta.WarningMessage);
        Assert.Contains("Nema Decision Pulse izuzetaka", meta.Message);
        Assert.Contains("Supplier decision hub nije dostupan.", meta.Message);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsSourceError_WhenFeedIsNotSuccessful()
    {
        var (service, emailService) = CreateService(Array.Empty<string>());
        var feed = CreateFeed(success: false);

        var result = await service.SendEmailAsync(feed, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal("source_error", result.FailureCategory);
        Assert.Equal(0, result.RecipientCount);
        Assert.Equal(feed.Items.Count, result.ItemCount);
        Assert.Equal(0, emailService.SendCount);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsRecipientsMissing_WhenNoRecipientsAreConfigured()
    {
        var (service, emailService) = CreateService(Array.Empty<string>());
        var feed = CreateFeed(success: true);

        var result = await service.SendEmailAsync(feed, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal("recipients_missing", result.FailureCategory);
        Assert.Equal(0, result.RecipientCount);
        Assert.Equal(feed.Items.Count, result.ItemCount);
        Assert.Equal(0, emailService.SendCount);
    }

    [Fact]
    public async Task SendEmailAsync_ReturnsSmtpDisabledWithoutSending()
    {
        var (service, emailService) = CreateService(["ops@trendplus.test"], enabled: false);
        var feed = CreateFeed(success: true);

        var result = await service.SendEmailAsync(feed, CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal("smtp_disabled", result.FailureCategory);
        Assert.Equal(0, result.RecipientCount);
        Assert.Equal(feed.Items.Count, result.ItemCount);
        Assert.Equal(0, emailService.SendCount);
    }

    [Fact]
    public async Task SendEmailAsync_SendsEmailWhenRecipientsAndSMTPAreAvailable()
    {
        var (service, emailService) = CreateService(["ops@trendplus.test", "ops2@trendplus.test"]);
        var feed = CreateFeed(success: true, items: [CreateItem()]);

        var result = await service.SendEmailAsync(feed, CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Null(result.FailureCategory);
        Assert.Equal(2, result.RecipientCount);
        Assert.Equal(1, result.ItemCount);
        Assert.Equal("Poslato na 2 primalaca.", result.Message);
        Assert.Equal(1, emailService.SendCount);
        Assert.NotNull(emailService.LastMessage);
        Assert.Equal(["ops@trendplus.test", "ops2@trendplus.test"], emailService.LastMessage!.To);
        Assert.Contains("SKU-100", emailService.LastMessage!.HtmlBody);
        Assert.Contains("Patika za test", emailService.LastMessage!.HtmlBody);
        Assert.Contains("Decision Pulse", emailService.LastMessage!.Subject);
    }

    private static (DecisionPulseService Service, RecordingEmailService EmailService) CreateService(
        IReadOnlyList<string> recipients,
        bool enabled = true)
    {
        var emailService = new RecordingEmailService(enabled);
        var options = Options.Create(new DecisionPulseOptions { Recipients = recipients.ToArray() });
        var configuration = new ConfigurationBuilder().Build();

        var service = new DecisionPulseService(
            new NoopTrendplusDbContext(),
            new NoopAnalyticsDbContext(),
            new NoopAnalyticsCacheService(),
            new NoopInventoryActionDecisionService(),
            emailService,
            configuration,
            options);

        return (service, emailService);
    }

    private static DecisionPulseResponseDto CreateFeed(bool success, IReadOnlyList<DecisionPulseItemDto>? items = null)
    {
        var generatedAtUtc = new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc);
        return new DecisionPulseResponseDto(
            GeneratedAtUtc: generatedAtUtc,
            PeriodFromUtc: generatedAtUtc.Date.AddDays(-7),
            PeriodToUtc: generatedAtUtc.Date,
            TenantScope: DecisionPulseProjector.DedicatedTenantScope,
            SuppressedCount: 0,
            Items: items ?? [],
            Meta: new DecisionPulseResponseMetaDto
            {
                Success = success,
                GeneratedAtUtc = generatedAtUtc,
                Message = success ? "ok" : "feed unavailable",
                ErrorCode = success ? null : "source_error",
                ErrorMessage = success ? null : "feed unavailable"
            });
    }

    private static DecisionPulseItemDto CreateItem()
    {
        var generatedAtUtc = new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc);
        return new DecisionPulseItemDto(
            Id: "pulse-1",
            SourceType: DecisionPulseProjector.SourceTypeProduct,
            SourceKey: "100",
            Title: "SKU-100 — Patika za test",
            WhySummary: "Signal je dovoljno jak za prikaz.",
            ReasonCodes: ["test_reason"],
            RecommendationStatus: "REPLENISH",
            RecommendationLabel: "Dopuni",
            DataQualityStatus: "good",
            InputFreshnessStatus: "fresh",
            DeepLink: "/analytics/products",
            GeneratedAtUtc: generatedAtUtc,
            TenantScope: DecisionPulseProjector.DedicatedTenantScope);
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public RecordingEmailService(bool enabled)
        {
            IsEnabled = enabled;
        }

        public bool IsEnabled { get; }

        public int SendCount { get; private set; }

        public EmailMessage? LastMessage { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            SendCount++;
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopTrendplusDbContext : ITrendplusDbContext
    {
        public DbSet<Artikli> Artikli => null!;
        public DbSet<ProductImage> ProductImages => null!;
        public DbSet<TipObuce> TipoviObuce => null!;
        public DbSet<Dobavljac> Dobavljaci => null!;
        public DbSet<CreatedIdDto> CreatedIds => null!;
        public DbSet<DnevnikPromena> DnevnikPromena => null!;
        public DbSet<Sezona> Sezone => null!;
        public DbSet<OutboxMessage> OutboxMessages => null!;
        public DbSet<DocumentRecord> Documents => null!;
        public DbSet<DocumentTemplate> DocumentTemplates => null!;
        public DbSet<DocumentAudit> DocumentAudits => null!;
        public DbSet<ProdajaZaglavlje> ProdajaZaglavlja => null!;
        public DbSet<ProdajaStavka> ProdajaStavke => null!;
        public DbSet<PovracajZaglavlje> PovracajZaglavlja => null!;
        public DbSet<PovracajStavka> PovracajStavke => null!;
        public DatabaseFacade Database => null!;
        public DbConnection GetDbConnection() => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class NoopAnalyticsDbContext : IAnalyticsDbContext
    {
        public DbSet<ProductsDim> ProductsDim => null!;
        public DbSet<StoresDim> StoresDim => null!;
        public DbSet<PerformanceLog> PerformanceLogs => null!;
        public DbSet<SalesFact> SalesFacts => null!;
        public DbSet<SalesLineFact> SalesLineFacts => null!;
        public DbSet<SuppliersDim> SuppliersDim => null!;
        public DbSet<SeasonsDim> SeasonsDim => null!;
        public DbSet<FootwearTypesDim> FootwearTypesDim => null!;
        public DbSet<InventoryMovementFact> InventoryMovementFacts => null!;
        public DbSet<ReturnFact> ReturnFacts => null!;
        public DbSet<TrendProductSnapshot> TrendProductSnapshots => null!;
        public DbSet<TrendProductMomentum> TrendProductMomentums => null!;
        public DbSet<TrendplusIndexRecord> TrendplusIndexRecords => null!;
        public DbSet<InventoryRecommendation> InventoryRecommendations => null!;
        public DbSet<AnalyticsActionItem> AnalyticsActionItems => null!;
        public DbSet<AnalyticsActionNote> AnalyticsActionNotes => null!;
        public DbConnection GetDbConnection() => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class NoopAnalyticsCacheService : IAnalyticsCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public bool IsRedisAvailable => false;

        public bool IsRedisEnabled => false;

        public void SetRedisEnabled(bool enabled)
        {
        }

        public CacheFootprintSnapshot GetFootprintSnapshot() => new("disabled", false, false, 0);
    }

    private sealed class NoopInventoryActionDecisionService : IInventoryActionDecisionService
    {
        public Task<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>>(new Dictionary<string, InventoryActionDecisionDefinition>());

        public Task<InventoryActionDecisionDefinition> UpsertAsync(InventoryActionDecisionUpsertRequest request, CancellationToken ct = default)
            => Task.FromException<InventoryActionDecisionDefinition>(new NotSupportedException("Not used in DecisionPulse send-email tests."));
    }
}
