using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Integration tests for the supplier-sales-stats endpoint.
/// These tests verify the endpoint against a real database with seed data.
/// </summary>
[Trait("Category", "Integration")]
public class AnalyticsSupplierSalesIntegrationTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;
    private readonly bool _integrationEnabled;

    public AnalyticsSupplierSalesIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
        _integrationEnabled = string.Equals(
            Environment.GetEnvironmentVariable("TRENDPLUS_RUN_INTEGRATION_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Endpoint returns valid JSON with required fields")]
    public async Task SupplierSalesStats_ReturnsValidJsonWithAllFields()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");

        Assert.True(root.TryGetProperty("suppliers", out _), "Missing 'suppliers' field");
        Assert.True(root.TryGetProperty("totals", out _), "Missing 'totals' field");
        Assert.True(root.TryGetProperty("dataQuality", out _), "Missing 'dataQuality' field");
        Assert.True(root.TryGetProperty("generatedAt", out _), "Missing 'generatedAt' field");
        Assert.Equal(JsonValueKind.Array, root.GetProperty("suppliers").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("totals").ValueKind);
    }

    [Fact(DisplayName = "Supplier endpoint matches golden snapshot")]
    public async Task SupplierSalesStats_MatchesGoldenSnapshot()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        GoldenSnapshotAssert.Matches("supplier-sales-stats.contract.json", ProjectSnapshot(root));
    }

    [Fact(DisplayName = "Invalid season returns not found")]
    public async Task SupplierSalesStats_InvalidSeason_ReturnsNotFound()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/supplier-sales-stats?sezonaId=999999");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Supplier metrics calculate correctly against fixture")]
    public async Task SupplierSalesStats_MetricsMatchFixtureValues()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToList();
        Assert.NotEmpty(suppliers);

        var supplierA = suppliers.Single(s => s.GetProperty("dobavljacNaziv").GetString() == "Supplier A");
        Assert.Equal(4650m, supplierA.GetProperty("ukupanPromet").GetDecimal());
        Assert.True(supplierA.GetProperty("marginPct").GetDouble() > 0d);
        Assert.True(supplierA.TryGetProperty("recommendation", out var recEl));
        Assert.True(recEl.TryGetProperty("status", out _));
        Assert.True(supplierA.GetProperty("sharePct").GetDouble() > 0d);
    }

    [Fact(DisplayName = "Data scope filters existing and imported rows")]
    public async Task SupplierSalesStats_DataScopeFiltersRows()
    {
        if (!_integrationEnabled) return;

        var allRoot = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1&dataScope=all");
        var existingRoot = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1&dataScope=existing");
        var importedRoot = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1&dataScope=imported");

        var allSuppliers = allRoot.GetProperty("suppliers").EnumerateArray().ToList();
        var existingSuppliers = existingRoot.GetProperty("suppliers").EnumerateArray().ToList();
        var importedSuppliers = importedRoot.GetProperty("suppliers").EnumerateArray().ToList();

        Assert.NotEmpty(allSuppliers);
        Assert.NotEmpty(existingSuppliers);
        Assert.NotEmpty(importedSuppliers);
        Assert.Contains(importedSuppliers, s => s.GetProperty("dobavljacNaziv").GetString() == "Supplier C");
        Assert.DoesNotContain(importedSuppliers, s => s.GetProperty("dobavljacNaziv").GetString() == "Supplier A");
        Assert.True(existingSuppliers.Count >= importedSuppliers.Count);
        Assert.NotEqual(
            existingRoot.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal(),
            importedRoot.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
    }

    [Fact(DisplayName = "Invalid period returns bad request")]
    public async Task SupplierSalesStats_InvalidPeriod_ReturnsBadRequest()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/supplier-sales-stats?fromDate=2026-03-10&toDate=2026-03-01");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Supplier SharePct invariant: all shares sum to 100%")]
    public async Task SupplierSalesStats_SharesSumTo100()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        var suppliers = root.GetProperty("suppliers").EnumerateArray();
        double totalShare = 0;
        foreach (var s in suppliers)
        {
            if (s.TryGetProperty("sharePct", out var shareEl))
            {
                totalShare += shareEl.GetDouble();
            }
        }

        Assert.InRange(totalShare, 99.9, 100.1);
    }

    [Fact(DisplayName = "Endpoint produces deterministic output for same inputs")]
    public async Task SupplierSalesStats_ProducesDeterministicJson()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var url = "/api/analytics/supplier-sales-stats?sezonaId=1";

        // First request
        var response1 = await client.GetAsync(url);
        var content1 = await response1.Content.ReadAsStringAsync();

        // Second request (same params)
        var response2 = await client.GetAsync(url);
        var content2 = await response2.Content.ReadAsStringAsync();

        var json1 = CanonicalizeJson(content1);
        var json2 = CanonicalizeJson(content2);

        Assert.Equal(json1, json2);
    }

    [Fact(DisplayName = "Supplier sum invariant: components equal totals")]
    public async Task SupplierSalesStats_SupplierSumEqualsTotal()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        var suppliersElement = root.GetProperty("suppliers");
        var totalsElement = root.GetProperty("totals");

        decimal supplierSum = 0;
        foreach (var supplier in suppliersElement.EnumerateArray())
        {
            if (supplier.TryGetProperty("ukupanPromet", out var revenueElement) &&
                revenueElement.TryGetDecimal(out var revenueDec))
            {
                supplierSum += revenueDec;
            }
        }

        if (totalsElement.TryGetProperty("ukupanPromet", out var totalsRevenueElement) &&
           totalsRevenueElement.TryGetDecimal(out var totalsRevenue))
        {
            decimal tolerance = 0.01m;
            Assert.True(Math.Abs(supplierSum - totalsRevenue) <= tolerance,
                $"Supplier sum {supplierSum} does not equal totals {totalsRevenue} within tolerance {tolerance}");
        }
    }

    [Fact(DisplayName = "Unknown suppliers map to 'Nepoznato'")]
    public async Task SupplierSalesStats_UnknownSuppliersNormalized()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToList();

        var unknownSuppliers = suppliers
            .Where(s => s.TryGetProperty("dobavljacNaziv", out var nameEl) && nameEl.GetString() == "Nepoznato")
            .ToList();

        Assert.Single(unknownSuppliers);
        Assert.True(unknownSuppliers[0].GetProperty("ukupanPromet").GetDecimal() > 0m);
    }

    [Fact(DisplayName = "Data quality reports missing cost metadata")]
    public async Task SupplierSalesStats_DataQualityIncludesMissingCostInfo()
    {
        if (!_integrationEnabled) return;

        var root = await GetJsonRootAsync("/api/analytics/supplier-sales-stats?sezonaId=1");
        var dataQuality = root.GetProperty("dataQuality");

        Assert.True(dataQuality.TryGetProperty("missingCostRevenueSharePct", out var missingCostElement));
        Assert.True(missingCostElement.GetDouble() > 0d);
        Assert.True(dataQuality.TryGetProperty("unknownSupplierRevenueSharePct", out var unknownShareEl));
        Assert.True(unknownShareEl.GetDouble() > 0d);
    }

    [Fact(DisplayName = "Endpoint responds within acceptable time")]
    public async Task SupplierSalesStats_PerformanceWithinThreshold()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var response = await client.GetAsync("/api/analytics/supplier-sales-stats?sezonaId=1");

        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;

        Assert.True(elapsedMs < 10000, $"Endpoint took {elapsedMs}ms, expected < 10000ms");
    }

    [Fact(DisplayName = "Endpoint handles gracefully when schema is incomplete")]
    public async Task SupplierSalesStats_HandlesMissingSchemaGracefully()
    {
        if (!_integrationEnabled) return;

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/analytics/supplier-sales-stats?sezonaId=999");

        Assert.NotEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // ============================================================================
    // Helper methods
    // ============================================================================

    private static string CanonicalizeJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var options = new JsonSerializerOptions { WriteIndented = false };
            return JsonSerializer.Serialize(doc.RootElement, options);
        }
        catch
        {
            return json;
        }
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

    private static object ProjectSnapshot(JsonElement root)
    {
        var suppliers = root.GetProperty("suppliers")
            .EnumerateArray()
            .Select(supplier => new
            {
                dobavljacNaziv = supplier.GetProperty("dobavljacNaziv").GetString(),
                ukupanPromet = supplier.GetProperty("ukupanPromet").GetDecimal(),
                ukupnaKolicina = supplier.GetProperty("ukupnaKolicina").GetInt32(),
                sharePct = Math.Round(supplier.GetProperty("sharePct").GetDouble(), 2),
                isUnknown = supplier.GetProperty("dobavljacNaziv").GetString() == "Nepoznato"
            })
            .ToList();

        var totals = root.GetProperty("totals");
        return new
        {
            suppliers,
            totals = new
            {
                ukupanPromet = totals.GetProperty("ukupanPromet").GetDecimal(),
                ukupnaKolicina = totals.GetProperty("ukupnaKolicina").GetInt32(),
                brojDobavljaca = totals.GetProperty("brojDobavljaca").GetInt32(),
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
                unknownSupplierRevenueSharePct = Math.Round(root.GetProperty("dataQuality").GetProperty("unknownSupplierRevenueSharePct").GetDouble(), 2)
            }
        };
    }
}
