using System.Data;
using Application.Analytics.Queries;
using Application.Analytics.Queries.GetInventoryForecast;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Services.Inventory;

public sealed class InventoryForecastSnapshotMaterializerService : IInventoryForecastSnapshotMaterializerService
{
    private static bool _schemaEnsured;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<InventoryForecastSnapshotMaterializerService> _logger;

    public InventoryForecastSnapshotMaterializerService(
        IAnalyticsDbContext db,
        ILogger<InventoryForecastSnapshotMaterializerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventoryForecastSnapshotMaterializationResult> UpsertAsync(
        InventoryForecastSnapshotMaterializationRequest request,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var materializerOwner = NormalizeMaterializerOwner(request.MaterializerOwner);
        var provenanceStatus = NormalizeProvenanceStatus(request.ProvenanceStatus, materializerOwner);
        var snapshotFreshnessUtc = request.SnapshotFreshnessUtc ?? request.IssuedAtUtc;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO analytics_inventory_forecast_snapshot (
                sku_id,
                store_id,
                supplier_id,
                size_code,
                forecast_basis_date,
                issued_at_utc,
                materializer_owner,
                provenance_status,
                snapshot_freshness_utc,
                forecast_7d,
                forecast_14d,
                forecast_28d,
                probability_of_oos_in_7d,
                overstock_risk,
                confidence_score,
                explanation
            )
            VALUES (
                @skuId,
                @storeId,
                @supplierId,
                @sizeCode,
                @forecastBasisDate,
                @issuedAtUtc,
                @materializerOwner,
                @provenanceStatus,
                @snapshotFreshnessUtc,
                @forecast7d,
                @forecast14d,
                @forecast28d,
                @probabilityOfOOSIn7d,
                @overstockRisk,
                @confidenceScore,
                @explanation
            )
            ON CONFLICT (sku_id, store_id, supplier_id, size_code, forecast_basis_date)
            DO UPDATE SET
                issued_at_utc = EXCLUDED.issued_at_utc,
                materializer_owner = EXCLUDED.materializer_owner,
                provenance_status = EXCLUDED.provenance_status,
                snapshot_freshness_utc = EXCLUDED.snapshot_freshness_utc,
                forecast_7d = EXCLUDED.forecast_7d,
                forecast_14d = EXCLUDED.forecast_14d,
                forecast_28d = EXCLUDED.forecast_28d,
                probability_of_oos_in_7d = EXCLUDED.probability_of_oos_in_7d,
                overstock_risk = EXCLUDED.overstock_risk,
                confidence_score = EXCLUDED.confidence_score,
                explanation = EXCLUDED.explanation
            RETURNING forecast_snapshot_id, issued_at_utc;
            """;

        AddParameter(cmd, "skuId", request.SkuId);
        AddParameter(cmd, "storeId", request.StoreId);
        AddParameter(cmd, "supplierId", request.SupplierId);
        AddParameter(cmd, "sizeCode", NormalizeSizeCode(request.SizeCode));
        AddParameter(cmd, "forecastBasisDate", request.ForecastBasisDateUtc.Date);
        AddParameter(cmd, "issuedAtUtc", request.IssuedAtUtc);
        AddParameter(cmd, "materializerOwner", materializerOwner);
        AddParameter(cmd, "provenanceStatus", provenanceStatus);
        AddParameter(cmd, "snapshotFreshnessUtc", snapshotFreshnessUtc);
        AddParameter(cmd, "forecast7d", request.Forecast7d);
        AddParameter(cmd, "forecast14d", request.Forecast14d);
        AddParameter(cmd, "forecast28d", request.Forecast28d);
        AddParameter(cmd, "probabilityOfOOSIn7d", request.ProbabilityOfOOSIn7d);
        AddParameter(cmd, "overstockRisk", request.OverstockRisk);
        AddParameter(cmd, "confidenceScore", request.ConfidenceScore);
        AddParameter(cmd, "explanation", request.Explanation);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException("Inventory forecast snapshot could not be saved.");
        }

        return new InventoryForecastSnapshotMaterializationResult(
            ForecastSnapshotId: reader.GetInt64(0),
            IssuedAtUtc: reader.GetDateTime(1));
    }

    public async Task<IReadOnlyList<InventoryForecastObservedPairDto>> ListObservedPairingsAsync(
        InventoryForecastObservedPairQuery request,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                forecast_snapshot_id,
                sku_id,
                store_id,
                supplier_id,
                size_code,
                forecast_basis_date,
                issued_at_utc,
                horizon_days,
                observed_date,
                forecast_value,
                observed_qty,
                reconstructed_qty,
                stock_qty,
                observed_provenance,
                pairing_status,
                materializer_owner,
                provenance_status,
                snapshot_freshness_utc,
                explanation
            FROM analytics_intel.vw_inventory_forecast_observed_pair_v1
            WHERE (@storeId IS NULL OR store_id = @storeId)
              AND (@supplierId IS NULL OR supplier_id = @supplierId)
              AND (@skuId IS NULL OR sku_id = @skuId)
              AND (@sizeCode IS NULL OR size_code = @sizeCode)
              AND (@horizonDays IS NULL OR horizon_days = @horizonDays)
            ORDER BY forecast_basis_date DESC, sku_id, store_id, horizon_days;
            """;

        AddParameter(cmd, "storeId", NpgsqlDbType.Integer, request.StoreId);
        AddParameter(cmd, "supplierId", NpgsqlDbType.Integer, request.SupplierId);
        AddParameter(cmd, "skuId", NpgsqlDbType.Integer, request.SkuId);
        AddParameter(cmd, "sizeCode", NpgsqlDbType.Text, string.IsNullOrWhiteSpace(request.SizeCode) ? DBNull.Value : request.SizeCode.Trim());
        AddParameter(cmd, "horizonDays", NpgsqlDbType.Integer, request.HorizonDays);

        var results = new List<InventoryForecastObservedPairDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new InventoryForecastObservedPairDto(
                ForecastSnapshotId: reader.GetInt64(0),
                SkuId: reader.GetInt32(1),
                StoreId: reader.GetInt32(2),
                SupplierId: reader.GetInt32(3),
                SizeCode: reader.GetString(4),
                ForecastBasisDate: reader.GetDateTime(5),
                IssuedAtUtc: reader.GetDateTime(6),
                HorizonDays: reader.GetInt32(7),
                ObservedDate: reader.GetDateTime(8),
                ForecastValue: reader.GetNullableDecimal(9),
                ObservedQty: reader.GetNullableDecimal(10),
                ReconstructedQty: reader.GetNullableDecimal(11),
                StockQty: reader.GetNullableDecimal(12),
                ObservedProvenance: reader.GetNullableString(13),
                PairingStatus: reader.GetString(14),
                MaterializerOwner: reader.GetString(15),
                ProvenanceStatus: reader.GetString(16),
                SnapshotFreshnessUtc: reader.GetNullableDateTime(17),
                Explanation: reader.GetString(18)));
        }

