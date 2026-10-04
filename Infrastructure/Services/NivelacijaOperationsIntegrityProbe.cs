using System.Text.Json;
using Application.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services;

/// <summary>
/// Reconciles the bounded price-event compatibility view with event identities,
/// event windows and raw sale facts. It uses the shared Operations registry through
/// the family-probe contract; it does not introduce another evidence store.
/// </summary>
public sealed class NivelacijaOperationsIntegrityProbe : IOperationsAnalyticsIntegrityFamilyProbe
{
    private const int EventWindowDays = 30;
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TrendplusDbContext _db;

    public NivelacijaOperationsIntegrityProbe(TrendplusDbContext db) => _db = db;

    public string Family => OperationsAnalyticsIntegrityFamilies.Nivelacija;

    public async Task<OperationsAnalyticsIntegrityProbeResult> ProbeAsync(
        OperationsAnalyticsIntegrityProbeRequest request)
    {
        if (request.MaxRows <= 0
            || request.FromUtc.Date > request.ToUtc.Date
            || (request.ToUtc.Date - request.FromUtc.Date).TotalDays > request.Definition.MaxWindowDays)
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Nivelacija integrity window or row bound is invalid.");
        }

        var connectionString = _db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            return OperationsAnalyticsIntegrityProbeResult.Unverified("Nivelacija raw facts are unavailable; no evidence was certified.");

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(request.CancellationToken);
            await using var command = new NpgsqlCommand(BuildSql(), connection)
            {
                CommandTimeout = 8
            };
            command.Parameters.Add(new NpgsqlParameter("fromUtc", NpgsqlDbType.TimestampTz) { Value = request.FromUtc });
            command.Parameters.Add(new NpgsqlParameter("toUtc", NpgsqlDbType.TimestampTz) { Value = request.ToUtc });
            command.Parameters.Add(new NpgsqlParameter("storeId", NpgsqlDbType.Integer)
            {
                Value = request.StoreId.HasValue ? request.StoreId.Value : DBNull.Value
            });
            command.Parameters.Add(new NpgsqlParameter("dataScope", NpgsqlDbType.Text) { Value = NormalizeScope(request.DataScope) });
            command.Parameters.Add(new NpgsqlParameter("rowLimit", NpgsqlDbType.Integer) { Value = request.MaxRows });

