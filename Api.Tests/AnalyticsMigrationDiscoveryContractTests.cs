using System.Reflection;
using Infrastructure.DbContexts;
using Infrastructure.Migrations.AnalyticsDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Api.Tests;

// RQ629: the analytics context had no migration-discovery guard (RQ588 covered only the main
// context), and two analytics classes were silently skipped by EF.
public sealed class AnalyticsMigrationDiscoveryContractTests
{
    private static readonly IReadOnlyDictionary<string, string> IntentionallyUndiscoverable =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Infrastructure.Migrations.AnalyticsDb.AddAnalyticsActionIndexes"] =
                "No [DbContext] attribute; its two indexes are created idempotently by 20261012120000_ReconcileUndiscoveredAnalyticsSchema.",
            ["Infrastructure.Migrations.AnalyticsDb.AlignAnalyticsRefreshRunContract"] =
                "No attributes; it would shrink columns on existing data. The widening half is applied idempotently by 20261012120000_ReconcileUndiscoveredAnalyticsSchema.",
        };

    [Fact]
    public void AnalyticsMigrations_AreDiscoveredOrExplicitlyClassified()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql("Host=localhost;Database=analytics_migration_discovery_test")
            .Options;
        using var context = new AnalyticsDbContext(options);

        var discovered = context.GetService<IMigrationsAssembly>().Migrations;
        var discoveredTypes = discovered.Values.ToHashSet();
        var migrationTypes = typeof(AnalyticsDbContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.Namespace == "Infrastructure.Migrations.AnalyticsDb"
                && typeof(Migration).IsAssignableFrom(type))
            .ToArray();

        var undiscovered = migrationTypes
            .Where(type => !discoveredTypes.Contains(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(IntentionallyUndiscoverable.Keys.OrderBy(name => name, StringComparer.Ordinal), undiscovered);

        foreach (var type in migrationTypes.Where(type => discoveredTypes.Contains(type)))
        {
            var attribute = type.GetCustomAttribute<MigrationAttribute>();
            Assert.NotNull(attribute);
            Assert.Matches("^[0-9]{14}_[A-Za-z0-9]+$", attribute!.Id);
        }

        Assert.True(discovered.ContainsKey("20261012120000_ReconcileUndiscoveredAnalyticsSchema"));
    }

    [Fact]
    public void ReconcileMigration_IsAdditiveAndIdempotent()
    {
        var sql = ReconcileUndiscoveredAnalyticsSchema.UpSql;

        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_analytics_action_sourcekey_status", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_analytics_action_priority_status", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);

        // Every ALTER is guarded by a "currently narrower" check and only targets the model length.
        var model = new AnalyticsDbContext(new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql("Host=localhost;Database=analytics_migration_discovery_test").Options).Model;
        var entity = model.GetEntityTypes().Single(e => e.GetTableName() == "analytics_refresh_runs");
        foreach (var (column, length) in new[] { ("TriggeredBy", 80), ("ProcessMode", 32), ("WorkerName", 200) })
        {
            Assert.Equal(length, entity.FindProperty(column)!.GetMaxLength());
            Assert.Contains($"column_name = '{column}' AND character_maximum_length < {length}", sql, StringComparison.Ordinal);
            Assert.Contains($"ALTER COLUMN \"{column}\" TYPE character varying({length})", sql, StringComparison.Ordinal);
        }

        var actionIndexes = model.GetEntityTypes().Single(e => e.GetTableName() == "analytics_action_items")
            .GetIndexes().Select(index => index.GetDatabaseName()).ToHashSet();
        Assert.Contains("idx_analytics_action_sourcekey_status", actionIndexes);
        Assert.Contains("idx_analytics_action_priority_status", actionIndexes);
    }
}