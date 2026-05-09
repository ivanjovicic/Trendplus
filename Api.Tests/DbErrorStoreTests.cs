using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class DbErrorStoreTests
{
    [Fact]
    public async Task SaveAsync_TruncatesLongFieldsToSchemaLimits()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TrendplusDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        await using var provider = services.BuildServiceProvider();
        var dbFactory = provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>();
        var store = new DbErrorStore(dbFactory, NullLogger<DbErrorStore>.Instance);

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
}
