using System.Data;
using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services.Inventory;

public sealed class InventoryReportScheduleService : IInventoryReportScheduleService
{
    private readonly ITrendplusDbContext _db;
    private readonly ILogger<InventoryReportScheduleService> _logger;

    public InventoryReportScheduleService(
        ITrendplusDbContext db,
        ILogger<InventoryReportScheduleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<IReadOnlyList<InventoryReportScheduleDefinition>> ListAsync(CancellationToken ct = default)
        => QueryAsync(onlyEnabled: false, ct);

    public Task<IReadOnlyList<InventoryReportScheduleDefinition>> ListEnabledAsync(CancellationToken ct = default)
        => QueryAsync(onlyEnabled: true, ct);

    public async Task<InventoryReportScheduleDefinition?> GetByIdAsync(long id, CancellationToken ct = default)
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
                   "Format", "Orientation", "IncludeFiltersAndMetadata", "RecipientsCsv", "Subject",
                   "Search", "StoreId", "SupplierId", "SortBy", "LastRunAtUtc", "LastRunStatus",
                   "LastError", "LastDocumentId", "CreatedByUserId", "CreatedByUserName",
                   "CreatedAtUtc", "UpdatedAtUtc"
            FROM "InventoryReportSchedules"
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

    public async Task<InventoryReportScheduleDefinition> UpsertAsync(long? id, InventoryReportScheduleUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var nowUtc = DateTime.UtcNow;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = id.HasValue
            ? """
              UPDATE "InventoryReportSchedules"
              SET "Name" = @name,
                  "IsEnabled" = @isEnabled,
                  "Frequency" = @frequency,
                  "DayOfWeek" = @dayOfWeek,
                  "RunAtLocalTime" = @runAtLocalTime,
                  "TimeZoneId" = @timeZoneId,
                  "Format" = @format,
                  "Orientation" = @orientation,
                  "IncludeFiltersAndMetadata" = @includeFiltersAndMetadata,
                  "RecipientsCsv" = @recipientsCsv,
                  "Subject" = @subject,
                  "Search" = @search,
                  "StoreId" = @storeId,
                  "SupplierId" = @supplierId,
                  "SortBy" = @sortBy,
                  "UpdatedAtUtc" = @updatedAtUtc
              WHERE "Id" = @id
              RETURNING "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                        "Format", "Orientation", "IncludeFiltersAndMetadata", "RecipientsCsv", "Subject",
                        "Search", "StoreId", "SupplierId", "SortBy", "LastRunAtUtc", "LastRunStatus",
                        "LastError", "LastDocumentId", "CreatedByUserId", "CreatedByUserName",
                        "CreatedAtUtc", "UpdatedAtUtc";
              """
            : """
              INSERT INTO "InventoryReportSchedules" (
                  "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                  "Format", "Orientation", "IncludeFiltersAndMetadata", "RecipientsCsv", "Subject",
                  "Search", "StoreId", "SupplierId", "SortBy", "CreatedByUserId", "CreatedByUserName",
                  "CreatedAtUtc", "UpdatedAtUtc"
              )
              VALUES (
                  @name, @isEnabled, @frequency, @dayOfWeek, @runAtLocalTime, @timeZoneId,
                  @format, @orientation, @includeFiltersAndMetadata, @recipientsCsv, @subject,
                  @search, @storeId, @supplierId, @sortBy, @createdByUserId, @createdByUserName,
                  @createdAtUtc, @updatedAtUtc
              )
              RETURNING "Id", "Name", "IsEnabled", "Frequency", "DayOfWeek", "RunAtLocalTime", "TimeZoneId",
                        "Format", "Orientation", "IncludeFiltersAndMetadata", "RecipientsCsv", "Subject",
                        "Search", "StoreId", "SupplierId", "SortBy", "LastRunAtUtc", "LastRunStatus",
                        "LastError", "LastDocumentId", "CreatedByUserId", "CreatedByUserName",
                        "CreatedAtUtc", "UpdatedAtUtc";
              """;

        if (id.HasValue)
        {
            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id.Value });
        }

