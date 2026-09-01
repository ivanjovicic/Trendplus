using System.Reflection;
using Api.Config;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AccessImportOperationalLogTests
{
    [Fact]
    public void AddAccessImportLogEntry_Persists_InProduction()
    {
        using var trendDb = CreateTrendDbContext();
        using var analyticsDb = CreateAnalyticsDbContext();
        var service = CreateService(trendDb, analyticsDb, Environments.Production);

        InvokeAddAccessImportLogEntry(
            service,
            batchId: 42,
            tableName: "prodaja_stavke",
            rowIndex: 3,
            severity: "warning",
            message: "keep me",
            sourceRowJson: "{}");

        Assert.Single(trendDb.AccessImportLogs.Local);
    }

    [Fact]
    public void AddAccessImportLogEntry_SkipsPersistence_WhenNotProduction()
    {
        using var trendDb = CreateTrendDbContext();
        using var analyticsDb = CreateAnalyticsDbContext();
        var service = CreateService(trendDb, analyticsDb, Environments.Development);

        InvokeAddAccessImportLogEntry(
            service,
            batchId: 42,
            tableName: "prodaja_stavke",
            rowIndex: 3,
            severity: "warning",
            message: "skip me",
            sourceRowJson: "{}");

        Assert.Empty(trendDb.AccessImportLogs.Local);
    }

    private static TrendplusDbContext CreateTrendDbContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TrendplusDbContext(options);
    }

    private static AnalyticsDbContext CreateAnalyticsDbContext()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AnalyticsDbContext(options);
    }

    private static AccessImportService CreateService(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        string environmentName)
    {
        return new AccessImportService(
            trendDb: trendDb,
            analyticsDb: analyticsDb,
            logger: NullLogger<AccessImportService>.Instance,
            options: Options.Create(new AccessImportOptions()),
            environment: new TestHostEnvironment(environmentName));
    }

    private static void InvokeAddAccessImportLogEntry(
        AccessImportService service,
        long batchId,
        string tableName,
        int rowIndex,
        string severity,
        string message,
        string? sourceRowJson)
    {
        var method = typeof(AccessImportService).GetMethod(
            "AddAccessImportLogEntry",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(service, [batchId, tableName, rowIndex, severity, message, sourceRowJson]);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ApplicationName = "Api.Tests";
            ContentRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }
}
