using Infrastructure.Seed;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class NivelacijaEventNormalizationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public NivelacijaEventNormalizationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Normalization_UsesExplicitMappings_LogsUnmappedValues_AndIgnoresOversizedReceiptIds()
    {
        Assert.True(_fixture.IsAvailable, "PostgreSQL Testcontainers fixture is required for this regression proof.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq544_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var logger = new CapturingLogger();
        DatabaseInitializer.AttachStartupSqlNoticeLogging(connection, logger);

        await ExecuteAsync(connection, """
            CREATE TABLE "DnevnikPromena" (
                "Id" integer PRIMARY KEY,
                "TipPromene" text,
                "Datum" timestamp,
                "Iznos" numeric,
                "BrojRacuna" text,
                "DobavljacId" integer,
                "ArtikalId" integer,
                "StaraProdajnaCena" numeric,
                "NovaProdajnaCena" numeric,
                "IDObjekat" integer,
                "DataOrigin" text
            );
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "PLU" text,
                "Naziv" text,
                "Kategorija" text,
                "IDDobavljac" integer
            );
            CREATE TABLE "Dobavljaci" ("Id" integer PRIMARY KEY, "Naziv" text);
            CREATE TABLE prodaja_zaglavlje (id integer PRIMARY KEY, datum_prodaje timestamp, broj_racuna text);
            CREATE TABLE prodaja_stavke (id_prodaja integer, id_artikal integer, kolicina numeric, cena numeric);
            INSERT INTO "Artikli" VALUES (10, 'SKU-10', 'Test artikal', 'Obuca', 20);
            INSERT INTO "Dobavljaci" VALUES (20, 'Test dobavljac');
            INSERT INTO "DnevnikPromena" VALUES
              (1, 'prijem', '2026-07-01', NULL, NULL, 20, 10, NULL, NULL, 1, 'access'),
              (2, 'nivelacija cena', '2026-07-10', NULL, '0001', 20, 10, 100, 80, 1, 'access'),
              (3, 'Storno nivelacije', '2026-07-11', NULL, NULL, 20, 10, 100, 80, 1, 'access'),
              (4, 'Re-nivelacija', '2026-07-12', NULL, NULL, 20, 10, 100, 80, 1, 'access'),
              (5, 'Povrat dobavljaču', '2026-07-13', NULL, NULL, 20, 10, 100, 80, 1, 'access'),
              (6, 'povrat kupca', '2026-07-14', NULL, NULL, 20, 10, 100, 80, 1, 'access'),
              (7, 'unos robe', '2026-07-15', NULL, NULL, 20, 10, NULL, NULL, 1, 'access'),
              (8, 'nivelacija', '2026-07-16', NULL, '123456789012', 20, 10, 100, 80, 1, 'access');
            """);

        var normalizationSql = await File.ReadAllTextAsync(FindRepoFile("Database/Migrations/014_NormalizeNivelacijaEvents.sql"));
        await ExecuteAsync(connection, normalizationSql);
        Assert.Contains(logger.Messages, message => message.Contains("source=nivelacija cena canonical=Nivelacija cena changed_rows=1", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("source=Storno nivelacije", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("source=Re-nivelacija", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("source=Povrat dobavljaču", StringComparison.Ordinal));

        Assert.Equal("Nivelacija cena", await ReadTextAsync(connection, "SELECT \"TipPromene\" FROM \"DnevnikPromena\" WHERE \"Id\" = 2"));
        Assert.Equal("Storno nivelacije", await ReadTextAsync(connection, "SELECT \"TipPromene\" FROM \"DnevnikPromena\" WHERE \"Id\" = 3"));
        Assert.Equal("Re-nivelacija", await ReadTextAsync(connection, "SELECT \"TipPromene\" FROM \"DnevnikPromena\" WHERE \"Id\" = 4"));
        Assert.Equal("Povrat dobavljaču", await ReadTextAsync(connection, "SELECT \"TipPromene\" FROM \"DnevnikPromena\" WHERE \"Id\" = 5"));
        Assert.Equal(new DateTime(2026, 7, 1), await ReadDateAsync(connection, "SELECT \"Datum\" FROM \"DnevnikPromena\" WHERE \"Id\" = 2"));

        await ExecuteAsync(connection, normalizationSql);
        Assert.Equal("Nivelacija cena", await ReadTextAsync(connection, "SELECT \"TipPromene\" FROM \"DnevnikPromena\" WHERE \"Id\" = 2"));
        Assert.Contains(logger.Messages, message => message.Contains("source=nivelacija cena canonical=Nivelacija cena changed_rows=0", StringComparison.Ordinal));

        var viewSql = await File.ReadAllTextAsync(FindRepoFile("Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql"));
        await ExecuteAsync(connection, viewSql);
        Assert.Equal(new DateTime(2026, 7, 1), await ReadDateAsync(connection,
            "SELECT event_date FROM vw_sales_pre_nivelacija WHERE price_event_id = 2"));
        Assert.Equal(new DateTime(2026, 7, 16), await ReadDateAsync(connection,
            "SELECT event_date FROM vw_sales_pre_nivelacija WHERE price_event_id = 8"));
    }

    private static string FindRepoFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MASTER_ROADMAP.md")))
            current = current.Parent;
        Assert.NotNull(current);
        return Path.Combine(current!.FullName, relativePath);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadTextAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (string?)await command.ExecuteScalarAsync();
    }

    private static async Task<DateTime> ReadDateAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
