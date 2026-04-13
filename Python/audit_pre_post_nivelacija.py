from __future__ import annotations

import argparse
import json
import os
from dataclasses import dataclass
from datetime import date, datetime
from decimal import Decimal
from pathlib import Path
from typing import Any

import psycopg2
import psycopg2.extras
import pyodbc


DEFAULT_MDB_PATH = Path(r"c:\Users\Ivan\source\repos\Trendplus2\Trend plus.mdb")
DEFAULT_PG_URL = (
    "postgresql://neondb_owner:npg_7hUftT3sXHgR@"
    "ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/"
    "trendplus?sslmode=require&channel_binding=require"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Audit Pre/Posle nivelacije lineage and parity.")
    parser.add_argument("--mdb-path", type=Path, default=DEFAULT_MDB_PATH)
    parser.add_argument("--pg-url", default=os.getenv("PRE_POST_NIVELACIJA_PG_URL", DEFAULT_PG_URL))
    parser.add_argument("--sample-size", type=int, default=10)
    parser.add_argument(
        "--output-json",
        type=Path,
        default=Path(r"c:\Users\Ivan\source\repos\Trendplus2\tmp\pre_post_nivelacija_audit.json"),
    )
    parser.add_argument(
        "--repair-live-rows",
        action="store_true",
        help="Update imported nivelacija line rows in Postgres using Access source dates/store/supplier lineage.",
    )
    return parser.parse_args()


def json_default(value: Any) -> Any:
    if isinstance(value, Decimal):
        return float(value)
    if isinstance(value, (datetime, date)):
        return value.isoformat()
    if isinstance(value, Path):
        return str(value)
    raise TypeError(f"Object of type {type(value)!r} is not JSON serializable")


def decimal_equal(left: Any, right: Any, tolerance: Decimal = Decimal("0.01")) -> bool:
    if left is None and right is None:
        return True
    if left is None or right is None:
        return False
    left_decimal = Decimal(str(left))
    right_decimal = Decimal(str(right))
    return abs(left_decimal - right_decimal) <= tolerance


def int_equal(left: Any, right: Any) -> bool:
    if left is None and right is None:
        return True
    if left is None or right is None:
        return False
    return int(left) == int(right)


def date_equal(left: Any, right: Any) -> bool:
    if left is None and right is None:
        return True
    if left is None or right is None:
        return False
    left_date = left.date() if isinstance(left, datetime) else left
    right_date = right.date() if isinstance(right, datetime) else right
    return left_date == right_date


@dataclass
class AuditContext:
    access_conn: pyodbc.Connection
    pg_conn: psycopg2.extensions.connection

    def fetch_access_one(self, query: str, params: tuple[Any, ...] = ()) -> dict[str, Any] | None:
        cursor = self.access_conn.cursor()
        cursor.execute(query, params)
        row = cursor.fetchone()
        if row is None:
            cursor.close()
            return None
        columns = [column[0] for column in cursor.description]
        result = dict(zip(columns, row))
        cursor.close()
        return result

    def fetch_access_all(self, query: str, params: tuple[Any, ...] = ()) -> list[dict[str, Any]]:
        cursor = self.access_conn.cursor()
        cursor.execute(query, params)
        columns = [column[0] for column in cursor.description]
        rows = [dict(zip(columns, row)) for row in cursor.fetchall()]
        cursor.close()
        return rows

    def fetch_pg_one(self, query: str, params: tuple[Any, ...] = ()) -> dict[str, Any] | None:
        with self.pg_conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
            cursor.execute(query, params)
            row = cursor.fetchone()
            return dict(row) if row is not None else None

    def fetch_pg_all(self, query: str, params: tuple[Any, ...] = ()) -> list[dict[str, Any]]:
        with self.pg_conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
            cursor.execute(query, params)
            return [dict(row) for row in cursor.fetchall()]


def build_access_lineage_map(ctx: AuditContext) -> dict[tuple[int, int], dict[str, Any]]:
    rows = ctx.fetch_access_all(
        """
        SELECT
            n.IDArtikal,
            n.IDDnevnik,
            d.Datum,
            d.IDObjekat,
            a.IDDobavljac
        FROM (tblNivelacije AS n
            LEFT JOIN tblDnevnikPromena AS d ON n.IDDnevnik = d.IDDnevnik)
            LEFT JOIN tblArtikli AS a ON n.IDArtikal = a.IDArtikal
        """
    )
    return {
        (int(row["IDArtikal"]), int(row["IDDnevnik"])): {
            "date": row["Datum"].date() if isinstance(row["Datum"], datetime) else row["Datum"],
            "store_id": row["IDObjekat"],
            "vendor_id": row["IDDobavljac"],
        }
        for row in rows
        if row["IDArtikal"] is not None and row["IDDnevnik"] is not None
    }


def repair_live_rows(ctx: AuditContext) -> dict[str, Any]:
    access_lineage = build_access_lineage_map(ctx)
    pg_rows = ctx.fetch_pg_all(
        """
        SELECT
            "Id",
            "ArtikalId",
            "BrojRacuna",
            "Datum"::date AS datum,
            "IDObjekat",
            "DobavljacId"
        FROM "DnevnikPromena"
        WHERE "DataOrigin" = 'access'
          AND "TipPromene" = 'Nivelacija'
          AND "ArtikalId" IS NOT NULL
        """
    )

    updates: list[tuple[Any, Any, Any, int]] = []
    updated_date_rows = 0
    updated_store_rows = 0
    updated_vendor_rows = 0
    samples: list[dict[str, Any]] = []

    for row in pg_rows:
        broj_racuna = row["BrojRacuna"]
        if not isinstance(broj_racuna, str) or not broj_racuna.lstrip("-").isdigit():
            continue

        key = (int(row["ArtikalId"]), int(broj_racuna))
        source = access_lineage.get(key)
        if source is None:
            continue

        current_date = row["datum"]
        current_store = row["IDObjekat"]
        current_vendor = row["DobavljacId"]
        needs_update = False

        if source["date"] != current_date:
            updated_date_rows += 1
            needs_update = True
        if source["store_id"] != current_store:
            updated_store_rows += 1
            needs_update = True
        if source["vendor_id"] != current_vendor:
            updated_vendor_rows += 1
            needs_update = True

        if not needs_update:
            continue

        updates.append((source["date"], source["store_id"], source["vendor_id"], int(row["Id"])))
        if len(samples) < 10:
            samples.append(
                {
                    "id": row["Id"],
                    "article_id": row["ArtikalId"],
                    "source_header_id": int(broj_racuna),
                    "before": {
                        "date": current_date,
                        "store_id": current_store,
                        "vendor_id": current_vendor,
                    },
                    "after": source,
                }
            )

    if updates:
        with ctx.pg_conn.cursor() as cursor:
            cursor.execute(
                """
                CREATE TEMP TABLE tmp_nivelacija_lineage_fix (
                    datum date,
                    idobjekat integer,
                    dobavljacid integer,
                    id integer
                ) ON COMMIT DROP
                """,
            )
            psycopg2.extras.execute_values(
                cursor,
                "INSERT INTO tmp_nivelacija_lineage_fix (datum, idobjekat, dobavljacid, id) VALUES %s",
                updates,
                page_size=1000,
            )
            cursor.execute(
                """
                UPDATE "DnevnikPromena" AS target
                SET "Datum" = source.datum,
                    "IDObjekat" = source.idobjekat,
                    "DobavljacId" = source.dobavljacid
                FROM tmp_nivelacija_lineage_fix AS source
                WHERE target."Id" = source.id
                """
            )
        ctx.pg_conn.commit()
    else:
        ctx.pg_conn.rollback()

    return {
        "candidate_rows": len(pg_rows),
        "updated_rows": len(updates),
        "updated_date_rows": updated_date_rows,
        "updated_store_rows": updated_store_rows,
        "updated_vendor_rows": updated_vendor_rows,
        "samples": samples,
    }


def connect_access(mdb_path: Path) -> pyodbc.Connection:
    available_drivers = [driver for driver in pyodbc.drivers() if "Access" in driver or "MDB" in driver]
    if not available_drivers:
        raise RuntimeError("No Access/MDB ODBC driver is available.")

    driver = next((driver for driver in available_drivers if "*.mdb" in driver), available_drivers[0])
    connection_string = f"Driver={{{driver}}};DBQ={mdb_path};"
    return pyodbc.connect(connection_string, autocommit=True)


def aggregate_checks(ctx: AuditContext) -> dict[str, Any]:
    access_line_rows = ctx.fetch_access_one(
        "SELECT COUNT(*) AS access_line_rows FROM tblNivelacije"
    )
    access_distinct_events = ctx.fetch_access_one(
        "SELECT COUNT(*) AS access_distinct_events FROM (SELECT IDDnevnik FROM tblNivelacije GROUP BY IDDnevnik) AS grouped_events"
    )
    access = {
        "access_line_rows": access_line_rows["access_line_rows"],
        "access_distinct_events": access_distinct_events["access_distinct_events"],
    }

    postgres = ctx.fetch_pg_one(
        """
        SELECT
            (SELECT COUNT(*) FROM "DnevnikPromena"
             WHERE "DataOrigin" = 'access'
               AND "TipPromene" = 'Nivelacija'
               AND "ArtikalId" IS NOT NULL) AS imported_line_rows,
            (SELECT COUNT(*) FROM "DnevnikPromena"
             WHERE "DataOrigin" = 'access'
               AND "TipPromene" IN ('Nivelacija', 'Nivelacija cena')
               AND "ArtikalId" IS NULL) AS imported_header_rows,
            (SELECT COUNT(*) FROM "vw_sales_pre_nivelacija") AS pre_rows,
            (SELECT COUNT(*) FROM "vw_sales_post_nivelacija") AS post_rows,
            (SELECT COUNT(*) FROM "vw_vendor_sales_nivelacija") AS vendor_rows,
            (SELECT COUNT(DISTINCT price_event_id) FROM "vw_vendor_sales_nivelacija") AS vendor_distinct_events,
            (SELECT COUNT(DISTINCT "BrojRacuna") FROM "DnevnikPromena"
             WHERE "DataOrigin" = 'access'
               AND "TipPromene" = 'Nivelacija'
               AND "ArtikalId" IS NOT NULL
               AND "BrojRacuna" ~ '^-?[0-9]+$') AS imported_distinct_source_headers,
            (SELECT COALESCE(SUM(pre_qty), 0) FROM "vw_sales_pre_nivelacija") AS pre_qty_sum,
            (SELECT COALESCE(SUM(pre_revenue), 0) FROM "vw_sales_pre_nivelacija") AS pre_revenue_sum,
            (SELECT COALESCE(SUM(post_qty), 0) FROM "vw_sales_post_nivelacija") AS post_qty_sum,
            (SELECT COALESCE(SUM(post_revenue), 0) FROM "vw_sales_post_nivelacija") AS post_revenue_sum,
            (SELECT COALESCE(SUM(pre_qty), 0) FROM "vw_vendor_sales_nivelacija") AS vendor_pre_qty_sum,
            (SELECT COALESCE(SUM(pre_revenue), 0) FROM "vw_vendor_sales_nivelacija") AS vendor_pre_revenue_sum,
            (SELECT COALESCE(SUM(post_qty), 0) FROM "vw_vendor_sales_nivelacija") AS vendor_post_qty_sum,
            (SELECT COALESCE(SUM(post_revenue), 0) FROM "vw_vendor_sales_nivelacija") AS vendor_post_revenue_sum
        """
    )

    comparisons = {
        "access_lines_match_vendor_rows": int_equal(access["access_line_rows"], postgres["vendor_rows"]),
        "pre_qty_matches_vendor_qty": decimal_equal(postgres["pre_qty_sum"], postgres["vendor_pre_qty_sum"]),
        "pre_revenue_matches_vendor_revenue": decimal_equal(
            postgres["pre_revenue_sum"], postgres["vendor_pre_revenue_sum"]
        ),
        "post_qty_matches_vendor_qty": decimal_equal(postgres["post_qty_sum"], postgres["vendor_post_qty_sum"]),
        "post_revenue_matches_vendor_revenue": decimal_equal(
            postgres["post_revenue_sum"], postgres["vendor_post_revenue_sum"]
        ),
        "access_events_match_imported_source_headers": int_equal(
            access["access_distinct_events"], postgres["imported_distinct_source_headers"]
        ),
    }

    return {
        "access": access,
        "postgres": postgres,
        "comparisons": comparisons,
    }


def edge_case_checks(ctx: AuditContext) -> dict[str, Any]:
    checks = ctx.fetch_pg_one(
        """
        SELECT
            (SELECT COUNT(*) FROM (
                SELECT "ArtikalId", "Datum"::date, COALESCE("Iznos", 0), COUNT(*)
                FROM "DnevnikPromena"
                WHERE "DataOrigin" = 'access'
                  AND "TipPromene" = 'Nivelacija'
                  AND "ArtikalId" IS NOT NULL
                GROUP BY 1, 2, 3
                HAVING COUNT(*) > 1
            ) duplicates) AS imported_duplicate_groups,
            (SELECT COUNT(*) FROM (
                SELECT event_date::date, COALESCE(vendor_id, -1), article_id, old_price, new_price, COUNT(*)
                FROM "vw_vendor_sales_nivelacija"
                GROUP BY 1, 2, 3, 4, 5
                HAVING COUNT(*) > 1
            ) duplicates) AS view_duplicate_groups,
            (SELECT COUNT(*) FROM "vw_vendor_sales_nivelacija"
             WHERE COALESCE(pre_qty, 0) = 0 OR COALESCE(post_qty, 0) = 0) AS zero_sales_period_rows,
            (SELECT COUNT(*) FROM "vw_vendor_sales_nivelacija"
             WHERE COALESCE(pre_qty, 0) = 0
               AND COALESCE(post_qty, 0) = 0
               AND COALESCE(pre_revenue, 0) = 0
               AND COALESCE(post_revenue, 0) = 0) AS inactive_rows,
            (SELECT COUNT(*) FROM (
                SELECT article_id, event_date::date, COUNT(*)
                FROM "vw_vendor_sales_nivelacija"
                GROUP BY 1, 2
                HAVING COUNT(*) > 1
            ) multi_change) AS multiple_changes_same_day_rows,
            NULL::integer AS out_of_stock_event_rows
        """
    )

    has_stock_red_zone = ctx.fetch_pg_one(
        "SELECT to_regclass('public.\"vw_stock_red_zone\"') IS NOT NULL AS has_view"
    )
    if has_stock_red_zone["has_view"]:
        oos_rows = ctx.fetch_pg_one(
            """
            SELECT COUNT(*) AS out_of_stock_event_rows
            FROM (
                SELECT v.article_id, v.event_date::date
                FROM "vw_vendor_sales_nivelacija" v
                JOIN "vw_stock_red_zone" s
                  ON UPPER(TRIM(COALESCE(s.sku, ''))) = UPPER(TRIM(COALESCE(v.sku, '')))
                WHERE s.is_oos
                GROUP BY 1, 2
            ) oos_rows
            """
        )
        checks["out_of_stock_event_rows"] = oos_rows["out_of_stock_event_rows"]
        checks["out_of_stock_check_status"] = "available"
    else:
        checks["out_of_stock_check_status"] = "skipped_missing_vw_stock_red_zone"

    access_multi = ctx.fetch_access_one(
        """
        SELECT COUNT(*) AS access_multiple_changes_same_day_rows
        FROM (
            SELECT n.IDArtikal, d.Datum, COUNT(*) AS cnt
            FROM tblNivelacije AS n
            LEFT JOIN tblDnevnikPromena AS d ON n.IDDnevnik = d.IDDnevnik
            GROUP BY n.IDArtikal, d.Datum
            HAVING COUNT(*) > 1
        ) AS grouped_rows
        """
    )

    checks.update(access_multi)
    return checks


def fetch_recomputed_window_metrics(ctx: AuditContext, article_id: int, event_date: date) -> dict[str, Any]:
    pre = ctx.fetch_pg_one(
        """
        SELECT
            COALESCE(SUM(ps."kolicina"), 0) AS qty,
            COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS revenue
        FROM "prodaja_stavke" ps
        JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
        WHERE ps."id_artikal" = %s
          AND pz."datum_prodaje" >= %s::date - INTERVAL '30 days'
          AND pz."datum_prodaje" < %s::date
        """,
        (article_id, event_date, event_date),
    )

    post = ctx.fetch_pg_one(
        """
        SELECT
            COALESCE(SUM(ps."kolicina"), 0) AS qty,
            COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS revenue
        FROM "prodaja_stavke" ps
        JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
        WHERE ps."id_artikal" = %s
          AND pz."datum_prodaje" >= %s::date
          AND pz."datum_prodaje" < %s::date + INTERVAL '30 days'
        """,
        (article_id, event_date, event_date),
    )

    return {
        "pre_qty": pre["qty"],
        "pre_revenue": pre["revenue"],
        "post_qty": post["qty"],
        "post_revenue": post["revenue"],
    }


def sample_row_checks(ctx: AuditContext, sample_size: int) -> list[dict[str, Any]]:
    sampled_rows = ctx.fetch_pg_all(
        """
        SELECT
            price_event_id,
            event_date::date AS event_date,
            vendor_id,
            vendor_name,
            article_id,
            sku,
            old_price,
            new_price,
            pre_qty,
            pre_revenue,
            post_qty,
            post_revenue
        FROM "vw_vendor_sales_nivelacija"
        ORDER BY random()
        LIMIT %s
        """,
        (sample_size,),
    )

    samples: list[dict[str, Any]] = []
    for sampled_row in sampled_rows:
        pg_dnevnik = ctx.fetch_pg_one(
            """
            SELECT
                "Id",
                "TipPromene",
                "Datum"::date AS datum,
                "ArtikalId",
                "StaraProdajnaCena",
                "NovaProdajnaCena",
                "BrojRacuna",
                "DobavljacId",
                "IDObjekat",
                "Iznos"
            FROM "DnevnikPromena"
            WHERE "Id" = %s
            """,
            (sampled_row["price_event_id"],),
        )

        source_header_id: int | None = None
        broj_racuna = None if pg_dnevnik is None else pg_dnevnik.get("BrojRacuna")
        if isinstance(broj_racuna, str) and broj_racuna.lstrip("-").isdigit():
            source_header_id = int(broj_racuna)

        access_row = None
        if source_header_id is not None:
            access_row = ctx.fetch_access_one(
                """
                SELECT TOP 1
                    n.IDArtikal,
                    n.Kolicina,
                    n.StaraCena,
                    n.NovaCena,
                    n.IDDnevnik,
                    d.Datum,
                    d.TipPromene,
                    d.IDObjekat,
                    d.IznosPromene,
                    a.IDDobavljac
                FROM (tblNivelacije AS n
                    LEFT JOIN tblDnevnikPromena AS d ON n.IDDnevnik = d.IDDnevnik)
                    LEFT JOIN tblArtikli AS a ON n.IDArtikal = a.IDArtikal
                WHERE n.IDArtikal = ?
                  AND n.IDDnevnik = ?
                """,
                (sampled_row["article_id"], source_header_id),
            )

        recomputed = fetch_recomputed_window_metrics(ctx, sampled_row["article_id"], sampled_row["event_date"])

        checks = {
            "access_article_match": access_row is not None and int_equal(access_row["IDArtikal"], sampled_row["article_id"]),
            "access_old_price_match": access_row is not None and decimal_equal(access_row["StaraCena"], sampled_row["old_price"]),
            "access_new_price_match": access_row is not None and decimal_equal(access_row["NovaCena"], sampled_row["new_price"]),
            "access_date_match": access_row is not None and date_equal(access_row["Datum"], sampled_row["event_date"]),
            "access_vendor_match": access_row is not None and int_equal(access_row["IDDobavljac"], sampled_row["vendor_id"]),
            "import_article_match": pg_dnevnik is not None and int_equal(pg_dnevnik["ArtikalId"], sampled_row["article_id"]),
            "import_old_price_match": pg_dnevnik is not None and decimal_equal(pg_dnevnik["StaraProdajnaCena"], sampled_row["old_price"]),
            "import_new_price_match": pg_dnevnik is not None and decimal_equal(pg_dnevnik["NovaProdajnaCena"], sampled_row["new_price"]),
            "import_date_match": pg_dnevnik is not None and date_equal(pg_dnevnik["datum"], sampled_row["event_date"]),
            "import_vendor_match": pg_dnevnik is not None and int_equal(pg_dnevnik["DobavljacId"], sampled_row["vendor_id"]),
            "recomputed_pre_qty_match": int_equal(recomputed["pre_qty"], sampled_row["pre_qty"]),
            "recomputed_post_qty_match": int_equal(recomputed["post_qty"], sampled_row["post_qty"]),
            "recomputed_pre_revenue_match": decimal_equal(recomputed["pre_revenue"], sampled_row["pre_revenue"]),
            "recomputed_post_revenue_match": decimal_equal(recomputed["post_revenue"], sampled_row["post_revenue"]),
        }

        samples.append(
            {
                "view": sampled_row,
                "imported_dnevnik": pg_dnevnik,
                "access_source": access_row,
                "recomputed_sales_window": recomputed,
                "checks": checks,
            }
        )

    return samples


def advanced_metric_origins() -> dict[str, Any]:
    return {
        "avgMomentumRevenue": {
            "article_field": "MomentumRevenue",
            "origin": "SQL view + code aggregation",
            "details": "Filled per article from vw_sales_momentum in MapRollingAndMomentumToNivelacijaArticlesAsync, then averaged in AllEndpoints.cs.",
        },
        "avgElasticity": {
            "article_field": "PriceElasticity",
            "origin": "code computed",
            "details": "Computed in MapElasticityAndLostSalesToNivelacijaArticles from article-level pre/post qty and PriceChangePercent, then averaged in AllEndpoints.cs.",
        },
        "avgDidRevenue": {
            "article_field": "DidRevenue",
            "origin": "SQL view + code aggregation",
            "details": "Filled per article from vw_nivelacija_did in MapOosAndDidToNivelacijaArticlesAsync, then averaged in AllEndpoints.cs.",
        },
        "avgLostSalesOOS": {
            "article_field": "LostSalesOOS",
            "origin": "SQL signal + code computed",
            "details": "Uses OOSRate from vw_stock_red_zone via MapOosAndDidToNivelacijaArticlesAsync; LostSalesOOS is then computed in MapElasticityAndLostSalesToNivelacijaArticles and averaged in AllEndpoints.cs.",
        },
    }


def build_lineage_summary() -> dict[str, Any]:
    return {
        "source_layer": {
            "tables": {
                "tblNivelacije": {
                    "key_fields": ["IDArtikal", "Kolicina", "StaraCena", "NovaCena", "IDDnevnik"],
                    "notes": "Line-level price change rows. No native date column; linked to tblDnevnikPromena via IDDnevnik.",
                },
                "tblDnevnikPromena": {
                    "key_fields": ["IDDnevnik", "Datum", "TipPromene", "IznosPromene", "IDObjekat", "Napomena"],
                    "notes": "Header/event layer used to inherit nivelacija event date and object/store context.",
                },
                "tblArtikli": {
                    "key_fields": ["IDArtikal", "IDDobavljac", "PLU", "Artikal"],
                    "notes": "Provides supplier lineage for article rows when vendor is not directly carried by tblNivelacije.",
                },
            }
        },
        "import_layer": {
            "target_table": "DnevnikPromena",
            "normalization_rules": [
                "ImportNivelacije in AccessImportService treats tblNivelacije as line-level and copies event date from source tblDnevnikPromena via IDDnevnik.",
                "Per-line Iznos is recomputed as abs((NovaCena - StaraCena) * Kolicina), not copied from Access header IznosPromene.",
                "Composite dedup key during import is (ArtikalId, Datum, Iznos).",
                "BrojRacuna stores the source IDDnevnik so line rows can be back-linked to the Access header.",
            ],
            "key_fields": [
                "Id",
                "TipPromene",
                "Datum",
                "ArtikalId",
                "StaraProdajnaCena",
                "NovaProdajnaCena",
                "BrojRacuna",
                "DobavljacId",
                "DataOrigin",
            ],
        },
        "sql_view_layer": {
            "vw_sales_pre_nivelacija": {
                "key_fields": [
                    "price_event_id",
                    "event_date",
                    "article_id",
                    "vendor_id",
                    "old_price",
                    "new_price",
                    "pre_qty",
                    "pre_revenue",
                    "coverage_pre30",
                ],
                "transformation_logic": "Builds one deduplicated event row per article/event/price combination from imported DnevnikPromena, then aggregates sales in the 30-day window before event_date.",
            },
            "vw_sales_post_nivelacija": {
                "key_fields": [
                    "price_event_id",
                    "event_date",
                    "article_id",
                    "vendor_id",
                    "post_qty",
                    "post_revenue",
                    "coverage_post30",
                ],
                "transformation_logic": "Aggregates sales in the 30-day window on or after event_date for the same deduplicated event rows.",
            },
            "vw_vendor_sales_nivelacija": {
                "key_fields": [
                    "price_event_id",
                    "event_date",
                    "vendor_id",
                    "vendor_name",
                    "article_id",
                    "old_price",
                    "new_price",
                    "pre_qty",
                    "post_qty",
                    "change_revenue",
                    "change_percent_revenue",
                ],
                "transformation_logic": "Joins pre and post views and computes change_qty/change_revenue plus percent deltas. Keeps one row per imported price event line.",
            },
        },
        "api_layer": {
            "endpoint": "/api/analytics/vendor-sales-nivelacija",
            "filtering_rules": [
                "Filters by vendorId, eventDate, from/to on event_date, category ILIKE, includeInactive, and row cap maxRows.",
                "Runtime dedup from SQL query uses ROW_NUMBER partitioned by event_date, vendor_id, article_id, old_price, new_price (or a reduced partition if price columns are missing).",
                "Rows with unchanged old/new price are removed from analyzed output; inactive rows are removed unless includeInactive=true.",
            ],
            "computed_in_code": [
                "HasSalesWindow",
                "PriceChanged",
                "PriceChangePercent",
                "CategoryStats",
                "PriceDirectionStats",
                "Insights",
                "avgMomentumRevenue / avgElasticity / avgDidRevenue / avgLostSalesOOS aggregation",
                "MetricsStatus accumulation",
            ],
        },
        "frontend_layer": {
            "file": "Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx",
            "current_rendered_metrics": [
                "totals.postRevenue",
                "top5SharePct",
                "totals.changeRevenue",
                "periodGrowthPct",
                "vendorStats-derived share/trend/reliability/decisionScore/status",
            ],
            "underused_backend_fields": [
                "dataQuality",
                "metricsStatus",
                "insights",
                "categoryStats",
                "priceDirectionStats",
                "articleStats",
                "avgMomentumRevenue",
                "avgElasticity",
                "avgDidRevenue",
                "avgLostSalesOOS",
            ],
        },
    }


def summarize_sample_checks(samples: list[dict[str, Any]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for sample in samples:
        for key, passed in sample["checks"].items():
            counts[key] = counts.get(key, 0) + int(bool(passed))
    return counts


def main() -> None:
    args = parse_args()

    access_conn = connect_access(args.mdb_path)
    pg_conn = psycopg2.connect(args.pg_url)

    try:
        ctx = AuditContext(access_conn=access_conn, pg_conn=pg_conn)
        repair_summary = repair_live_rows(ctx) if args.repair_live_rows else None
        results = {
            "lineage": build_lineage_summary(),
            "advanced_metric_origins": advanced_metric_origins(),
            "aggregate_checks": aggregate_checks(ctx),
            "edge_case_checks": edge_case_checks(ctx),
            "sample_checks": sample_row_checks(ctx, args.sample_size),
        }
        if repair_summary is not None:
            results["repair_summary"] = repair_summary
        results["sample_check_summary"] = summarize_sample_checks(results["sample_checks"])

        args.output_json.parent.mkdir(parents=True, exist_ok=True)
        args.output_json.write_text(json.dumps(results, indent=2, default=json_default), encoding="utf-8")

        aggregate = results["aggregate_checks"]
        if repair_summary is not None:
            print("Repair summary:")
            for key, value in repair_summary.items():
                if key == "samples":
                    continue
                print(f"- {key}: {value}")
            print()

        print("Aggregate comparisons:")
        for key, passed in aggregate["comparisons"].items():
            print(f"- {key}: {'PASS' if passed else 'FAIL'}")

        print("\nEdge cases:")
        for key, value in results["edge_case_checks"].items():
            print(f"- {key}: {value}")

        print("\nSample summary:")
        for key, value in results["sample_check_summary"].items():
            print(f"- {key}: {value}/{args.sample_size}")

        print(f"\nWrote audit results to {args.output_json}")
    finally:
        pg_conn.close()
        access_conn.close()


if __name__ == "__main__":
    main()