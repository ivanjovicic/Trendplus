using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Integration tests for the shoe-type-sales-stats endpoint.
/// These tests verify the endpoint against a real database with shoe-type seed data.
/// </summary>
[Trait("Category", "Integration")]
public class AnalyticsShoeTypeSalesIntegrationTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly bool _integrationEnabled;

    public AnalyticsShoeTypeSalesIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
        _integrationEnabled = string.Equals(
            Environment.GetEnvironmentVariable("TRENDPLUS_RUN_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "ShoeType endpoint returns valid JSON structure")]
    public async Task ShoeTypeSalesStats_ReturnsValidJsonStructure()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31");

        Assert.True(root.ValueKind == JsonValueKind.Object, "Response should be an object with shoeTypes/totals/dataQuality");
        Assert.True(root.TryGetProperty("shoeTypes", out _), "Missing 'shoeTypes' field");
        Assert.True(root.TryGetProperty("totals", out _), "Missing 'totals' field");
        Assert.True(root.TryGetProperty("dataQuality", out _), "Missing 'dataQuality' field");

        var shoeTypes = root.GetProperty("shoeTypes");
        Assert.Equal(JsonValueKind.Array, shoeTypes.ValueKind);

        if (shoeTypes.GetArrayLength() > 0)
        {
            var firstItem = shoeTypes[0];
            Assert.True(firstItem.TryGetProperty("tipObuceNaziv", out _), "Missing 'tipObuceNaziv' field");
            Assert.True(firstItem.TryGetProperty("ukupanPromet", out _), "Missing 'ukupanPromet' field");
            Assert.True(firstItem.TryGetProperty("ukupnaKolicina", out _), "Missing 'ukupnaKolicina' field");
            Assert.True(firstItem.TryGetProperty("recommendation", out _), "Missing 'recommendation' field");
        }
    }

    [Fact(DisplayName = "ShoeType endpoint matches golden snapshot")]
    public async Task ShoeTypeSalesStats_MatchesGoldenSnapshot()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-06-01&toDate=2026-08-31");
        GoldenSnapshotAssert.Matches("shoe-type-sales-stats.contract.json", ProjectSnapshot(root));
    }

    [Fact(DisplayName = "Invalid season returns not found")]
    public async Task ShoeTypeSalesStats_InvalidSeason_ReturnsNotFound()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/shoe-type-sales-stats?sezonaId=999999");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Invalid date range returns bad request")]
    public async Task ShoeTypeSalesStats_InvalidPeriod_ReturnsBadRequest()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-06-01");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Invalid date range returns bad request on in-memory host")]
    public async Task ShoeTypeSalesStats_InvalidPeriod_ReturnsBadRequest_InMemory()
    {
        await using var factory = new ShoeTypeSalesInMemoryFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-06-01");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        using var doc = JsonDocument.Parse(body);
        Assert.False(
            doc.RootElement.TryGetProperty("meta", out var meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.True);
    }

    [Fact(DisplayName = "ShoeType endpoint correctly aggregates 'Nepoznato' for null/empty types")]
    public async Task ShoeTypeSalesStats_AggregatesUnknownTypes()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31");
        var items = root.GetProperty("shoeTypes").EnumerateArray().ToList();
        var unknownItems = items.Where(i => i.GetProperty("tipObuceNaziv").GetString() == "Nepoznato").ToList();

        // There should be exactly one 'Nepoznato' entry even if multiple articles have null/empty IDTipObuce
        Assert.Single(unknownItems);
        Assert.True(unknownItems[0].GetProperty("ukupanPromet").GetDecimal() > 0m);
    }

    [Fact(DisplayName = "ShoeType endpoint filters by storeId correctly")]
    public async Task ShoeTypeSalesStats_FiltersByStoreId()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/shoe-type-sales-stats?storeId=2&fromDate=2026-01-01&toDate=2026-12-31");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(content).RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(0, root.GetProperty("shoeTypes").GetArrayLength());
    }

    [Fact(DisplayName = "ShoeType endpoint includes Margin metrics")]
    public async Task ShoeTypeSalesStats_IncludesMarginMetrics()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31");
        var shoeTypes = root.GetProperty("shoeTypes");

        if (shoeTypes.GetArrayLength() > 0)
        {
            var firstItem = shoeTypes[0];
            Assert.True(firstItem.TryGetProperty("marginPct", out _), "Missing 'marginPct' field");
            Assert.True(firstItem.TryGetProperty("marginContribution", out _), "Missing 'marginContribution' field");
            Assert.True(firstItem.TryGetProperty("sharePct", out _), "Missing 'sharePct' field");
        }
    }

    [Fact(DisplayName = "ShoeType endpoint includes Nivelacija split metrics")]
    public async Task ShoeTypeSalesStats_IncludesNivelacijaSplitMetrics()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31");
        var shoeTypes = root.GetProperty("shoeTypes");

        if (shoeTypes.GetArrayLength() > 0)
        {
            var firstItem = shoeTypes[0];
            Assert.True(firstItem.TryGetProperty("preNivelacijePromet", out _), "Missing 'preNivelacijePromet' field");
            Assert.True(firstItem.TryGetProperty("posleNivelacijePromet", out _), "Missing 'posleNivelacijePromet' field");
            Assert.True(firstItem.TryGetProperty("brojArtikalaSaNivelacijom", out _), "Missing 'brojArtikalaSaNivelacijom' field");
            Assert.True(firstItem.TryGetProperty("recommendation", out _), "Missing 'recommendation' field");
        }
    }

    [Fact(DisplayName = "Data scope filters imported and existing rows")]
    public async Task ShoeTypeSalesStats_DataScopeFiltersRows()
    {
        if (!_integrationEnabled) return;

        var allRoot = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31&dataScope=all");
        var existingRoot = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31&dataScope=existing");
        var importedRoot = await GetJsonRootAsync("/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31&dataScope=imported");

        Assert.NotEmpty(allRoot.GetProperty("shoeTypes").EnumerateArray());
        Assert.NotEmpty(existingRoot.GetProperty("shoeTypes").EnumerateArray());
        Assert.NotEmpty(importedRoot.GetProperty("shoeTypes").EnumerateArray());

        Assert.Contains(importedRoot.GetProperty("shoeTypes").EnumerateArray(), item => item.GetProperty("tipObuceNaziv").GetString() == "Sandale");
        Assert.DoesNotContain(importedRoot.GetProperty("shoeTypes").EnumerateArray(), item => item.GetProperty("tipObuceNaziv").GetString() == "Patike");
        Assert.DoesNotContain(existingRoot.GetProperty("shoeTypes").EnumerateArray(), item => item.GetProperty("tipObuceNaziv").GetString() == "Sandale");
    }

    [Fact(DisplayName = "Endpoint is deterministic for same inputs")]
    public async Task ShoeTypeSalesStats_ProducesDeterministicJson()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var url = "/api/analytics/shoe-type-sales-stats?fromDate=2026-01-01&toDate=2026-12-31";

        var first = await client.GetAsync(url);
        var second = await client.GetAsync(url);

        Assert.Equal(CanonicalizeJson(await first.Content.ReadAsStringAsync()), CanonicalizeJson(await second.Content.ReadAsStringAsync()));
    }

    private async Task<JsonElement> GetJsonRootAsync(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);

        Assert.True(response.IsSuccessStatusCode, $"Expected success for {url}, got {response.StatusCode}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));

        return JsonDocument.Parse(content).RootElement;
    }

    private static string CanonicalizeJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return json;
        }
    }

    private static object ProjectSnapshot(JsonElement root)
    {
        var shoeTypesRoot = root.GetProperty("shoeTypes");
        var shoeTypes = shoeTypesRoot.EnumerateArray()
            .Select(item => new
            {
                tipObuceNaziv = item.GetProperty("tipObuceNaziv").GetString(),
                ukupanPromet = item.GetProperty("ukupanPromet").GetDecimal(),
                ukupnaKolicina = item.GetProperty("ukupnaKolicina").GetInt32(),
                sharePct = Math.Round(item.GetProperty("sharePct").GetDouble(), 2),
                isUnknown = item.GetProperty("tipObuceNaziv").GetString() == "Nepoznato"
            })
            .ToList();

        var totals = root.GetProperty("totals");
        var dataQuality = root.GetProperty("dataQuality");

        return new
        {
            shoeTypes,
            totals = new
            {
                ukupanPromet = totals.GetProperty("ukupanPromet").GetDecimal(),
                ukupnaKolicina = totals.GetProperty("ukupnaKolicina").GetInt32(),
                brojTipovaObuce = totals.GetProperty("brojTipovaObuce").GetInt32(),
                recommendationSummary = new
                {
                    increaseFocus = totals.GetProperty("recommendationSummary").GetProperty("increaseFocus").GetInt32(),
                    maintain = totals.GetProperty("recommendationSummary").GetProperty("maintain").GetInt32(),
                    review = totals.GetProperty("recommendationSummary").GetProperty("review").GetInt32(),
                    doNotTrust = totals.GetProperty("recommendationSummary").GetProperty("doNotTrust").GetInt32(),
                    insufficientData = totals.GetProperty("recommendationSummary").GetProperty("insufficientData").GetInt32()
                }
            },
            dataQuality = new
            {
                unknownTypeRevenueSharePct = Math.Round(dataQuality.GetProperty("unknownTypeRevenueSharePct").GetDouble(), 2)
            }
        };
    }

    private sealed class ShoeTypeSalesInMemoryFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _databaseName = $"shoe-type-sales-invalid-period-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();

                services.AddDbContextFactory<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddDbContext<TrendplusDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
                services.AddScoped<ITrendplusDbContext>(sp =>
                    sp.GetRequiredService<TrendplusDbContext>());
            });
        }
    }
}
