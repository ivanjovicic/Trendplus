-- =============================================
-- Trendplus2 Analytics Dashboard Enhancements
-- Compatible with current Trendplus schema
-- =============================================

-- 1) Performance indexes
CREATE INDEX IF NOT EXISTS idx_prodaja_datum ON "prodaja_zaglavlje" ("datum_prodaje" DESC);
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja ON "prodaja_stavke" ("id_prodaja");
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal ON "prodaja_stavke" ("id_artikal");
CREATE INDEX IF NOT EXISTS idx_artikli_kategorija ON "Artikli" ("Kategorija");
CREATE INDEX IF NOT EXISTS idx_artikli_dobavljac ON "Artikli" ("IDDobavljac");
CREATE INDEX IF NOT EXISTS idx_artikli_pol ON "Artikli" ("Pol");

-- 2) Pre-aggregated tables
CREATE TABLE IF NOT EXISTS "AnalyticsDailySummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL UNIQUE,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalTransactions" INT NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "AvgBasketValue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "AvgItemPrice" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_daily_summary_date ON "AnalyticsDailySummary" ("Date" DESC);

CREATE TABLE IF NOT EXISTS "AnalyticsCategorySummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "Kategorija" VARCHAR(100) NOT NULL,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "TransactionCount" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "Kategorija")
);
CREATE INDEX IF NOT EXISTS idx_category_summary_date ON "AnalyticsCategorySummary" ("Date" DESC);

CREATE TABLE IF NOT EXISTS "AnalyticsSupplierSummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "DobavljacId" INT,
    "DobavljacNaziv" VARCHAR(200),
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "TransactionCount" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "DobavljacId")
);
CREATE INDEX IF NOT EXISTS idx_supplier_summary_date ON "AnalyticsSupplierSummary" ("Date" DESC);

CREATE TABLE IF NOT EXISTS "AnalyticsGenderSummary" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "Pol" VARCHAR(50) NOT NULL,
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "Pol")
);
CREATE INDEX IF NOT EXISTS idx_gender_summary_date ON "AnalyticsGenderSummary" ("Date" DESC);

CREATE TABLE IF NOT EXISTS "AnalyticsTopProducts" (
    "Id" SERIAL PRIMARY KEY,
    "Date" DATE NOT NULL,
    "ProductId" INT NOT NULL,
    "ProductName" VARCHAR(300),
    "TotalRevenue" DECIMAL(18,2) NOT NULL DEFAULT 0,
    "TotalUnits" INT NOT NULL DEFAULT 0,
    "Rank" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE("Date", "ProductId")
);
CREATE INDEX IF NOT EXISTS idx_top_products_date ON "AnalyticsTopProducts" ("Date" DESC);

-- 3) Daily summary upsert
INSERT INTO "AnalyticsDailySummary" ("Date", "TotalRevenue", "TotalTransactions", "TotalUnits", "AvgBasketValue", "AvgItemPrice", "UpdatedAt")
SELECT
  d."Date",
  d."TotalRevenue",
  d."TotalTransactions",
  d."TotalUnits",
  d."AvgBasketValue",
  d."AvgItemPrice",
  NOW()
FROM (
  SELECT
    DATE(p."datum_prodaje") AS "Date",
    COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS "TotalRevenue",
    COUNT(DISTINCT p."id") AS "TotalTransactions",
    COALESCE(SUM(ps."kolicina"), 0) AS "TotalUnits",
    CASE WHEN COUNT(DISTINCT p."id") > 0 THEN COALESCE(SUM(ps."kolicina" * ps."cena"), 0) / COUNT(DISTINCT p."id") ELSE 0 END AS "AvgBasketValue",
    CASE WHEN COALESCE(SUM(ps."kolicina"), 0) > 0 THEN COALESCE(SUM(ps."kolicina" * ps."cena"), 0) / SUM(ps."kolicina") ELSE 0 END AS "AvgItemPrice"
  FROM "prodaja_zaglavlje" p
  JOIN "prodaja_stavke" ps ON p."id" = ps."id_prodaja"
  GROUP BY DATE(p."datum_prodaje")
) d
ON CONFLICT ("Date") DO UPDATE SET
  "TotalRevenue" = EXCLUDED."TotalRevenue",
  "TotalTransactions" = EXCLUDED."TotalTransactions",
  "TotalUnits" = EXCLUDED."TotalUnits",
  "AvgBasketValue" = EXCLUDED."AvgBasketValue",
  "AvgItemPrice" = EXCLUDED."AvgItemPrice",
  "UpdatedAt" = NOW();