        AddUpsertParameters(cmd, request, nowUtc);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("Inventory report schedule could not be saved.");
        }

        return MapSchedule(reader);
    }

    public async Task MarkRunResultAsync(long id, InventoryReportScheduleRunResult result, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE "InventoryReportSchedules"
            SET "LastRunAtUtc" = @lastRunAtUtc,
                "LastRunStatus" = @lastRunStatus,
                "LastError" = @lastError,
                "LastDocumentId" = @lastDocumentId,
                "UpdatedAtUtc" = @updatedAtUtc
            WHERE "Id" = @id;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = id });
        cmd.Parameters.Add(new NpgsqlParameter("lastRunAtUtc", NpgsqlDbType.Timestamp) { Value = result.ExecutedAtUtc });
        cmd.Parameters.Add(new NpgsqlParameter("lastRunStatus", NpgsqlDbType.Varchar) { Value = result.Status });
        cmd.Parameters.Add(new NpgsqlParameter("lastError", NpgsqlDbType.Text) { Value = (object?)(result.Success ? null : result.Message) ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("lastDocumentId", NpgsqlDbType.Uuid) { Value = (object?)result.DocumentId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("updatedAtUtc", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<InventoryReportScheduleDefinition>> QueryAsync(bool onlyEnabled, CancellationToken ct)
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
                   "Format", "Orientation", "IncludeFiltersAndMetadata", "RecipientsCsv", "Subject",
                   "Search", "StoreId", "SupplierId", "SortBy", "LastRunAtUtc", "LastRunStatus",
                   "LastError", "LastDocumentId", "CreatedByUserId", "CreatedByUserName",
                   "CreatedAtUtc", "UpdatedAtUtc"
            FROM "InventoryReportSchedules"
            WHERE (@onlyEnabled = false OR "IsEnabled" = true)
            ORDER BY "UpdatedAtUtc" DESC, "Id" DESC;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("onlyEnabled", NpgsqlDbType.Boolean) { Value = onlyEnabled });

        var results = new List<InventoryReportScheduleDefinition>();
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
            CREATE TABLE IF NOT EXISTS "InventoryReportSchedules" (
                "Id" BIGSERIAL PRIMARY KEY,
                "Name" VARCHAR(200) NOT NULL,
                "IsEnabled" BOOLEAN NOT NULL DEFAULT TRUE,
                "Frequency" VARCHAR(16) NOT NULL,
                "DayOfWeek" INTEGER NULL,
                "RunAtLocalTime" VARCHAR(8) NOT NULL,
                "TimeZoneId" VARCHAR(64) NOT NULL,
                "Format" VARCHAR(16) NOT NULL,
                "Orientation" VARCHAR(16) NOT NULL,
                "IncludeFiltersAndMetadata" BOOLEAN NOT NULL DEFAULT TRUE,
                "RecipientsCsv" VARCHAR(2000) NOT NULL,
                "Subject" VARCHAR(250) NULL,
                "Search" VARCHAR(250) NULL,
                "StoreId" INTEGER NULL,
                "SupplierId" INTEGER NULL,
                "SortBy" VARCHAR(50) NULL,
                "LastRunAtUtc" TIMESTAMP NULL,
                "LastRunStatus" VARCHAR(50) NULL,
                "LastError" VARCHAR(2000) NULL,
                "LastDocumentId" UUID NULL,
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
            _logger.LogWarning(ex, "Inventory report schedule schema ensure failed.");
            throw;
        }
    }

    private static void AddUpsertParameters(DbCommand cmd, InventoryReportScheduleUpsertRequest request, DateTime nowUtc)
    {
        cmd.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Varchar) { Value = request.Name.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("isEnabled", NpgsqlDbType.Boolean) { Value = request.IsEnabled });
        cmd.Parameters.Add(new NpgsqlParameter("frequency", NpgsqlDbType.Varchar) { Value = request.Frequency.Trim().ToLowerInvariant() });
        cmd.Parameters.Add(new NpgsqlParameter("dayOfWeek", NpgsqlDbType.Integer) { Value = (object?)request.DayOfWeek ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("runAtLocalTime", NpgsqlDbType.Varchar) { Value = request.RunAtLocalTime.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("timeZoneId", NpgsqlDbType.Varchar) { Value = request.TimeZoneId.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("format", NpgsqlDbType.Varchar) { Value = request.Format.Trim().ToLowerInvariant() });
        cmd.Parameters.Add(new NpgsqlParameter("orientation", NpgsqlDbType.Varchar) { Value = request.Orientation.Trim().ToLowerInvariant() });
        cmd.Parameters.Add(new NpgsqlParameter("includeFiltersAndMetadata", NpgsqlDbType.Boolean) { Value = request.IncludeFiltersAndMetadata });
        cmd.Parameters.Add(new NpgsqlParameter("recipientsCsv", NpgsqlDbType.Varchar) { Value = request.RecipientsCsv.Trim() });
        cmd.Parameters.Add(new NpgsqlParameter("subject", NpgsqlDbType.Varchar) { Value = (object?)request.Subject?.Trim() ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("search", NpgsqlDbType.Varchar) { Value = (object?)request.Search?.Trim() ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("storeId", NpgsqlDbType.Integer) { Value = (object?)request.StoreId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("supplierId", NpgsqlDbType.Integer) { Value = (object?)request.SupplierId ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("sortBy", NpgsqlDbType.Varchar) { Value = (object?)request.SortBy?.Trim() ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("createdByUserId", NpgsqlDbType.Varchar) { Value = request.CreatedByUserId });
        cmd.Parameters.Add(new NpgsqlParameter("createdByUserName", NpgsqlDbType.Varchar) { Value = request.CreatedByUserName });
        cmd.Parameters.Add(new NpgsqlParameter("createdAtUtc", NpgsqlDbType.Timestamp) { Value = nowUtc });
        cmd.Parameters.Add(new NpgsqlParameter("updatedAtUtc", NpgsqlDbType.Timestamp) { Value = nowUtc });
    }

    private static InventoryReportScheduleDefinition MapSchedule(IDataRecord reader)
    {
        return new InventoryReportScheduleDefinition(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetBoolean(9),
            reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetInt32(13),
            reader.IsDBNull(14) ? null : reader.GetInt32(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetDateTime(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.IsDBNull(19) ? null : reader.GetGuid(19),
            reader.GetString(20),
            reader.GetString(21),
            reader.GetDateTime(22),
            reader.GetDateTime(23));
    }
}
