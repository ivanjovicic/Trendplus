using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

public sealed class SefXmlPreviewEndpointTests
{
    private const string AdminApiKey = "sef-pilot-test-admin-key";

    [Fact]
    public async Task PostPreview_RejectsUnauthorizedRequestWithoutInvokingParser()
    {
        await using var host = await TestHost.CreateAsync();

        using var response = await host.Client.PostAsync("/api/pilot/sef-xml/preview", XmlFile("invoice.xml", "<Invoice />"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Parser.CallCount);
    }

    [Fact]
    public async Task PostPreview_ReturnsReadOnlyPreviewWithoutAnyPersistenceDependency()
    {
        await using var host = await TestHost.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pilot/sef-xml/preview")
        {
            Content = XmlFile("invoice.xml", "<Invoice />")
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isReadOnlyPreview\":true", body, StringComparison.Ordinal);
        Assert.Equal(1, host.Parser.CallCount);
        Assert.DoesNotContain("access", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saved", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostPreview_RejectsOversizedFileBeforeInvokingParser()
    {
        await using var host = await TestHost.CreateAsync(maxUploadBytes: 32);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pilot/sef-xml/preview")
        {
            Content = XmlFile("invoice.xml", new string('x', 64))
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("maxUploadBytes", body, StringComparison.Ordinal);
        Assert.Equal(0, host.Parser.CallCount);
    }

    [Theory]
    [InlineData("invoice.pdf")]
    [InlineData("invoice.zip")]
    public async Task PostPreview_RejectsNonXmlNamesWithoutInvokingParser(string fileName)
    {
        await using var host = await TestHost.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/pilot/sef-xml/preview")
        {
            Content = XmlFile(fileName, "synthetic non-XML content")
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Parser.CallCount);
    }

    [Fact]
    public async Task PostPreview_IsNotExposedWhenPilotFlagIsOff()
    {
        await using var host = await TestHost.CreateAsync(enabled: false);
        using var request = AuthorizedRequest();

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, host.Parser.CallCount);
    }

    [Fact]
    public async Task PostPreview_UsesStrictRateLimit()
    {
        await using var host = await TestHost.CreateAsync(permitLimit: 1);
        using var first = AuthorizedRequest();
        using var firstResponse = await host.Client.SendAsync(first);
        firstResponse.EnsureSuccessStatusCode();

        using var second = AuthorizedRequest();
        using var secondResponse = await host.Client.SendAsync(second);

        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal(1, host.Parser.CallCount);
    }

    [Fact]
    public async Task PostPreview_ReturnsOnlySafeParserErrorDetails()
    {
        await using var host = await TestHost.CreateAsync(new SefXmlPreviewException(
            SefXmlPreviewErrorCode.MalformedXml,
            "XML dokument nije ispravan ili nije moguće bezbedno ga pročitati."));
        using var request = AuthorizedRequest();

        using var response = await host.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("MalformedXml", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invoice.xml", body, StringComparison.Ordinal);
        Assert.Equal(1, host.Parser.CallCount);
    }

    private static HttpRequestMessage AuthorizedRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/pilot/sef-xml/preview")
        {
            Content = XmlFile("invoice.xml", "<Invoice />")
        };
        request.Headers.Add("X-Admin-Key", AdminApiKey);
        return request;
    }

    private static MultipartFormDataContent XmlFile(string name, string content)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");
        multipart.Add(file, "file", name);
        return multipart;
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private TestHost(WebApplication app, RecordingParser parser)
        {
            App = app;
            Client = app.GetTestClient();
            Parser = parser;
        }

        public WebApplication App { get; }
        public HttpClient Client { get; }
        public RecordingParser Parser { get; }

        public static async Task<TestHost> CreateAsync(
            SefXmlPreviewException? parseError = null,
            long? maxUploadBytes = null,
            int permitLimit = 20,
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
            if (maxUploadBytes.HasValue)
            {
                builder.Configuration["SefXmlPilot:MaxUploadBytes"] = maxUploadBytes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var parser = new RecordingParser(parseError);
            builder.Services.AddSingleton<ISefXmlInvoiceParser>(parser);
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("strict", _ =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "sef-xml-preview",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapSefXmlPreviewEndpoints();
            await app.StartAsync();
            return new TestHost(app, parser);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private sealed class RecordingParser : ISefXmlInvoiceParser
    {
        private readonly SefXmlPreviewException? _parseError;

        public RecordingParser(SefXmlPreviewException? parseError = null)
        {
            _parseError = parseError;
        }

        public int CallCount { get; private set; }

        public Task<SefXmlInvoicePreview> ParseAsync(Stream stream, CancellationToken ct = default)
        {
            CallCount++;
            if (_parseError is not null)
            {
                return Task.FromException<SefXmlInvoicePreview>(_parseError);
            }

            return Task.FromResult(new SefXmlInvoicePreview(
                IsReadOnlyPreview: true,
                DocumentKind: SefXmlDocumentKind.Invoice,
                InvoiceNumber: "SYNTHETIC-001",
                IssueDate: new DateOnly(2026, 10, 16),
                InvoiceTypeCode: "380",
                DocumentCurrencyCode: "RSD",
                Supplier: new SefXmlSupplier(null, null),
                Lines: [],
                Totals: new SefXmlInvoiceTotals(new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null)),
                TaxSubtotals: [],
                Diagnostics: []));
        }
    }
}
