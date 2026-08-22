using System.Data;
using System.Data.Common;
using Application.Analytics.DecisionPulse;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services.Analytics;

public sealed class DecisionPulseScheduleService : IDecisionPulseScheduleService
{
    private readonly ITrendplusDbContext _db;
    private readonly ILogger<DecisionPulseScheduleService> _logger;

    public DecisionPulseScheduleService(
        ITrendplusDbContext db,
        ILogger<DecisionPulseScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<IReadOnlyList<DecisionPulseScheduleDefinition>> ListAsync(CancellationToken ct = default)
        => QueryAsync(onlyEnabled: false, ct);

    public Task<IReadOnlyList<DecisionPulseScheduleDefinition>> ListEnabledAsync(CancellationToken ct = default)
        => QueryAsync(onlyEnabled: true, ct);

    public async Task<DecisionPulseScheduleDefinition?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                   "RecipientsCsv", "Subject", "StoreId", "SupplierId", "DataScope", "LastRunAtUtc",
                   "LastRunStatus", "LastError", "CreatedByUserId", "CreatedByUserName", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "DecisionPulseSchedules"
            WHERE "Id" = @id;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapSchedule(reader);
    }

    public async Task<DecisionPulseScheduleDefinition> UpsertAsync(long? id, DecisionPulseScheduleUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var nowUtc = DateTime.UtcNow;
        await using var cmd = connection.CreateCommand();
        if (id.HasValue)
        {
            cmd.CommandText = """
                UPDATE "DecisionPulseSchedules"
                SET "Name" = @name,
                    "IsEnabled" = @isEnabled,
                    "Frequency" = @frequency,
                    "DayOfWeek" = @dayOfWeek,
                    "RunAtLocalTime" = @runAtLocalTime,
                    "TimeZoneId" = @timeZoneId,
                    "RecipientsCsv" = @recipientsCsv,
                    "Subject" = @subject,
                    "StoreId" = @storeId,
                    "SupplierId" = @supplierId,
                    "DataScope" = @dataScope,
                    "UpdatedAtUtc" = @updatedAtUtc
                WHERE "Id" = @id
                RETURNING "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                          "RecipientsCsv", "Subject", "StoreId", "SupplierId", "DataScope", "LastRunAtUtc",
                          "LastRunStatus", "LastError", "CreatedByUserId", "CreatedByUserName", "CreatedAtUtc", "UpdatedAtUtc";
                """;
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO "DecisionPulseSchedules" (
                    "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                    "RecipientsCsv", "Subject", "StoreId", "SupplierId", "DataScope",
                    "CreatedByUserId", "CreatedByUserName", "CreatedAtUtc", "UpdatedAtUtc"
                )
                VALUES (
                    @name, @isEnabled, @frequency, @dayOfWeek, @runAtLocalTime, @timeZoneId,
                    @recipientsCsv, @subject, @storeId, @supplierId, @dataScope,
                    @createdByUserId, @createdByUserName, @createdAtUtc, @updatedAtUtc
                )
                RETURNING "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                          "RecipientsCsv", "Subject", "StoreId", "SupplierId", "DataScope", "LastRunAtUtc",
                          "LastRunStatus", "LastError", "CreatedByUserId", "CreatedByUserName", "CreatedAtUtc", "UpdatedAtUtc";
                """;
        }

        if (id.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id.Value });
        }

        AddUpsertParameters(cmd, request, nowUtc);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("Decision Pulse schedule could not be saved.");
        }

        return MapSchedule(reader);
    }

    public async Task MarkRunResultAsync(long id, DecisionPulseScheduleRunResult result, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE "DecisionPulseSchedules"
            SET "LastRunAtUtc" = @lastRunAtUtc,
                "LastRunStatus" = @lastRunStatus,
                "LastError" = @lastError,
                "UpdatedAtUtc" = @updatedAtUtc
            WHERE "Id" = @id;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        cmd.Parameters.Add(new NpgsqlParameter("lastRunAtUtc", NpgsqlDbType.Timestamp) { Value = result.ExecutedAtUtc });
        cmd.Parameters.Add(new NpgsqlParameter("lastRunStatus", NpgsqlDbType.Varchar) { Value = result.Status });
        cmd.Parameters.Add(new NpgsqlParameter("lastError", NpgsqlDbType.Text) { Value = (object?)result.Message ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("updatedAtUtc", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<DecisionPulseScheduleDefinition>> QueryAsync(bool onlyEnabled, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                   "RecipientsCsv", "Subject", "StoreId", "SupplierId", "DataScope", "LastRunAtUtc",
                   "LastRunStatus", "LastError", "CreatedByUserId", "CreatedByUserName", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "DecisionPulseSchedules"
            WHERE (@onlyEnabled = false OR "IsEnabled" = true)
            ORDER BY "UpdatedAtUtc" DESC, "Id" DESC;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("onlyEnabled", NpgsqlDbType.Boolean) { Value = onlyEnabled });

        var results = new List<DecisionPulseScheduleDefinition>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapSchedule(reader));
        }

        return results;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "DecisionPulseSchedules" (
                "Id" BIGSERIAL PRIMARY KEY,
                "Name" VARCHAR(200) NOT NULL,
                "IsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
                "Frequency" VARCHAR(16) NOT NULL,
                "DayOfWeek" INTEGER NULL,
                "RunAtLocalTime" VARCHAR(8) NOT NULL,
                "TimeZoneId" VARCHAR(64) NOT NULL,
                "RecipientsCsv" VARCHAR(2000) NOT NULL,
                "Subject" VARCHAR(250) NULL,
                "StoreId" INTEGER NULL,
                "SupplierId" INTEGER NULL,
                "DataScope" VARCHAR(16) NOT NULL DEFAULT 'all',
                "LastRunAtUtc" TIMESTAMP NULL,
                "LastRunStatus" VARCHAR(50) NULL,
                "LastError" VARCHAR(2000) NULL,
                "CreatedByUserId" VARCHAR(200) NOT NULL,
                "CreatedByUserName" VARCHAR(200) NOT NULL,
                "CreatedAtUtc" TIMESTAMP NOT NULL,
                "UpdatedAtUtc" TIMESTAMP NOT NULL
            );
            """;

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision Pulse schedule schema ensure failed.");
            throw;
        }
    }