-- 4) Category summary upsert
INSERT INTO "AnalyticsCategorySummary" ("Date", "Kategorija", "TotalRevenue", "TotalUnits", "TransactionCount", "UpdatedAt")
SELECT
  DATE(p."datum_prodaje") AS "Date",
  COALESCE(a."Kategorija", 'Ostalo') AS "Kategorija",
  COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS "TotalRevenue",
  COALESCE(SUM(ps."kolicina"), 0) AS "TotalUnits",
  COUNT(DISTINCT p."id") AS "TransactionCount",
  NOW()
FROM "prodaja_zaglavlje" p
JOIN "prodaja_stavke" ps ON p."id" = ps."id_prodaja"
JOIN "Artikli" a ON a."Id" = ps."id_artikal"
GROUP BY DATE(p."datum_prodaje"), COALESCE(a."Kategorija", 'Ostalo')
ON CONFLICT ("Date", "Kategorija") DO UPDATE SET
  "TotalRevenue" = EXCLUDED."TotalRevenue",
  "TotalUnits" = EXCLUDED."TotalUnits",
  "TransactionCount" = EXCLUDED."TransactionCount",
  "UpdatedAt" = NOW();

-- 5) Supplier summary upsert
INSERT INTO "AnalyticsSupplierSummary" ("Date", "DobavljacId", "DobavljacNaziv", "TotalRevenue", "TotalUnits", "TransactionCount", "UpdatedAt")
SELECT
  DATE(p."datum_prodaje") AS "Date",
  d."Id" AS "DobavljacId",
  COALESCE(d."Naziv", 'Nepoznato') AS "DobavljacNaziv",
  COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS "TotalRevenue",
  COALESCE(SUM(ps."kolicina"), 0) AS "TotalUnits",
  COUNT(DISTINCT p."id") AS "TransactionCount",
  NOW()
FROM "prodaja_zaglavlje" p
JOIN "prodaja_stavke" ps ON p."id" = ps."id_prodaja"
JOIN "Artikli" a ON a."Id" = ps."id_artikal"
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
GROUP BY DATE(p."datum_prodaje"), d."Id", COALESCE(d."Naziv", 'Nepoznato')
ON CONFLICT ("Date", "DobavljacId") DO UPDATE SET
  "DobavljacNaziv" = EXCLUDED."DobavljacNaziv",
  "TotalRevenue" = EXCLUDED."TotalRevenue",
  "TotalUnits" = EXCLUDED."TotalUnits",
  "TransactionCount" = EXCLUDED."TransactionCount",
  "UpdatedAt" = NOW();

-- 6) Gender summary upsert
INSERT INTO "AnalyticsGenderSummary" ("Date", "Pol", "TotalRevenue", "TotalUnits", "UpdatedAt")
SELECT
  DATE(p."datum_prodaje") AS "Date",
  COALESCE(a."Pol", 'Neodredjeno') AS "Pol",
  COALESCE(SUM(ps."kolicina" * ps."cena"), 0) AS "TotalRevenue",
  COALESCE(SUM(ps."kolicina"), 0) AS "TotalUnits",
  NOW()