        return results;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await SchemaLock.WaitAsync(ct);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            var connection = _db.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE SCHEMA IF NOT EXISTS analytics_intel;

                CREATE TABLE IF NOT EXISTS analytics_inventory_forecast_snapshot (
                    forecast_snapshot_id BIGSERIAL PRIMARY KEY,
                    sku_id INTEGER NOT NULL,
                    store_id INTEGER NOT NULL,
                    supplier_id INTEGER NOT NULL,
                    size_code TEXT NOT NULL DEFAULT 'UNKNOWN',
                    forecast_basis_date DATE NOT NULL,
                    issued_at_utc TIMESTAMPTZ NOT NULL,
                    materializer_owner TEXT NOT NULL,
                    provenance_status TEXT NOT NULL,
                    snapshot_freshness_utc TIMESTAMPTZ NULL,
                    forecast_7d NUMERIC(18, 4) NULL,
                    forecast_14d NUMERIC(18, 4) NULL,
                    forecast_28d NUMERIC(18, 4) NULL,
                    probability_of_oos_in_7d NUMERIC(18, 4) NULL,
                    overstock_risk NUMERIC(18, 4) NULL,
                    confidence_score NUMERIC(18, 4) NULL,
                    explanation TEXT NOT NULL,
                    CONSTRAINT analytics_inventory_forecast_snapshot_key
                        UNIQUE (sku_id, store_id, supplier_id, size_code, forecast_basis_date)
                );

                CREATE INDEX IF NOT EXISTS ix_analytics_inventory_forecast_snapshot_basis
                    ON analytics_inventory_forecast_snapshot (forecast_basis_date DESC, sku_id, store_id);

                CREATE INDEX IF NOT EXISTS ix_analytics_inventory_forecast_snapshot_supplier
                    ON analytics_inventory_forecast_snapshot (supplier_id, forecast_basis_date DESC);