    private static void AddUpsertParameters(DbCommand cmd, DecisionPulseScheduleUpsertRequest request, DateTime nowUtc)
    {
        cmd.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Varchar) { Value = request.Name.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("isEnabled", NpgsqlDbType.Boolean) { Value = request.IsEnabled });
        cmd.Parameters.Add(new NpgsqlParameter("frequency", NpgsqlDbType.Varchar) { Value = request.Frequency.Trim().ToLowerInvariant() });
        cmd.Parameters.Add(new NpgsqlParameter("dayOfWeek", NpgsqlDbType.Integer) { Value = (object?)request.DayOfWeek ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("runAtLocalTime", NpgsqlDbType.Varchar) { Value = request.RunAtLocalTime.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("timeZoneId", NpgsqlDbType.Varchar) { Value = request.TimeZoneId.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("recipientsCsv", NpgsqlDbType.Varchar) { Value = request.RecipientsCsv.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("subject", NpgsqlDbType.Varchar) { Value = (object?)request.Subject?.Trim() ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("storeId", NpgsqlDbType.Integer) { Value = (object?)request.StoreId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("supplierId", NpgsqlDbType.Integer) { Value = (object?)request.SupplierId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("dataScope", NpgsqlDbType.Varchar) { Value = NormalizeDataScope(request.DataScope) });
        cmd.Parameters.Add(new NpgsqlParameter("createdByUserId", NpgsqlDbType.Varchar) { Value = request.CreatedByUserId });
        cmd.Parameters.Add(new NpgsqlParameter("createdByUserName", NpgsqlDbType.Varchar) { Value = request.CreatedByUserName });
        cmd.Parameters.Add(new NpgsqlParameter("createdAtUtc", NpgsqlDbType.Timestamp) { Value = nowUtc });
        cmd.Parameters.Add(new NpgsqlParameter("updatedAtUtc", NpgsqlDbType.Timestamp) { Value = nowUtc });
    }

    private static DecisionPulseScheduleDefinition MapSchedule(IDataRecord reader)
    {
        return new DecisionPulseScheduleDefinition(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetInt32(9),
            reader.IsDBNull(10) ? null : reader.GetInt32(10),
            reader.IsDBNull(11) ? "all" : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.GetString(15),
            reader.GetString(16),
            reader.GetDateTime(17),
            reader.GetDateTime(18));
    }

    private static string NormalizeDataScope(string? value)
    {
        var normalized = (value ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "existing" or "imported" ? normalized : "all";
    }
}