FROM "prodaja_zaglavlje" p
JOIN "prodaja_stavke" ps ON p."id" = ps."id_prodaja"
JOIN "Artikli" a ON a."Id" = ps."id_artikal"
GROUP BY DATE(p."datum_prodaje"), COALESCE(a."Pol", 'Neodredjeno')
ON CONFLICT ("Date", "Pol") DO UPDATE SET
  "TotalRevenue" = EXCLUDED."TotalRevenue",
  "TotalUnits" = EXCLUDED."TotalUnits",
  "UpdatedAt" = NOW();

-- 7) Top products upsert
WITH ranked AS (
  SELECT
    DATE(p."datum_prodaje") AS "Date",
    a."Id" AS "ProductId",
    COALESCE(a."Naziv", 'Nepoznato') AS "ProductName",
    SUM(ps."kolicina" * ps."cena") AS "TotalRevenue",
    SUM(ps."kolicina") AS "TotalUnits",
    ROW_NUMBER() OVER (
      PARTITION BY DATE(p."datum_prodaje")
      ORDER BY SUM(ps."kolicina" * ps."cena") DESC, a."Id"
    ) AS "Rank"
  FROM "prodaja_zaglavlje" p
  JOIN "prodaja_stavke" ps ON p."id" = ps."id_prodaja"
  JOIN "Artikli" a ON a."Id" = ps."id_artikal"
  GROUP BY DATE(p."datum_prodaje"), a."Id", COALESCE(a."Naziv", 'Nepoznato')
)
INSERT INTO "AnalyticsTopProducts" ("Date", "ProductId", "ProductName", "TotalRevenue", "TotalUnits", "Rank", "UpdatedAt")
SELECT
  r."Date",
  r."ProductId",
  r."ProductName",
  r."TotalRevenue",
  r."TotalUnits",
  r."Rank",
  NOW()
FROM ranked r
WHERE r."Rank" <= 100
ON CONFLICT ("Date", "ProductId") DO UPDATE SET
  "ProductName" = EXCLUDED."ProductName",
  "TotalRevenue" = EXCLUDED."TotalRevenue",
  "TotalUnits" = EXCLUDED."TotalUnits",
  "Rank" = EXCLUDED."Rank",
  "UpdatedAt" = NOW();

-- 8) Moving averages
CREATE OR REPLACE VIEW vw_analytics_daily_ma AS
SELECT
  d."Date",
  d."TotalRevenue",
  d."TotalTransactions",
  d."TotalUnits",
  AVG(d."TotalRevenue") OVER (ORDER BY d."Date" ROWS BETWEEN 6 PRECEDING AND CURRENT ROW) AS "MA7_Revenue",
  AVG(d."TotalRevenue") OVER (ORDER BY d."Date" ROWS BETWEEN 29 PRECEDING AND CURRENT ROW) AS "MA30_Revenue",
  AVG(d."TotalTransactions") OVER (ORDER BY d."Date" ROWS BETWEEN 6 PRECEDING AND CURRENT ROW) AS "MA7_Transactions"
FROM "AnalyticsDailySummary" d;

-- 9) Velocity per SKU (units/day for selected period)
CREATE OR REPLACE VIEW vw_analytics_velocity AS
SELECT
  COALESCE(a."PLU", a."Id"::text) AS sku,
  a."Id" AS article_id,
  COALESCE(a."Naziv", 'Nepoznato') AS article_name,
  COALESCE(a."Kategorija", 'Ostalo') AS category,
  COALESCE(d."Naziv", 'Nepoznato') AS vendor_name,
  SUM(ps."kolicina")::decimal / GREATEST(COUNT(DISTINCT DATE(p."datum_prodaje")), 1) AS velocity_units_per_day,
  SUM(ps."kolicina" * ps."cena")::decimal / GREATEST(COUNT(DISTINCT DATE(p."datum_prodaje")), 1) AS velocity_revenue_per_day
