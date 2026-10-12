using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations.AnalyticsDb;

/// <summary>
/// Reconciles schema that the model and snapshot already describe but that EF never applied,
/// because two analytics migration classes are not discoverable (no [DbContext] attribute):
/// <list type="bullet">
/// <item><c>20260521123000_AddAnalyticsActionIndexes</c> - the two composite
/// <c>analytics_action_items</c> indexes were never created.</item>
/// <item><c>20260531103000_AlignAnalyticsRefreshRunContract</c> - <c>analytics_refresh_runs</c>
/// kept its original lengths, so TriggeredBy (32), ProcessMode (16) and WorkerName (128) are
/// narrower than the model (80, 32, 200) and the recorder.</item>
/// </list>
/// Only additive, idempotent steps: indexes use IF NOT EXISTS and columns are widened only when
/// they are currently narrower. Columns the old class would have shrunk (JobKey, ErrorMessage,
/// CorrelationId) are left wider than the model on purpose; shrinking could fail on real data.
/// The legacy classes stay undiscoverable (see DatabaseMigrationDiscoveryContractTests).
/// </summary>
[DbContext(typeof(AnalyticsDbContext))]
[Migration("20261012120000_ReconcileUndiscoveredAnalyticsSchema")]
public partial class ReconcileUndiscoveredAnalyticsSchema : Migration
{
    internal const string UpSql = """
        DO $$
        BEGIN
            IF to_regclass('public.analytics_action_items') IS NOT NULL THEN
                CREATE INDEX IF NOT EXISTS idx_analytics_action_sourcekey_status
                    ON analytics_action_items ("SourceType", "SourceKey", "Status");
                CREATE INDEX IF NOT EXISTS idx_analytics_action_priority_status
                    ON analytics_action_items ("Priority", "Status");
            END IF;

            IF to_regclass('public.analytics_refresh_runs') IS NOT NULL THEN
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'analytics_refresh_runs'
                             AND column_name = 'TriggeredBy' AND character_maximum_length < 80) THEN
                    ALTER TABLE analytics_refresh_runs ALTER COLUMN "TriggeredBy" TYPE character varying(80);
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'analytics_refresh_runs'
                             AND column_name = 'ProcessMode' AND character_maximum_length < 32) THEN
                    ALTER TABLE analytics_refresh_runs ALTER COLUMN "ProcessMode" TYPE character varying(32);
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = 'analytics_refresh_runs'
                             AND column_name = 'WorkerName' AND character_maximum_length < 200) THEN
                    ALTER TABLE analytics_refresh_runs ALTER COLUMN "WorkerName" TYPE character varying(200);
                END IF;
            END IF;
        END $$;
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(UpSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: the model owns these indexes and lengths, and narrowing columns
        // back could fail on stored data.
    }
}