using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class DbErrorStoreTests
{
    internal async Task SaveAsync_TruncatesLongFieldsToSchemaLimits()
    {
        await SaveAsync_TruncatesLongFieldsToSchemaLimits_WhenProduction();
    }

    [Fact]
    public async Task SaveAsync_TruncatesLongFieldsToSchemaLimits_WhenProduction()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TrendplusDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production));

        await using var provider = services.BuildServiceProvider();
        var dbFactory = provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>();
        var store = new DbErrorStore(dbFactory, NullLogger<DbErrorStore>.Instance, provider.GetRequiredService<IHostEnvironment>());

        await store.SaveAsync(
            new ErrorRecord
            {
                Level = "Error",
                Message = new string('m', 2500),
                ExceptionType = new string('e', 600),
                StackTrace = new string('s', 4500),
                Path = new string('p', 1100),
                UserName = new string('u', 250),
                ClientApp = new string('a', 1200),
                CorrelationId = "test-correlation"
            },
            CancellationToken.None);

        await using var db = await dbFactory.CreateDbContextAsync();
        var saved = await db.ErrorRecords.SingleAsync();

        Assert.Equal(2000, saved.Message.Length);
        Assert.Equal(500, saved.ExceptionType.Length);
        Assert.Equal(4000, saved.StackTrace?.Length);
        Assert.Equal(1000, saved.Path?.Length);
        Assert.Equal(200, saved.UserName?.Length);
        Assert.Equal(1000, saved.ClientApp?.Length);
        Assert.Equal("test-correlation", saved.CorrelationId);
    }

    [Fact]
    public async Task SaveAsync_SkipsPersistence_WhenNotProduction()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TrendplusDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Development));

        await using var provider = services.BuildServiceProvider();
        var dbFactory = provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>();
        var store = new DbErrorStore(dbFactory, NullLogger<DbErrorStore>.Instance, provider.GetRequiredService<IHostEnvironment>());

        await store.SaveAsync(
            new ErrorRecord
            {
                Level = "Error",
                Message = "dev-only",
                ExceptionType = "InvalidOperationException",
                CorrelationId = "dev-correlation"
            },
            CancellationToken.None);

        await using var db = await dbFactory.CreateDbContextAsync();
        Assert.Empty(db.ErrorRecords);
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
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