FROM "prodaja_stavke" ps
JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
JOIN "Artikli" a ON a."Id" = ps."id_artikal"
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
GROUP BY COALESCE(a."PLU", a."Id"::text), a."Id", COALESCE(a."Naziv", 'Nepoznato'), COALESCE(a."Kategorija", 'Ostalo'), COALESCE(d."Naziv", 'Nepoznato');

-- 10) OOS and lost-sales proxy
CREATE OR REPLACE VIEW vw_analytics_oos_lost_sales AS
WITH daily AS (
  SELECT
    ps."id_artikal" AS article_id,
    DATE(p."datum_prodaje") AS sale_day,
    SUM(ps."kolicina")::decimal AS units_day
  FROM "prodaja_stavke" ps
  JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
  WHERE p."datum_prodaje" >= NOW() - INTERVAL '30 days'
  GROUP BY ps."id_artikal", DATE(p."datum_prodaje")
),
rates AS (
  SELECT
    article_id,
    AVG(units_day) AS avg_units_per_day
  FROM daily
  GROUP BY article_id
)
SELECT
  COALESCE(a."PLU", a."Id"::text) AS sku,
  a."Id" AS article_id,
  CASE WHEN COALESCE(a."Kolicina", 0) <= 0 THEN 1 ELSE 0 END AS is_oos,
  CASE WHEN COALESCE(a."Kolicina", 0) > 0 AND COALESCE(a."Kolicina", 0) <= COALESCE(a."MinimalnaKolicina", 1) THEN 1 ELSE 0 END AS is_low_stock,
  COALESCE(r.avg_units_per_day, 0) AS avg_units_per_day,
  CASE WHEN COALESCE(a."Kolicina", 0) <= 0 THEN COALESCE(r.avg_units_per_day, 0) ELSE 0 END AS lost_sales_estimate
FROM "Artikli" a
LEFT JOIN rates r ON r.article_id = a."Id";

-- 11) Revenue contribution and Pareto
CREATE OR REPLACE VIEW vw_analytics_pareto AS
WITH ranked AS (
  SELECT
    COALESCE(a."PLU", a."Id"::text) AS sku,
    a."Id" AS article_id,
    COALESCE(a."Naziv", 'Nepoznato') AS article_name,
    SUM(ps."kolicina" * ps."cena") AS revenue
  FROM "prodaja_stavke" ps
  JOIN "Artikli" a ON a."Id" = ps."id_artikal"
  GROUP BY COALESCE(a."PLU", a."Id"::text), a."Id", COALESCE(a."Naziv", 'Nepoznato')
)
SELECT
  r.*,
  CASE WHEN SUM(r.revenue) OVER () = 0 THEN 0
       ELSE SUM(r.revenue) OVER (ORDER BY r.revenue DESC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) / SUM(r.revenue) OVER ()
  END AS cumulative_share
FROM ranked r;

-- 12) Data completeness and freshness
CREATE OR REPLACE VIEW vw_analytics_data_health AS
SELECT
  CASE
    WHEN COUNT(*) = 0 THEN 0
    ELSE 1.0 - (COUNT(*) FILTER (
      WHERE a."Naziv" IS NULL OR a."PLU" IS NULL OR a."Kategorija" IS NULL
    )::decimal / COUNT(*)::decimal)
  END AS completeness_score,
  MAX(a."UpdatedAt") AS last_import
FROM "Artikli" a;

-- 13) Inventory health
CREATE OR REPLACE VIEW vw_analytics_inventory_health AS
SELECT
  COUNT(*) AS total_sku_count,
  COALESCE(SUM(COALESCE(a."Kolicina", 0)), 0) AS total_on_hand,
  COUNT(*) FILTER (WHERE COALESCE(a."Kolicina", 0) > 0 AND COALESCE(a."Kolicina", 0) <= COALESCE(a."MinimalnaKolicina", 1)) AS low_stock_count,
  COUNT(*) FILTER (WHERE COALESCE(a."Kolicina", 0) = 0) AS out_of_stock_count
FROM "Artikli" a;
