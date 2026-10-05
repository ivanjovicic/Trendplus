using System.Text.RegularExpressions;
using System.Reflection;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Api.Tests;

public sealed class DatabaseMigrationDiscoveryContractTests
{
    private static readonly IReadOnlyDictionary<string, LegacyMigrationAllowlistEntry> IntentionallyUndiscoverableAllowlist =
        new Dictionary<string, LegacyMigrationAllowlistEntry>(StringComparer.Ordinal)
        {
            ["Infrastructure.Migrations.CreateTransfersTables"] = new(
                "20260327120000_CreateTransfersTables",
                "Idempotent startup bootstrap and the current model snapshot already provide Transfers, TransferItems and StockReservations; the initializer explicitly inserts this migration ID into __EFMigrationsHistory to avoid duplicate DDL.",
                "Migration history is manually recorded by DatabaseInitializer after bootstrap.",
                "Platform / Data",
                new DateOnly(2026, 12, 31)),
            ["Infrastructure.Migrations.AddTransferLifecycleFields"] = new(
                "20260327153000_AddTransferLifecycleFields",
                "The idempotent startup bootstrap and current model snapshot already provide the lifecycle columns and transfer-item quantities; discovering this AddColumn migration after bootstrap would duplicate schema work.",
                "No manual history-list reference exists; bootstrap is the active schema owner.",
                "Platform / Data",
                new DateOnly(2026, 12, 31)),
            ["Infrastructure.Migrations.AddArtikliIdTipObuceIndex"] = new(
                "20260404183000_AddArtikliIdTipObuceIndex",
                "This index is absent from the current model snapshot and inspected bootstrap/SQL sources, with no other repository reference; production catalog state and measured benefit are unknown, so deletion or execution is not yet justified.",
                "No manual history-list reference exists; preserve without discovering until production catalog and index need are checked.",
                "Platform / Data",
                new DateOnly(2026, 12, 31)),
            ["Infrastructure.Migrations.AddDailySalesStatsIndexes"] = new(
                "20260407153000_AddDailySalesStatsIndexes",
                "The two Daily Sales index definitions exist only in this undiscovered class among inspected deployment sources and are absent from the current model snapshot; catalog presence and measured benefit are unknown, so the runbook now marks them unverified.",
                "No manual history-list reference exists; keep unverified until a catalog check and query-cost review.",
                "Analytics Reliability / Daily Sales",
                new DateOnly(2026, 12, 31)),
        };

    [Fact]
    public void TrendplusMigrations_AreDiscoveredOrExplicitlyClassified()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_discovery_test")
            .Options;
        using var context = new TrendplusDbContext(options);

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var discoveredMigrations = migrationsAssembly.Migrations;
        var discoveredTypes = discoveredMigrations.Values.ToHashSet();
        var migrationTypes = typeof(TrendplusDbContext).Assembly
            .GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && type.Namespace == "Infrastructure.Migrations"
                && typeof(Migration).IsAssignableFrom(type))
            .ToArray();

        var undiscovered = migrationTypes
            .Where(type => !discoveredTypes.Contains(type))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            IntentionallyUndiscoverableAllowlist.Keys.OrderBy(name => name, StringComparer.Ordinal),
            undiscovered);

        foreach (var (migrationTypeName, exception) in IntentionallyUndiscoverableAllowlist)
        {
            Assert.Contains(migrationTypes, type => type.FullName == migrationTypeName);
            Assert.Matches("^[0-9]{14}_[A-Za-z0-9]+$", exception.MigrationId);
            Assert.False(string.IsNullOrWhiteSpace(exception.Reason));
            Assert.False(string.IsNullOrWhiteSpace(exception.HistoryHandling));
            Assert.False(string.IsNullOrWhiteSpace(exception.FollowUpOwner));
            Assert.True(exception.SunsetDate > DateOnly.FromDateTime(DateTime.UtcNow));
            var legacyType = migrationTypes.Single(type => type.FullName == migrationTypeName);
            Assert.DoesNotContain(legacyType, discoveredTypes);
            Assert.Null(legacyType.GetCustomAttribute<MigrationAttribute>());
            Assert.Null(legacyType.GetCustomAttribute<DbContextAttribute>());
        }

        var migrationAttributes = migrationTypes
            .Where(type => !IntentionallyUndiscoverableAllowlist.ContainsKey(type.FullName!))
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<MigrationAttribute>()))
            .ToArray();

        Assert.All(migrationAttributes, entry => Assert.NotNull(entry.Attribute));

        var duplicateIds = migrationAttributes
            .GroupBy(entry => entry.Attribute!.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.Empty(duplicateIds);

        foreach (var (migrationType, attribute) in migrationAttributes)
        {
            Assert.Matches("^[0-9]{14}_[A-Za-z0-9]+$", attribute!.Id);
            Assert.Equal(typeof(TrendplusDbContext), migrationType.GetCustomAttribute<DbContextAttribute>()?.ContextType);
            Assert.True(discoveredMigrations.TryGetValue(attribute.Id, out var discoveredType));
            Assert.Equal(migrationType, discoveredType);
        }
    }

    private sealed record LegacyMigrationAllowlistEntry(
        string MigrationId,
        string Reason,
        string HistoryHandling,
        string FollowUpOwner,
        DateOnly SunsetDate);
}
