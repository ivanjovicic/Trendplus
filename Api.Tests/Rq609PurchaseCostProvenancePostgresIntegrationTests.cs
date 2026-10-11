using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Api.Services.Startup;
using Infrastructure.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ609")]
public sealed class Rq609PurchaseCostProvenancePostgresIntegrationTests : IClassFixture<Rq607PostgresFixture>
{
    private const string MigrationId = "20261010160000_AddPurchaseCostProvenance";
    private readonly Rq607PostgresFixture _fixture;

    public Rq609PurchaseCostProvenancePostgresIntegrationTests(Rq607PostgresFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RQ609 canonical provenance migration is additive, idempotent, and preflight reports schema state")]
    public async Task Migration_RerunPreservesCostsNullAndUnknownOrigin()
    {
        Assert.True(_fixture.IsAvailable, "RQ609 certification requires PostgreSQL; unavailable infrastructure must fail, not skip.");
        var databaseName = $"tp_rq609_cost_{Guid.NewGuid():N}";
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(databaseName);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "RQ609 could not create an isolated PostgreSQL database.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var setup = new NpgsqlCommand("""
            CREATE TABLE prodaja_stavke (
                id bigint PRIMARY KEY,
                nabavna_cena numeric(18,2) NULL
            );
            INSERT INTO prodaja_stavke (id, nabavna_cena) VALUES (1, 125.50), (2, NULL), (3, 0);
            CREATE TABLE public."__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                "ProductVersion" character varying(32) NOT NULL
            );
            """, connection))
        {
            await setup.ExecuteNonQueryAsync();
        }

        var before = await RunPreflightAsync(connectionString!);
        Assert.Equal("missing", before.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("not_applied", before.RootElement.GetProperty("efMigration").GetString());

        // /ready remains healthy when the DB is reachable even though this business column is absent.
        // It is a connectivity/startup contract; the schema preflight is the compatibility release gate.
        await using (var connectivity = new NpgsqlCommand("SELECT 1", connection))
            Assert.Equal(1, await connectivity.ExecuteScalarAsync());
        var readiness = new StartupReadinessState();
        readiness.ReportProbe(new StartupReadinessState.DatabaseProbeState { Ok = true }, new StartupReadinessState.DatabaseProbeState { Ok = true });
        readiness.MarkReady();
        await using var host = await CreateReadinessHostAsync(readiness);
        using var readyResponse = await host.Client.GetAsync("/ready");
        Assert.Equal(System.Net.HttpStatusCode.OK, readyResponse.StatusCode);

        await ApplyCanonicalMigrationUpAsync(connection);
        await using (var preserveOrigin = new NpgsqlCommand("UPDATE prodaja_stavke SET purchase_cost_provenance = 'unknown_provenance' WHERE id = 3", connection))
            await preserveOrigin.ExecuteNonQueryAsync();
        await using (var recordMigration = new NpgsqlCommand("INSERT INTO public.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@id, '8.0.0')", connection))
        {
            recordMigration.Parameters.AddWithValue("id", MigrationId);
            await recordMigration.ExecuteNonQueryAsync();
        }

        // Re-execute the exact Up operations declared by the production EF migration.
        await ApplyCanonicalMigrationUpAsync(connection);

        await using (var verify = new NpgsqlCommand("SELECT id, nabavna_cena, purchase_cost_provenance FROM prodaja_stavke ORDER BY id", connection))
        await using (var reader = await verify.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(125.50m, reader.GetDecimal(1));
            Assert.True(reader.IsDBNull(2));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(2L, reader.GetInt64(0));
            Assert.True(reader.IsDBNull(1));
            Assert.True(reader.IsDBNull(2));
            Assert.True(await reader.ReadAsync());
            Assert.Equal(3L, reader.GetInt64(0));
            Assert.Equal(0m, reader.GetDecimal(1));
            Assert.Equal("unknown_provenance", reader.GetString(2));
            Assert.False(await reader.ReadAsync());
        }

        var after = await RunPreflightAsync(connectionString!);
        Assert.Equal("ready", after.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("applied", after.RootElement.GetProperty("efMigration").GetString());
        Assert.Equal("ef_migration", after.RootElement.GetProperty("schemaSource").GetString());

    }

    [Fact(DisplayName = "RQ609 unavailable provenance column remains a database error, never a zero value")]
    public async Task MissingColumnQueryFailsInsteadOfReturningSyntheticValue()
    {
        Assert.True(_fixture.IsAvailable, "RQ609 certification requires PostgreSQL; unavailable infrastructure must fail, not skip.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq609_absent_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "RQ609 could not create an isolated PostgreSQL database.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var setup = new NpgsqlCommand("CREATE TABLE prodaja_stavke (id bigint PRIMARY KEY, nabavna_cena numeric(18,2) NULL); INSERT INTO prodaja_stavke VALUES (1, 45.00);", connection))
            await setup.ExecuteNonQueryAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var query = new NpgsqlCommand("SELECT purchase_cost_provenance FROM prodaja_stavke WHERE id = 1", connection);
            await query.ExecuteScalarAsync();
        });
        Assert.Equal("42703", error.SqlState);
    }

    private static async Task ApplyCanonicalMigrationUpAsync(NpgsqlConnection connection)
    {
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(AddPurchaseCostProvenance).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up!.Invoke(new AddPurchaseCostProvenance(), [migrationBuilder]);
        foreach (var operation in migrationBuilder.Operations.OfType<SqlOperation>())
        {
            await using var command = new NpgsqlCommand(operation.Sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<JsonDocument> RunPreflightAsync(string connectionString)
    {
        var repoRoot = FindRepoRoot();
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("tools/purchase-cost-provenance-preflight.mjs");
        start.Environment["TRENDPLUS_PREFLIGHT_CONNECTION"] = connectionString;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch the schema preflight process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode is 0 or 2, $"Preflight exited unexpectedly ({process.ExitCode}): {stderr}");
        var report = JsonDocument.Parse(stdout);
        Assert.DoesNotContain("Password", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", stdout, StringComparison.OrdinalIgnoreCase);
        return report;
    }

    private static async Task<ReadinessTestHost> CreateReadinessHostAsync(StartupReadinessState readiness)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(readiness);
        var app = builder.Build();
        app.MapStartupReadinessEndpoint(_ => "test");
        await app.StartAsync();
        return new ReadinessTestHost(app, app.GetTestClient());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trendplus2.Backend.slnf")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root for the RQ609 preflight test.");
    }

    private sealed class ReadinessTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
