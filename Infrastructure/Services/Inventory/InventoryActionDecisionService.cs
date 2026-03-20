using System.Data;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Application.Inventory.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services.Inventory;

public sealed class InventoryActionDecisionService : IInventoryActionDecisionService
{
    private readonly ITrendplusDbContext _db;
    private readonly ILogger<InventoryActionDecisionService> _logger;

    public InventoryActionDecisionService(
        ITrendplusDbContext db,
        ILogger<InventoryActionDecisionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>> ListAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT "SuggestionKey", "ActionType", "Status", "Note",
                   "UpdatedByUserId", "UpdatedByUserName", "UpdatedAtUtc"
            FROM "InventoryActionDecisions";
            """;

        var result = new Dictionary<string, InventoryActionDecisionDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var definition = Map(reader);
            result[definition.SuggestionKey] = definition;
        }

        return result;
    }

    public async Task<InventoryActionDecisionDefinition> UpsertAsync(InventoryActionDecisionUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "InventoryActionDecisions" (
                "SuggestionKey", "ActionType", "Status", "Note",
                "UpdatedByUserId", "UpdatedByUserName", "UpdatedAtUtc"
            )
            VALUES (
                @suggestionKey, @actionType, @status, @note,
                @updatedByUserId, @updatedByUserName, @updatedAtUtc
            )
            ON CONFLICT ("SuggestionKey")
            DO UPDATE SET
                "ActionType" = EXCLUDED."ActionType",
                "Status" = EXCLUDED."Status",
                "Note" = EXCLUDED."Note",
                "UpdatedByUserId" = EXCLUDED."UpdatedByUserId",
                "UpdatedByUserName" = EXCLUDED."UpdatedByUserName",
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            RETURNING "SuggestionKey", "ActionType", "Status", "Note",
                      "UpdatedByUserId", "UpdatedByUserName", "UpdatedAtUtc";
            """;

        cmd.Parameters.Add(new NpgsqlParameter("suggestionKey", NpgsqlDbType.Varchar) { Value = request.SuggestionKey });
        cmd.Parameters.Add(new NpgsqlParameter("actionType", NpgsqlDbType.Varchar) { Value = request.ActionType });
        cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = request.Status });
        cmd.Parameters.Add(new NpgsqlParameter("note", NpgsqlDbType.Varchar) { Value = (object?)request.Note ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("updatedByUserId", NpgsqlDbType.Varchar) { Value = request.UpdatedByUserId });
        cmd.Parameters.Add(new NpgsqlParameter("updatedByUserName", NpgsqlDbType.Varchar) { Value = request.UpdatedByUserName });
        cmd.Parameters.Add(new NpgsqlParameter("updatedAtUtc", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("Inventory action decision could not be saved.");
        }

        return Map(reader);
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
            CREATE TABLE IF NOT EXISTS "InventoryActionDecisions" (
                "SuggestionKey" VARCHAR(200) PRIMARY KEY,
                "ActionType" VARCHAR(32) NOT NULL,
                "Status" VARCHAR(32) NOT NULL,
                "Note" VARCHAR(1000) NULL,
                "UpdatedByUserId" VARCHAR(200) NOT NULL,
                "UpdatedByUserName" VARCHAR(200) NOT NULL,
                "UpdatedAtUtc" TIMESTAMP NOT NULL
            );
            """;

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inventory action decision schema ensure failed.");
            throw;
        }
    }

    private static InventoryActionDecisionDefinition Map(IDataRecord reader)
    {
        return new InventoryActionDecisionDefinition(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDateTime(6));
    }
}
