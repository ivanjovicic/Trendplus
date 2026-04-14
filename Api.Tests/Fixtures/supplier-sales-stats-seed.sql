-- ============================================================================
-- Fixture: supplier-sales-stats comprehensive test dataset
-- Purpose: Seed a test Trendplus database with realistic supplier, article,
--          sales, and related data to test the supplier-sales-stats endpoint
--          under controlled conditions.
-- ============================================================================

-- Clear existing data (safe for test DB only)
TRUNCATE TABLE "ProdajaStavke" CASCADE;
TRUNCATE TABLE "ProdajaZaglavlja" CASCADE;
TRUNCATE TABLE "Artikli" CASCADE;
TRUNCATE TABLE "Dobavljaci" CASCADE;
TRUNCATE TABLE "Sezone" CASCADE;

-- Insert seasons
INSERT INTO "Sezone" ("Naziv", "DatumOd", "DatumDo")
VALUES
  ('Test Season 1', '2026-01-01', '2026-03-31'),
  ('Test Season 2', '2026-04-01', '2026-06-30');

-- Insert suppliers (some with known IDs, one null for unknown bucket)
INSERT INTO "Dobavljaci" ("Naziv")
VALUES
  ('Supplier A'),
  ('Supplier B'),
  ('Supplier C'),
  (''),  -- Empty name: should map to 'Nepoznato'
  (NULL)  -- NULL name: should map to 'Nepoznato'
RETURNING "Id";

-- Assume supplier IDs: 1-5 (adjust if needed)

-- Insert articles
INSERT INTO "Artikli" ("PLU", "Naziv", "IDDobavljac", "NabavnaCenaDin", "NabavnaCena", "DataOrigin")
VALUES
  ('ART001', 'Article 1 - Supplier A', 1, 50.00, 50.00, 'existing'),
  ('ART002', 'Article 2 - Supplier A', 1, 60.00, 60.00, 'existing'),
  ('ART003', 'Article 3 - Supplier B', 2, 100.00, 100.00, 'existing'),
  ('ART004', 'Article 4 - Supplier B', 2, NULL, 80.00, 'existing'),  -- Null cost
  ('ART005', 'Article 5 - Supplier C', 3, 25.00, 25.00, 'imported'),
  ('ART006', 'Article 6 - Unknown (Null Supplier)', NULL, 40.00, 40.00, 'existing')
RETURNING "Id";

-- Assume article IDs: 1-6 (adjust if needed)

-- Insert sales declarations (ProdajaZaglavlja)
INSERT INTO "ProdajaZaglavlja" ("DatumProdaje", "IDObjekat")
VALUES
  ('2026-02-15', 1),
  ('2026-02-16', 1),
  ('2026-02-20', 1),
  ('2026-03-01', 1),
  ('2026-03-15', 1)
RETURNING "Id";

-- Assume sale IDs: 1-5 (adjust if needed)

-- Insert sales line items (ProdajaStavke)
-- Test Date: 2026-02-15
INSERT INTO "ProdajaStavke" ("IdProdaja", "IdArtikal", "Kolicina", "Cena", "NabavnaCena")
VALUES
  (1, 1, 10, 100.00, 50.00),  -- Supplier A, Article 1
  (1, 3, 5, 200.00, 100.00),  -- Supplier B, Article 3

-- Test Date: 2026-02-16
  (2, 2, 15, 120.00, 60.00),  -- Supplier A, Article 2
  (2, 4, 8, 180.00, NULL),    -- Supplier B, Article 4 (missing cost)

-- Test Date: 2026-02-20
  (3, 5, 20, 50.00, 25.00),   -- Supplier C, Article 5
  (3, 6, 12, 80.00, 40.00),   -- Unknown supplier, Article 6

-- Test Date: 2026-03-01
  (4, 1, 5, 105.00, 50.00),   -- Supplier A, Article 1 (price change)
  (4, 3, 3, 210.00, 100.00),  -- Supplier B, Article 3

-- Test Date: 2026-03-15
  (5, 2, 25, 120.00, 60.00),  -- Supplier A, Article 2 (high volume)
  (5, 5, 10, 52.00, 25.00);   -- Supplier C, Article 5 (price change)

-- Insert reference price history (DnevnikPromena) for nivelacija tracking
-- This fixture assumes no explicit nivelacija events; they can be added on demand.
-- If nivelacija data is needed, add entries with TipPromene = 'Nivelacija' or 'Nivelacija cena'

-- ============================================================================
-- Fixture stats summary (for manual verification):
-- Suppliers: 3 known (A, B, C) + 2 unknown (empty + null name)
-- Articles: 6 total (5 known supplier + 1 unknown)
-- Sales: 5 declarations spanning 2026-02-15 to 2026-03-15
-- Line items: 10
-- Data quality issues: 1 missing cost (ART004), 1 unknown supplier (ART006)
-- ============================================================================
