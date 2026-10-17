using System.Net;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Api.Services.SefXml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class SefXmlReconciliationEndpointTests
{
    private const string AdminApiKey = "sef-pilot-test-admin-key";

    [Fact]
    public async Task Reconcile_RejectsUnauthorizedRequestWithoutReadingCatalogue()
    {
        await using var host = await TestHost.CreateAsync();

        using var response = await host.Client.PostAsJsonAsync("/api/pilot/sef-xml/reconcile", Request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Reader.CallCount);
    }

    [Fact]
    public async Task Reconcile_IsNotExposedWhenPilotFlagIsOff()
    {
        await using var host = await TestHost.CreateAsync(enabled: false);
        using var response = AuthorizedRequest(Request());

        using var responseMessage = await host.Client.SendAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, responseMessage.StatusCode);
        Assert.Equal(0, host.Reader.CallCount);
    }

    [Fact]
    public async Task Reconcile_RejectsUnknownDataScopeWithoutReadingCatalogue()
    {
        await using var host = await TestHost.CreateAsync();
        using var request = AuthorizedRequest(Request(dataScope: "outside-authorized-scope"));

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Reader.CallCount);
    }

    [Fact]
    public async Task Reconcile_ReturnsReadOnlyPreviewAndDoesNotCreateReceivedQuantity()
    {
        await using var host = await TestHost.CreateAsync(
            new SefXmlCatalogReadResult([new SefXmlCatalogArticle(-7, "SUP-42", "Čizma 42")], IsTruncated: false));
        using var request = AuthorizedRequest(Request());

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isReadOnlyPreview\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"canPostInventory\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"selectedCatalogArticleId\":-7", body, StringComparison.Ordinal);
        Assert.Contains("\"physicallyReceivedQuantity\":null", body, StringComparison.Ordinal);
        Assert.Contains("\"accessReceiptVerificationStatus\":0", body, StringComparison.Ordinal);
        Assert.Equal(1, host.Reader.CallCount);
    }

    [Fact]
    public async Task Reconcile_HidesReaderFailureAndDoesNotLeakSourceDocumentData()
    {
        await using var host = await TestHost.CreateAsync(readerException: new InvalidOperationException("DATABASE=SENSITIVE"));
        using var request = AuthorizedRequest(Request());

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Katalog nije dostupan", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC-QDB12", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DATABASE=SENSITIVE", body, StringComparison.Ordinal);
        Assert.Equal(1, host.Reader.CallCount);
    }

    private static HttpRequestMessage AuthorizedRequest(SefXmlReconciliationRequest payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/pilot/sef-xml/reconcile")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        return request;
    }

    private static SefXmlReconciliationRequest Request(string? dataScope = "all") => new(
        Invoice: new SefXmlInvoicePreview(
            IsReadOnlyPreview: true,
            DocumentKind: SefXmlDocumentKind.Invoice,
            InvoiceNumber: "SYNTHETIC-QDB12",
            IssueDate: new DateOnly(2026, 10, 16),
            InvoiceTypeCode: "380",
            DocumentCurrencyCode: "RSD",
            Supplier: new SefXmlSupplier("Sintetički dobavljač", "109999999"),
            Lines:
            [
                new SefXmlInvoiceLine(
                    SourceOrder: 1,
                    SourceLineId: "L-1",
                    SupplierItemId: "SUP-42",
                    BuyerItemId: null,
                    StandardItemId: null,
                    Name: "Čizma 42",
                    RawUnitCode: "H87",
                    InvoicedQuantity: 2m,
                    PhysicallyReceivedQuantity: null,
                    PriceAmount: 100m,
                    PriceCurrencyCode: "RSD",
                    PriceBaseQuantity: null,
                    NetLineAmount: 200m,
                    NetLineCurrencyCode: "RSD",
                    Allowances: [],
                    PriceBasisExplanation: "Synthetic price basis.",
                    Diagnostics: [])
            ],
            Totals: new SefXmlInvoiceTotals(new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null)),
            TaxSubtotals: [],
            Diagnostics: []),
        ManualSelections: [],
        DataScope: dataScope);

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingCatalogReader reader)
        {
            App = app;
            Client = app.GetTestClient();
            Reader = reader;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingCatalogReader Reader { get; }

        public static async Task<TestHost> CreateAsync(
            SefXmlCatalogReadResult? result = null,
            Exception? readerException = null,
            bool enabled = true)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddLogging();
            builder.Configuration["Admin:ApiKey"] = AdminApiKey;
            builder.Configuration["SefXmlPilot:Enabled"] = enabled.ToString();
            var reader = new RecordingCatalogReader(result ?? new SefXmlCatalogReadResult([], IsTruncated: false), readerException);
            builder.Services.AddSingleton<ISefXmlCatalogReader>(reader);
            builder.Services.AddSingleton<SefXmlReconciliationService>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "sef-xml-reconciliation",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapSefXmlReconciliationEndpoints();
            await app.StartAsync();
            return new TestHost(app, reader);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingCatalogReader : ISefXmlCatalogReader
    {
        private readonly SefXmlCatalogReadResult _result;
        private readonly Exception? _exception;

        public RecordingCatalogReader(SefXmlCatalogReadResult result, Exception? exception)
        {
            _result = result;
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public Task<SefXmlCatalogReadResult> ReadCandidatesAsync(
            SefXmlInvoicePreview invoice,
            IReadOnlyList<SefXmlManualMatchSelection>? manualSelections,
            string dataScope,
            CancellationToken ct = default)
        {
            CallCount++;
            return _exception is null
                ? Task.FromResult(_result)
                : Task.FromException<SefXmlCatalogReadResult>(_exception);
        }
    }
}