            var evidence = new List<EventEvidence>();
            var rowCount = 0;
            var driftCount = 0;
            await using var reader = await command.ExecuteReaderAsync(request.CancellationToken);
            while (await reader.ReadAsync(request.CancellationToken))
            {
                if (evidence.Count >= request.MaxRows)
                    return OperationsAnalyticsIntegrityProbeResult.Degraded("Nivelacija event row bound was exceeded.", rowCount);

                var eventId = reader.GetInt64(0);
                var eventDate = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc);
                var articleId = reader.GetInt32(2);
                int? storeId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                var dataOrigin = reader.IsDBNull(4) ? null : reader.GetString(4);
                decimal? oldPrice = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
                decimal? newPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6);
                var direction = ResolveDirection(oldPrice, newPrice);
                var mature = reader.GetBoolean(7);
                var overlaps = reader.GetBoolean(8);
                var nextEventDate = reader.IsDBNull(9) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
                var sameDayCount = reader.GetInt32(10);
                var preRows = reader.GetInt32(11);
                var preUnits = reader.IsDBNull(12) ? (decimal?)null : reader.GetDecimal(12);
                var preRevenue = reader.IsDBNull(13) ? (decimal?)null : reader.GetDecimal(13);
                var postRows = reader.GetInt32(14);
                var postUnits = reader.IsDBNull(15) ? (decimal?)null : reader.GetDecimal(15);
                var postRevenue = reader.IsDBNull(16) ? (decimal?)null : reader.GetDecimal(16);
                var viewFound = !reader.IsDBNull(17);
                var viewArticleId = reader.IsDBNull(18) ? (int?)null : reader.GetInt32(18);
                var viewEventDate = reader.IsDBNull(19) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(19), DateTimeKind.Utc);
                decimal? viewOldPrice = reader.IsDBNull(20) ? null : reader.GetDecimal(20);
                decimal? viewNewPrice = reader.IsDBNull(21) ? null : reader.GetDecimal(21);
                var viewDirection = reader.IsDBNull(22) ? null : reader.GetString(22);
                var viewMature = viewFound && reader.GetBoolean(23);
                var viewOverlaps = viewFound && reader.GetBoolean(24);
                var viewNextEventDate = reader.IsDBNull(25) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(25), DateTimeKind.Utc);
                var viewSameDayCount = viewFound ? reader.GetInt32(26) : 0;
                var viewPreUnits = reader.IsDBNull(27) ? (decimal?)null : reader.GetDecimal(27);
                var viewPreRevenue = reader.IsDBNull(28) ? (decimal?)null : reader.GetDecimal(28);
                var viewPostUnits = reader.IsDBNull(29) ? (decimal?)null : reader.GetDecimal(29);
                var viewPostRevenue = reader.IsDBNull(30) ? (decimal?)null : reader.GetDecimal(30);
                var viewHasStoreIdentity = reader.GetBoolean(31);
                int? viewStoreId = reader.IsDBNull(32) ? null : reader.GetInt32(32);

                rowCount += 1 + preRows + postRows;
                if (rowCount > request.MaxRows)
                    return OperationsAnalyticsIntegrityProbeResult.Degraded("Nivelacija source-fact row bound was exceeded.", rowCount);

                var canCompareViewSales = !request.StoreId.HasValue
                    && string.Equals(NormalizeScope(request.DataScope), "all", StringComparison.Ordinal);
                var mismatches = FindMismatches(
                    viewFound, articleId, viewArticleId, eventDate, viewEventDate, oldPrice, viewOldPrice,
                    newPrice, viewNewPrice, direction, viewDirection, mature, viewMature, overlaps, viewOverlaps,
                    nextEventDate, viewNextEventDate, sameDayCount, viewSameDayCount,
                    preUnits, viewPreUnits, preRevenue, viewPreRevenue,
                    postUnits, viewPostUnits, postRevenue, viewPostRevenue,
                    canCompareViewSales, storeId, viewHasStoreIdentity, viewStoreId);
                if (mismatches.Count > 0)
                    driftCount++;

                evidence.Add(new EventEvidence(
                    eventId,
                    articleId,
                    eventDate,
                    storeId,
                    storeId.HasValue ? storeId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL",
                    dataOrigin,
                    NormalizeScope(request.DataScope),
                    direction,
                    mature,
                    overlaps,
                    nextEventDate,
                    sameDayCount,
                    eventDate.AddDays(-EventWindowDays),
                    eventDate,
                    eventDate,
                    eventDate.AddDays(EventWindowDays),
                    preUnits,
                    preRevenue,
                    postUnits,
                    postRevenue,
                    viewFound,
                    viewDirection,
                    viewMature,
                    viewOverlaps,
                    viewNextEventDate,
                    viewSameDayCount,
                    viewPreUnits,
                    viewPreRevenue,
                    viewPostUnits,
                    viewPostRevenue,
                    viewHasStoreIdentity,
                    viewStoreId,
                    mismatches));
            }

            var evidenceDimensions = JsonSerializer.SerializeToElement(new
            {
                oracle = "rq547-rq550-rq568-raw-price-event-and-sales-fact-reconciliation",
                eventWindowDays = EventWindowDays,
                requestedFromUtc = request.FromUtc,
                requestedToUtc = request.ToUtc,
                storeId = request.StoreId,
                dataScope = NormalizeScope(request.DataScope),
                eventCount = evidence.Count,
                rowCount,
                events = evidence
            }, EvidenceJsonOptions);

            if (evidence.Count == 0)
                return OperationsAnalyticsIntegrityProbeResult.Unverified(
                    "No price events were found in the bounded Nivelacija window; an empty window does not prove integrity.",
                    0,
                    evidenceDimensions);

            // A concrete canonical mismatch is drift even when the request is
            // store/dataScope scoped. Those contexts cannot become green because
            // the existing compatibility view is unscoped, but its event/cohort
            // disagreements must remain visible as drift.
            if (driftCount > 0)
                return OperationsAnalyticsIntegrityProbeResult.DriftDetected(
                    $"The canonical Nivelacija view disagrees with independent raw event/cohort evidence for {driftCount} bounded event(s).",
                    Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
                    rowCount) with { EvidenceDimensions = evidenceDimensions };

            if (request.StoreId.HasValue || !string.Equals(NormalizeScope(request.DataScope), "all", StringComparison.Ordinal))
                return OperationsAnalyticsIntegrityProbeResult.Unverified(
                    "The bounded raw event context is recorded, but the canonical view oracle is not scoped to this store/dataScope; it was not certified.",
                    rowCount,
                    evidenceDimensions);

            return OperationsAnalyticsIntegrityProbeResult.Verified(
                "Bounded Nivelacija event identity, direction, maturity, overlap and 30-day raw sales windows reconcile to the canonical view.",
                probeRowCount: rowCount) with { EvidenceDimensions = evidenceDimensions };
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703" or "42501")
        {
            return OperationsAnalyticsIntegrityProbeResult.Unverified(
                "The Nivelacija canonical relation, required columns or SELECT permission is unavailable; no evidence was certified.");
        }
    }

    private static string BuildSql() => """
        WITH source_events AS (
            SELECT
                d."Id"::bigint AS event_id,
                COALESCE(src."Datum", d."Datum")::date AS event_date,
                d."ArtikalId" AS article_id,
                d."IDObjekat" AS store_id,
                d."DataOrigin" AS data_origin,
                d."StaraProdajnaCena"::numeric AS old_price,
                d."NovaProdajnaCena"::numeric AS new_price,
                ROW_NUMBER() OVER (
                    PARTITION BY d."ArtikalId", COALESCE(src."Datum", d."Datum"), d."StaraProdajnaCena", d."NovaProdajnaCena"
                    ORDER BY d."Id" DESC) AS duplicate_rank
            FROM "DnevnikPromena" d
            JOIN "Artikli" a ON a."Id" = d."ArtikalId"
            LEFT JOIN LATERAL (
                SELECT CASE
                    WHEN d."BrojRacuna" ~ '^[0-9]+$'
                     AND (
                            length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) < 19
                         OR (
                                length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) = 19
                            AND COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0') <= '9223372036854775807'
                         )
                     )
                    THEN COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')::bigint
                END AS source_event_id
            ) reference ON TRUE
            LEFT JOIN "DnevnikPromena" src ON src."Id"::bigint = reference.source_event_id
            WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
              AND d."ArtikalId" IS NOT NULL
              AND COALESCE(src."Datum", d."Datum") IS NOT NULL
              AND COALESCE(src."Datum", d."Datum")::date >= @fromUtc::date - 30
              AND COALESCE(src."Datum", d."Datum")::date < @toUtc::date + 30
        ), deduplicated AS (
            SELECT * FROM source_events WHERE duplicate_rank = 1
        ), sequenced AS (
            SELECT *,
                LEAD(event_date) OVER (PARTITION BY article_id ORDER BY event_date, event_id) AS next_event_date,
                COUNT(*) OVER (PARTITION BY article_id, event_date)::int AS same_day_event_count
            FROM deduplicated
        ), target_events AS (
            SELECT * FROM sequenced
            WHERE event_date >= @fromUtc::date AND event_date <= @toUtc::date
              AND (@storeId IS NULL OR store_id IS NULL OR store_id = @storeId)
              AND (
                    @dataScope::text = 'all'
                    OR (@dataScope::text = 'imported' AND data_origin = 'access')
                    OR (@dataScope::text = 'existing' AND (data_origin = 'existing' OR data_origin IS NULL OR data_origin = ''))
              )
            ORDER BY event_date, event_id
            LIMIT (@rowLimit + 1)
        )
        SELECT
            e.event_id, e.event_date, e.article_id, e.store_id, e.data_origin, e.old_price, e.new_price,
            (e.event_date + 30 <= CURRENT_DATE) AS is_mature,
            COALESCE(e.next_event_date < e.event_date + 30, FALSE) AS overlaps_next_event,
            e.next_event_date, e.same_day_event_count,
            pre.row_count, pre.units, pre.revenue,
            post.row_count,
            CASE WHEN post.row_count = 0 AND (e.event_date + 30 <= CURRENT_DATE) THEN 0::numeric ELSE post.units END,
            CASE WHEN post.row_count = 0 AND (e.event_date + 30 <= CURRENT_DATE) THEN 0::numeric(18,2) ELSE post.revenue END,
            v.price_event_id,
            v.article_id,
            v.event_date,
            v.old_price,
            v.new_price,
            v.price_direction,
            v.post_window_complete,
            v.overlaps_next_event,
            v.next_event_date,
            v.same_day_event_count,
            v.pre_qty,
            v.pre_revenue,
            v.post_qty,
            v.post_revenue,
            COALESCE(to_jsonb(v) ? 'store_id', FALSE) AS has_store_identity,
            CASE WHEN to_jsonb(v)->>'store_id' ~ '^-?[0-9]+$'
                 THEN (to_jsonb(v)->>'store_id')::integer END AS store_id
        FROM target_events e
        LEFT JOIN vw_vendor_sales_nivelacija v ON v.price_event_id = e.event_id
        LEFT JOIN LATERAL (
            SELECT COUNT(*)::int AS row_count, SUM(ps.kolicina)::numeric AS units,
                   SUM(ps.kolicina * ps.cena)::numeric(18,2) AS revenue
            FROM prodaja_stavke ps JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
            WHERE ps.id_artikal = e.article_id
              AND pz.datum_prodaje >= e.event_date - 30 AND pz.datum_prodaje < e.event_date
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND (
                    @dataScope::text = 'all'
                    OR (@dataScope::text = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope::text = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
              )
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
        ) pre ON TRUE
        LEFT JOIN LATERAL (
            SELECT COUNT(*)::int AS row_count, SUM(ps.kolicina)::numeric AS units,
                   SUM(ps.kolicina * ps.cena)::numeric(18,2) AS revenue
            FROM prodaja_stavke ps JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
            WHERE ps.id_artikal = e.article_id
              AND pz.datum_prodaje >= e.event_date AND pz.datum_prodaje < e.event_date + 30
              AND (@storeId IS NULL OR pz.id_objekat = @storeId)
              AND (
                    @dataScope::text = 'all'
                    OR (@dataScope::text = 'imported' AND pz.data_origin = 'access')
                    OR (@dataScope::text = 'existing' AND (pz.data_origin = 'existing' OR pz.data_origin IS NULL OR pz.data_origin = ''))
              )
              AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
        ) post ON TRUE
        ORDER BY e.event_date, e.event_id
        """;

    private static List<string> FindMismatches(
        bool viewFound, int articleId, int? viewArticleId, DateTime eventDate, DateTime? viewEventDate,
        decimal? oldPrice, decimal? viewOldPrice, decimal? newPrice, decimal? viewNewPrice,
        string direction, string? viewDirection, bool mature, bool viewMature,
        bool overlaps, bool viewOverlaps, DateTime? nextDate, DateTime? viewNextDate,
        int sameDayCount, int viewSameDayCount, decimal? preUnits, decimal? viewPreUnits,
        decimal? preRevenue, decimal? viewPreRevenue, decimal? postUnits, decimal? viewPostUnits,
        decimal? postRevenue, decimal? viewPostRevenue, bool canCompareViewSales,
        int? sourceStoreId, bool viewHasStoreIdentity, int? viewStoreId)
    {
        var mismatches = new List<string>();
        if (!viewFound) mismatches.Add("event_identity_missing_from_view");
        if (articleId != viewArticleId) mismatches.Add("event_article_identity");
        if (eventDate != viewEventDate) mismatches.Add("event_cohort_date");
        if (oldPrice != viewOldPrice || newPrice != viewNewPrice) mismatches.Add("event_price_identity");
        if (!string.Equals(direction, viewDirection, StringComparison.Ordinal)) mismatches.Add("price_direction");
        if (mature != viewMature) mismatches.Add("post_window_maturity");
        if (overlaps != viewOverlaps) mismatches.Add("overlap");
        if (nextDate != viewNextDate) mismatches.Add("next_event_identity");
        if (sameDayCount != viewSameDayCount) mismatches.Add("same_day_cohort_count");
        if (viewHasStoreIdentity && sourceStoreId != viewStoreId) mismatches.Add("event_store_identity");
        if (canCompareViewSales)
        {
            if (!EqualNullable(preUnits, viewPreUnits)) mismatches.Add("pre_window_units");
            if (!EqualNullable(preRevenue, viewPreRevenue)) mismatches.Add("pre_window_revenue");
            if (!EqualNullable(postUnits, viewPostUnits)) mismatches.Add("post_window_units");
            if (!EqualNullable(postRevenue, viewPostRevenue)) mismatches.Add("post_window_revenue");
        }
        return mismatches;
    }

    private static bool EqualNullable(decimal? expected, decimal? actual)
        => expected.HasValue == actual.HasValue
           && (!expected.HasValue || Math.Abs(expected.Value - actual!.Value) <= 0.01m);

    private static string ResolveDirection(decimal? oldPrice, decimal? newPrice)
        => !oldPrice.HasValue || !newPrice.HasValue ? "unknown"
            : newPrice.Value < oldPrice.Value ? "markdown"
            : newPrice.Value > oldPrice.Value ? "markup" : "flat";

    private static string NormalizeScope(string? scope)
    {
        var normalized = (scope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    private sealed record EventEvidence(
        long EventId, int ArticleId, DateTime EventDateUtc, int? StoreId, string StoreIdentity,
        string? DataOrigin, string DataScope, string Direction, bool IsMature, bool OverlapsNextEvent,
        DateTime? NextEventDateUtc, int SameDayEventCount, DateTime PreWindowFromUtc,
        DateTime PreWindowToUtc, DateTime PostWindowFromUtc, DateTime PostWindowToUtc,
        decimal? RawPreUnits, decimal? RawPreRevenue, decimal? RawPostUnits, decimal? RawPostRevenue,
        bool CanonicalViewFound, string? ViewDirection, bool ViewIsMature, bool ViewOverlapsNextEvent,
        DateTime? ViewNextEventDateUtc, int ViewSameDayEventCount, decimal? ViewPreUnits,
        decimal? ViewPreRevenue, decimal? ViewPostUnits, decimal? ViewPostRevenue,
        bool CanonicalStoreIdentityAvailable, int? CanonicalStoreId,
        IReadOnlyList<string> Mismatches);
}
