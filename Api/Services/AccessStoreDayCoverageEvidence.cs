namespace Api.Services;

internal sealed record AccessStoreDayCounts(
    int? StoreId,
    DateOnly BusinessDate,
    int ReceiptCount,
    int SaleLineCount,
    bool StoreMapped = true);

internal sealed record AccessStoreDayCoverageRow(
    int? StoreId,
    DateOnly BusinessDate,
    string Status,
    int ReceiptCount,
    int SaleLineCount,
    string? ReasonCode);

/// <summary>
/// Turns a complete source snapshot's observed receipt days into bounded store/day evidence.
/// It never extends coverage before a store's first receipt or after its last receipt.
/// </summary>
internal static class AccessStoreDayCoverageEvidence
{
    public static bool IsCompleteSourceScan(
        bool sourceSnapshotComplete,
        int sourceHeaderRows,
        int acceptedHeaderRows,
        int persistedHeaderRows,
        int sourceLineRows,
        int acceptedLineRows,
        int persistedLineRows,
        int unknownDateRows)
        => sourceSnapshotComplete &&
            sourceHeaderRows > 0 &&
            unknownDateRows == 0 &&
            sourceHeaderRows == acceptedHeaderRows &&
            sourceLineRows == acceptedLineRows &&
            persistedHeaderRows == sourceHeaderRows &&
            persistedLineRows == sourceLineRows;

    public static IReadOnlyList<AccessStoreDayCoverageRow> Build(
        IEnumerable<AccessStoreDayCounts> sourceDays,
        bool sourceDayComplete)
    {
        var rows = sourceDays
            .GroupBy(x => (x.StoreId, x.BusinessDate))
            .Select(group => new AccessStoreDayCounts(
                group.Key.StoreId,
                group.Key.BusinessDate,
                group.Sum(x => x.ReceiptCount),
                group.Sum(x => x.SaleLineCount),
                group.All(x => x.StoreMapped)))
            .OrderBy(x => x.BusinessDate)
            .ThenBy(x => x.StoreId)
            .ToArray();

        var result = new List<AccessStoreDayCoverageRow>();
        var storeIdentityIsComplete = rows.All(x => x.StoreId.HasValue && x.StoreMapped);
        foreach (var unknownStoreDay in rows.Where(x => x.StoreId is null || !x.StoreMapped))
        {
            result.Add(new AccessStoreDayCoverageRow(
                unknownStoreDay.StoreId,
                unknownStoreDay.BusinessDate,
                "unverified",
                unknownStoreDay.ReceiptCount,
                unknownStoreDay.SaleLineCount,
                unknownStoreDay.StoreId is null ? "missing_store_identity" : "unmapped_store_id"));
        }

        foreach (var storeDays in rows.Where(x => x.StoreId.HasValue && x.StoreMapped).GroupBy(x => x.StoreId!.Value))
        {
            var byDate = storeDays.ToDictionary(x => x.BusinessDate);
            var firstDate = byDate.Keys.Min();
            var lastDate = byDate.Keys.Max();
            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                if (byDate.TryGetValue(date, out var observed))
                {
                    result.Add(new AccessStoreDayCoverageRow(
                        storeDays.Key,
                        date,
                        sourceDayComplete && storeIdentityIsComplete ? "observed_sales" : "partial",
                        observed.ReceiptCount,
                        observed.SaleLineCount,
                        sourceDayComplete && storeIdentityIsComplete ? null : "source_day_not_complete"));
                }
                else if (sourceDayComplete && storeIdentityIsComplete)
                {
                    result.Add(new AccessStoreDayCoverageRow(
                        storeDays.Key,
                        date,
                        "verified_empty",
                        0,
                        0,
                        null));
                }
            }
        }

        return result;
    }
}
