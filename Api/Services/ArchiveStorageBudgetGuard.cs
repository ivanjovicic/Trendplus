using System.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public sealed record ArchiveStorageBudgetSnapshot(long TotalBytes, long RowCount);

public sealed record ArchiveStorageBudgetDecision(bool Allowed, string Reason)
{
    public static ArchiveStorageBudgetDecision Permit(ArchiveStorageBudgetSnapshot snapshot)
        => new(true, $"archive within budget: {snapshot.TotalBytes} bytes, {snapshot.RowCount} rows");
}

public static class ArchiveStorageBudgetGuard
{
    public static ArchiveStorageBudgetDecision Evaluate(
        ArchiveStorageBudgetSnapshot snapshot,
        long maxBytes,
        long maxRows)
    {
        if (maxBytes <= 0 || maxRows <= 0)
            return new(false, "archive storage budget is invalid; both limits must be positive");

        if (snapshot.TotalBytes > maxBytes)
            return new(false, $"archive byte budget exceeded: {snapshot.TotalBytes} > {maxBytes}");

        if (snapshot.RowCount > maxRows)
            return new(false, $"archive row budget exceeded: {snapshot.RowCount} > {maxRows}");

        return ArchiveStorageBudgetDecision.Permit(snapshot);
    }

    public static async Task<ArchiveStorageBudgetSnapshot?> ReadAsync(
        DbContext dbContext,
        CancellationToken ct = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
            await connection.OpenAsync(ct);

        try
        {
            await using var relationCommand = connection.CreateCommand();
            relationCommand.CommandText = "SELECT to_regclass('public.deleted_rows_archive');";
            var relation = await relationCommand.ExecuteScalarAsync(ct);
            if (relation is null || relation is DBNull)
                return null;

            await using var sizeCommand = connection.CreateCommand();
            sizeCommand.CommandText = """
                SELECT pg_total_relation_size('public.deleted_rows_archive'),
                       COUNT(*)
                FROM public.deleted_rows_archive;
                """;

            await using var reader = await sizeCommand.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.IsDBNull(0) || reader.IsDBNull(1))
                return null;

            return new ArchiveStorageBudgetSnapshot(reader.GetInt64(0), reader.GetInt64(1));
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }
}
