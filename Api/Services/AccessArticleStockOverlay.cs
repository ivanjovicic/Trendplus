using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Access Artikli quantities are a source snapshot. Locally posted stock movements are outside that
/// snapshot and must remain applied when a later Access import refreshes the baseline.
/// </summary>
public static class AccessArticleStockOverlay
{
    // ASCII "TPARTICL". Serializes Access' explicit article IDs and PostgreSQL-generated
    // web article IDs, and protects the Access import's monotonic identity-sequence repair.
    public const long ArticleIdentityAdvisoryLockKey = 0x5450_4152_5449_434C;

    public static async Task AcquireArticleIdentityLockAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database, CancellationToken ct)
    {
        if (database.CurrentTransaction is null)
            throw new InvalidOperationException("Zaključavanje identiteta artikala zahteva aktivnu PostgreSQL transakciju.");

        await database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})",
            new object[] { ArticleIdentityAdvisoryLockKey },
            ct);
    }

    public static async Task<IReadOnlyDictionary<int, int>> LoadAsync(ITrendplusDbContext db, CancellationToken ct)
    {
        var deltas = await db.DnevnikPromena.AsNoTracking()
            .Where(x => x.DataOrigin == "app" && x.ArtikalId.HasValue && x.Kolicina.HasValue &&
                (x.TipPromene == TipPromeneConstants.UlazRobe ||
                 x.TipPromene == TipPromeneConstants.PrenosUlaz ||
                 x.TipPromene == TipPromeneConstants.PrenosIzlaz ||
                 x.TipPromene == TipPromeneConstants.PovratKupca))
            .GroupBy(x => x.ArtikalId!.Value)
            .Select(g => new { ArticleId = g.Key, Quantity = g.Sum(x => (long)x.Kolicina!.Value) })
            .ToListAsync(ct);

        return deltas.ToDictionary(x => x.ArticleId, x => checked((int)x.Quantity));
    }

    public static int? Apply(int? sourceSnapshotQuantity, int applicationMovementDelta, int? previousQuantity = null) =>
        sourceSnapshotQuantity.HasValue
            ? checked(sourceSnapshotQuantity.Value + applicationMovementDelta)
            : previousQuantity;
}
