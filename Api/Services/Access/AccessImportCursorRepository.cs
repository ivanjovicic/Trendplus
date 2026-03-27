using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services.Access;

public sealed class AccessImportCursorRepository : IAccessImportCursorRepository
{
    private readonly TrendplusDbContext _db;

    public AccessImportCursorRepository(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<AccessImportCursor> GetOrCreateAsync(string tableKey, string cursorMode, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var normalizedMode = NormalizeCursorMode(cursorMode);

        var existing = await _db.AccessImportCursors
            .FirstOrDefaultAsync(x => x.TableKey == normalizedTableKey, ct);
        if (existing is not null)
            return existing;

        var created = new AccessImportCursor
        {
            TableKey = normalizedTableKey,
            CursorMode = normalizedMode,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.AccessImportCursors.Add(created);
        try
        {
            await _db.SaveChangesAsync(ct);
            return created;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent worker created the cursor first; return canonical persisted row.
            _db.Entry(created).State = EntityState.Detached;
            var concurrent = await _db.AccessImportCursors
                .FirstOrDefaultAsync(x => x.TableKey == normalizedTableKey, ct);
            if (concurrent is not null)
                return concurrent;

            throw;
        }
    }

    public Task<AccessImportCursor?> GetAsync(string tableKey, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        return _db.AccessImportCursors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TableKey == normalizedTableKey, ct);
    }

    public async Task<bool> TryAcquireLeaseAsync(string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var normalizedOwner = NormalizeLeaseOwner(leaseOwner);
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);
        var affected = await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AccessImportCursors"
            SET "LeaseOwner" = {0},
                "LeaseAcquiredAtUtc" = {1},
                "LeaseExpiresAtUtc" = {2},
                "UpdatedAtUtc" = {1}
            WHERE "TableKey" = {3}
              AND (
                    "LeaseOwner" IS NULL
                 OR "LeaseExpiresAtUtc" IS NULL
                 OR "LeaseExpiresAtUtc" <= {1}
                 OR "LeaseOwner" = {0}
              );
            """,
            [normalizedOwner, now, expiresAt, normalizedTableKey],
            ct);
        return affected > 0;
    }

    public async Task<bool> RenewLeaseAsync(string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var normalizedOwner = NormalizeLeaseOwner(leaseOwner);
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);
        var affected = await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AccessImportCursors"
            SET "LeaseExpiresAtUtc" = {0},
                "UpdatedAtUtc" = {1}
            WHERE "TableKey" = {2}
              AND "LeaseOwner" = {3};
            """,
            [expiresAt, now, normalizedTableKey, normalizedOwner],
            ct);
        return affected > 0;
    }

    public async Task ReleaseLeaseAsync(string tableKey, string leaseOwner, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var normalizedOwner = NormalizeLeaseOwner(leaseOwner);
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AccessImportCursors"
            SET "LeaseOwner" = NULL,
                "LeaseAcquiredAtUtc" = NULL,
                "LeaseExpiresAtUtc" = NULL,
                "UpdatedAtUtc" = {0}
            WHERE "TableKey" = {1}
              AND "LeaseOwner" = {2};
            """,
            [now, normalizedTableKey, normalizedOwner],
            ct);
    }

    public async Task MarkRunStartedAsync(string tableKey, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AccessImportCursors"
            SET "LastRunStartedAtUtc" = {0},
                "UpdatedAtUtc" = {0}
            WHERE "TableKey" = {1};
            """,
            [now, normalizedTableKey],
            ct);
    }

    public async Task MarkRunCompletedAsync(string tableKey, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AccessImportCursors"
            SET "LastRunCompletedAtUtc" = {0},
                "UpdatedAtUtc" = {0}
            WHERE "TableKey" = {1};
            """,
            [now, normalizedTableKey],
            ct);
    }

    public async Task CommitCursorAsync(
        string tableKey,
        DateTime? cursorTimestampUtc,
        long? cursorId,
        long? cursorTieBreakerId,
        long? lastSuccessfulBatchId,
        CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var now = DateTime.UtcNow;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "AccessImportCursors"
            SET "CursorTimestampUtc" = {cursorTimestampUtc},
                "CursorId" = {cursorId},
                "CursorTieBreakerId" = {cursorTieBreakerId},
                "LastSuccessfulBatchId" = {lastSuccessfulBatchId},
                "LastError" = NULL,
                "LastRunCompletedAtUtc" = {now},
                "UpdatedAtUtc" = {now}
            WHERE "TableKey" = {normalizedTableKey};
            """,
            ct);
    }

    public async Task MarkFailureAsync(string tableKey, string? error, CancellationToken ct)
    {
        var normalizedTableKey = NormalizeTableKey(tableKey);
        var now = DateTime.UtcNow;
        var trimmedError = Trim(error, 2000);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "AccessImportCursors"
            SET "LastError" = {trimmedError},
                "UpdatedAtUtc" = {now}
            WHERE "TableKey" = {normalizedTableKey};
            """,
            ct);
    }

    private static string NormalizeTableKey(string tableKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableKey);
        return tableKey.Trim().ToLowerInvariant();
    }

    private static string NormalizeCursorMode(string cursorMode)
    {
        if (string.IsNullOrWhiteSpace(cursorMode))
            return "id";
        return cursorMode.Trim().ToLowerInvariant();
    }

    private static string NormalizeLeaseOwner(string leaseOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        return leaseOwner.Trim();
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && string.Equals(pg.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
