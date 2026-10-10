-- RQ607 read-only oracle for InventoryStatus scope verification.
-- This script intentionally contains SELECTs only. It compares the analytics
-- ProductsDim snapshot with current Artikli low-stock evidence and does not
-- update, delete, import or clear cache/data.
-- Adjust the UTC window before running against a permitted local database.

WITH requested_scopes(scope_name) AS (
    VALUES ('imported'), ('existing'), ('all')
), scoped_snapshot AS (
    SELECT
        s.scope_name,
        p."Kolicina"
    FROM requested_scopes s
    JOIN "ProductsDim" p
      ON p."Timestamp" >= TIMESTAMPTZ '2026-10-10 00:00:00+00'
     AND p."Timestamp" <= TIMESTAMPTZ '2026-10-11 00:00:00+00'
     AND (
            s.scope_name = 'all'
         OR (s.scope_name = 'imported' AND p."DataOrigin" = 'access')
         OR (s.scope_name = 'existing' AND (p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = ''))
     )
)
SELECT
    scope_name,
    COUNT(*)::integer AS total_sku_count,
    COALESCE(SUM(CASE WHEN "Kolicina" > 0 THEN "Kolicina" ELSE 0 END), 0)::integer AS total_on_hand,
    COUNT(*) FILTER (WHERE "Kolicina" = 0)::integer AS out_of_stock_count
FROM scoped_snapshot
GROUP BY scope_name
ORDER BY scope_name;

WITH requested_scopes(scope_name) AS (
    VALUES ('imported'), ('existing'), ('all')
)
SELECT
    s.scope_name,
    COUNT(*) FILTER (
        WHERE a."Kolicina" IS NOT NULL
          AND a."Kolicina" > 0
          AND (
                (a."MinimalnaKolicina" > 0 AND a."Kolicina" <= a."MinimalnaKolicina")
             OR ((a."MinimalnaKolicina" IS NULL OR a."MinimalnaKolicina" <= 0) AND a."Kolicina" <= 2)
          )
    )::integer AS low_stock_count,
    COUNT(*) FILTER (WHERE a."Kolicina" IS NULL)::integer AS unknown_quantity_count,
    COUNT(*)::integer AS operational_rows
FROM requested_scopes s
LEFT JOIN "Artikli" a
  ON a."UpdatedAt" >= TIMESTAMPTZ '2026-10-10 00:00:00+00'
 AND a."UpdatedAt" <= TIMESTAMPTZ '2026-10-11 00:00:00+00'
 AND (
        s.scope_name = 'all'
     OR (s.scope_name = 'imported' AND a."DataOrigin" = 'access')
     OR (s.scope_name = 'existing' AND (a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = ''))
 )
GROUP BY s.scope_name
ORDER BY s.scope_name;
