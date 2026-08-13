using Application.Documents.Models;
using Infrastructure.Configuration;
using Infrastructure.Services.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class DocumentSecurityTests
{
    [Fact]
    public void DownloadTokenService_RoundTripsDocumentId()
    {
        var service = new DocumentDownloadTokenService(Options.Create(new DocumentExportOptions
        {
            SigningKey = "unit-test-key"
        }), CreateHostEnvironment(), NullLogger<DocumentDownloadTokenService>.Instance);

        var documentId = Guid.NewGuid();
        var token = service.Create(documentId, DateTime.UtcNow.AddMinutes(5));

        Assert.True(service.TryValidate(documentId, token));
        Assert.False(service.TryValidate(Guid.NewGuid(), token));
    }

    [Fact]
    public void UserContextAccessor_DoesNotGrantRolesFromSpoofedHeaders()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "attacker";
        httpContext.Request.Headers["X-User-Name"] = "Attacker";
        httpContext.Request.Headers["X-User-Roles"] = "Admin,AnalyticsExport";

        var accessor = new DocumentUserContextAccessor(new StaticHttpContextAccessor { HttpContext = httpContext });
        var context = accessor.GetCurrent();

        Assert.Equal("anonymous", context.UserId);
        Assert.Equal("anonymous", context.UserName);
        Assert.Empty(context.Roles);
        Assert.Throws<UnauthorizedAccessException>(() => new DocumentAccessControlService().EnsureCanGenerate(context));
    }

    [Fact]
    public void AccessControl_RejectsAnonymousWithoutRoles()
    {
        var service = new DocumentAccessControlService();
        var context = new DocumentExecutionContext
        {
            UserId = "anonymous",
            UserName = "anonymous",
            Roles = Array.Empty<string>()
        };

        Assert.Throws<UnauthorizedAccessException>(() => service.EnsureCanGenerate(context));
    }

    [Fact]
    public void AccessControl_AllowsAdminRole()
    {
        var service = new DocumentAccessControlService();
        var context = new DocumentExecutionContext
        {
            UserId = "admin-key",
            UserName = "admin-key",
            Roles = ["Admin"]
        };

        service.EnsureCanGenerate(context);
    }

    [Fact]
    public void DownloadTokenService_RejectsExpiredToken()
    {
        var service = CreateService();
        var documentId = Guid.NewGuid();
        var token = service.Create(documentId, DateTime.UtcNow.AddMinutes(-1));

        Assert.False(service.TryValidate(documentId, token));
    }

    [Fact]
    public void DownloadTokenService_RejectsTamperedToken()
    {
        var service = CreateService();
        var documentId = Guid.NewGuid();
        var token = service.Create(documentId, DateTime.UtcNow.AddMinutes(5));
        var parts = token.Split('.', 2);
        Assert.Equal(2, parts.Length);

        var payloadBytes = Base64UrlDecode(parts[0]);
        payloadBytes[0] ^= 0x01;
        var tamperedPayload = Base64UrlEncode(payloadBytes);
        var tampered = $"{tamperedPayload}.{parts[1]}";

        Assert.False(service.TryValidate(documentId, tampered));
    }

    [Fact]
    public void DownloadTokenService_RejectsInvalidJsonPayload()
    {
        var service = CreateService();
        var invalidPayload = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("{bad-json"));
        var invalidSignature = Base64UrlEncode(new byte[] { 1, 2, 3 });
        var token = $"{invalidPayload}.{invalidSignature}";

        Assert.False(service.TryValidate(Guid.NewGuid(), token));
    }

    private static DocumentDownloadTokenService CreateService()
    {
        return new DocumentDownloadTokenService(Options.Create(new DocumentExportOptions
        {
            SigningKey = "unit-test-key"
        }), CreateHostEnvironment(), NullLogger<DocumentDownloadTokenService>.Instance);
    }

    private static IHostEnvironment CreateHostEnvironment()
    {
        return new TestHostEnvironment();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class StaticHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Convert.FromBase64String(normalized);
    }
}