                CREATE OR REPLACE VIEW analytics_intel.vw_inventory_forecast_observed_pair_v1 AS
                WITH horizons AS (
                    SELECT *
                    FROM (
                        VALUES
                            (7, 'forecast_7d'),
                            (14, 'forecast_14d'),
                            (28, 'forecast_28d')
                    ) AS v(horizon_days, forecast_column)
                )
                SELECT
                    f.forecast_snapshot_id,
                    f.sku_id,
                    f.store_id,
                    f.supplier_id,
                    f.size_code,
                    f.forecast_basis_date,
                    f.issued_at_utc,
                    h.horizon_days,
                    (f.forecast_basis_date + h.horizon_days) AS observed_date,
                    CASE h.horizon_days
                        WHEN 7 THEN f.forecast_7d
                        WHEN 14 THEN f.forecast_14d
                        WHEN 28 THEN f.forecast_28d
                        ELSE NULL
                    END AS forecast_value,
                    o.observed_qty,
                    o.reconstructed_qty,
                    o.stock_qty,
                    o.provenance AS observed_provenance,
                    CASE
                        WHEN COALESCE(NULLIF(btrim(f.provenance_status), ''), 'owner_unknown') = 'stale'
                            THEN 'stale'
                        WHEN COALESCE(NULLIF(btrim(f.provenance_status), ''), 'owner_unknown') <> 'trusted'
                            THEN 'unavailable_untrusted_forecast'
                        WHEN o.provenance = 'observed'
                            THEN 'paired_observed'
                        WHEN o.date IS NULL
                            THEN 'missing_observed_window'
                        ELSE 'unavailable_non_observed_basis'
                    END AS pairing_status,
                    COALESCE(NULLIF(btrim(f.materializer_owner), ''), 'none') AS materializer_owner,
                    COALESCE(NULLIF(btrim(f.provenance_status), ''), 'owner_unknown') AS provenance_status,
                    f.snapshot_freshness_utc,
                    f.explanation
                FROM analytics_inventory_forecast_snapshot f
                CROSS JOIN horizons h
                LEFT JOIN analytics_intel.vw_inventory_daily_stock_v1 o
                    ON o.article_id = f.sku_id
                   AND o.store_id = f.store_id
                   AND o.date = (f.forecast_basis_date + h.horizon_days);

                COMMENT ON TABLE analytics_inventory_forecast_snapshot IS
                'Authoritative inventory forecast snapshot materializer. Each row stores issue time, provenance and the 7/14/28-day signal bundle used for observed pairing.';

                COMMENT ON VIEW analytics_intel.vw_inventory_forecast_observed_pair_v1 IS
                'Observed pairing foundation for inventory forecast snapshots. Only rows with trusted forecast provenance and observed daily stock evidence are pairable as paired_observed.';
                """;

            await cmd.ExecuteNonQueryAsync(ct);
            _schemaEnsured = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddParameter(IDbCommand command, string name, NpgsqlDbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = type;
        }

        command.Parameters.Add(parameter);
    }

    private static string NormalizeSizeCode(string sizeCode) =>
        string.IsNullOrWhiteSpace(sizeCode) ? "UNKNOWN" : sizeCode.Trim();

    private static string NormalizeMaterializerOwner(string materializerOwner) =>
        string.IsNullOrWhiteSpace(materializerOwner)
            ? InventoryForecastSnapshotProvenance.UnprovenMaterializerOwner
            : materializerOwner.Trim();

    private static string NormalizeProvenanceStatus(string provenanceStatus, string materializerOwner)
    {
        if (string.Equals(materializerOwner, InventoryForecastSnapshotProvenance.UnprovenMaterializerOwner, StringComparison.OrdinalIgnoreCase))
        {
            return InventoryForecastSnapshotProvenance.OwnerUnknown;
        }

        var normalized = string.IsNullOrWhiteSpace(provenanceStatus)
            ? InventoryForecastSnapshotProvenance.Trusted
            : provenanceStatus.Trim().ToLowerInvariant();

        return normalized is InventoryForecastSnapshotProvenance.Trusted
            or InventoryForecastSnapshotProvenance.OwnerUnknown
            or InventoryForecastSnapshotProvenance.Stale
            ? normalized
            : InventoryForecastSnapshotProvenance.Trusted;
    }
}
