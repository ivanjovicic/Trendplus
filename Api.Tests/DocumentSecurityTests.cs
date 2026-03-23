using Infrastructure.Configuration;
using Infrastructure.Services.Documents;
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
        var tampered = $"{token[..^1]}A";

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

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
