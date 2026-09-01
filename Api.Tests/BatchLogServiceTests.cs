using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class BatchLogServiceTests
{
    [Fact]
    public async Task Log_And_FlushAsync_Persists_WhenProduction()
    {
        await using var db = CreateDbContext();
        var service = new BatchLogService(db, NullLogger<BatchLogService>.Instance, new TestHostEnvironment(Environments.Production));

        service.Log(7, "prodaja_stavke", 1, "warning", "row skipped", "{ }");
        await service.FlushAsync();

        Assert.Single(db.AccessImportLogs);
    }

    [Fact]
    public async Task Log_And_FlushAsync_SkipPersistence_WhenNotProduction()
    {
        await using var db = CreateDbContext();
        var service = new BatchLogService(db, NullLogger<BatchLogService>.Instance, new TestHostEnvironment(Environments.Development));

        service.Log(7, "prodaja_stavke", 1, "warning", "row skipped", "{ }");
        await service.FlushAsync();

        Assert.Empty(db.AccessImportLogs);
    }

    private static TrendplusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TrendplusDbContext(options);
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
