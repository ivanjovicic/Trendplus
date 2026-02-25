-- ============================================
-- ANALYTICS OPTIMIZATION - Indexes Only (Simple Version)
-- Run on: PostgreSQL (Neon)
-- ============================================

-- ============================================
-- PART 1: INDEXES FOR FASTER QUERIES
-- ============================================

-- Index on ProdajaZaglavlja.DatumProdaje (most important!)
CREATE INDEX IF NOT EXISTS idx_prodaja_datum 
ON "ProdajaZaglavlja" ("DatumProdaje" DESC);

-- Index on ProdajaStavke for JOIN operations
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_prodaja 
ON "ProdajaStavke" ("IdProdaja");

CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal 
ON "ProdajaStavke" ("IdArtikal");

-- Index on Artikli for category/supplier grouping
CREATE INDEX IF NOT EXISTS idx_artikli_kategorija 
ON "Artikli" ("Kategorija");

CREATE INDEX IF NOT EXISTS idx_artikli_dobavljac 
ON "Artikli" ("IDDobavljac");

CREATE INDEX IF NOT EXISTS idx_artikli_pol 
ON "Artikli" ("Pol");

-- ============================================
-- PART 2: ANALYTICS PRE-COMPUTED VIEWS
-- ============================================

-- View for daily sales (much faster than ad-hoc queries)
CREATE OR REPLACE VIEW "vw_daily_sales" AS
SELECT 
    DATE("DatumProdaje") as "date",
    COUNT(DISTINCT p."Id") as "transaction_count",
    SUM(ps."Kolicina") as "total_units",
    SUM(ps."Kolicina" * ps."Cena") as "total_revenue"
FROM "ProdajaZaglavlja" p
JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
GROUP BY DATE("DatumProdaje")
ORDER BY DATE("DatumProdaje") DESC;

-- View for category sales
CREATE OR REPLACE VIEW "vw_category_sales" AS
SELECT 
    COALESCE(a."Kategorija", 'Ostalo') as "kategorija",
    COALESCE(a."Pol", 'Neodređeno') as "pol",
    SUM(ps."Kolicina" * ps."Cena") as "total_revenue",
    SUM(ps."Kolicina") as "total_units",
    COUNT(DISTINCT p."Id") as "transaction_count"
FROM "ProdajaZaglavlja" p
JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
GROUP BY a."Kategorija", a."Pol"
ORDER BY "total_revenue" DESC;

-- View for supplier sales
CREATE OR REPLACE VIEW "vw_supplier_sales" AS
SELECT 
    d."Id" as "dobavljac_id",
    COALESCE(d."Naziv", 'Nepoznato') as "dobavljac_naziv",
    SUM(ps."Kolicina" * ps."Cena") as "total_revenue",
    SUM(ps."Kolicina") as "total_units",
    COUNT(DISTINCT p."Id") as "transaction_count"
FROM "ProdajaZaglavlja" p
JOIN "ProdajaStavke" ps ON p."Id" = ps."IdProdaja"
JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
GROUP BY d."Id", d."Naziv"
ORDER BY "total_revenue" DESC;

-- View for top products
CREATE OR REPLACE VIEW "vw_top_products" AS
SELECT 
    a."Id" as "product_id",
    a."Naziv" as "product_name",
    SUM(ps."Kolicina" * ps."Cena") as "total_revenue",
    SUM(ps."Kolicina") as "total_units"
FROM "ProdajaStavke" ps
JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
GROUP BY a."Id", a."Naziv"
ORDER BY "total_revenue" DESC;

-- ============================================
-- VERIFICATION
-- ============================================
SELECT 'Indexes created' AS status;
SELECT 'Views created' AS status;

-- Test the views
SELECT COUNT(*) as daily_count FROM "vw_daily_sales";
SELECT COUNT(*) as category_count FROM "vw_category_sales";
SELECT COUNT(*) as supplier_count FROM "vw_supplier_sales";
SELECT COUNT(*) as products_count FROM "vw_top_products";
