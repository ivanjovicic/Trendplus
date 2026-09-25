-- ============================================================================
-- Fixture: shoe-type-sales-stats comprehensive test dataset
-- Purpose: Seed a test Trendplus database with realistic shoe types,
--          articles, sales, and related data to test the shoe-type-sales-stats
--          endpoint under controlled conditions.
-- ============================================================================

-- Clear existing data (shared tables)
TRUNCATE TABLE prodaja_stavke, prodaja_zaglavlje, "Artikli", "TipoviObuce", "Sezone"
  RESTART IDENTITY CASCADE;

-- Insert seasons
INSERT INTO "Sezone" ("Naziv", "DatumOd", "DatumDo")
VALUES
  ('Test Season Summer', '2026-06-01', '2026-08-31');

-- Insert shoe types
INSERT INTO "TipoviObuce" ("Naziv")
VALUES
  ('Patike'),      -- ID 1
  ('Cipele'),      -- ID 2
  ('Sandale'),     -- ID 3
  (''),            -- ID 4 (Empty name -> Nepoznato)
  ('')             -- ID 5 (empty name -> Nepoznato)
RETURNING "Id";

-- Insert articles with shoe types
INSERT INTO "Artikli" ("PLU", "Naziv", "IDTipObuce", "NabavnaCenaDin", "NabavnaCena", "DataOrigin")
VALUES
  ('ART-P1', 'Patika Runner', 1, 50.00, 50.00, 'existing'),
  ('ART-P2', 'Patika Walker', 1, 60.00, 60.00, 'existing'),
  ('ART-C1', 'Cipela Formal', 2, 100.00, 100.00, 'existing'),
  ('ART-C2', 'Cipela Casual', 2, NULL, NULL, 'existing'),
  ('ART-S1', 'Sandala Beach', 3, 25.00, 25.00, 'access'),
  ('ART-U1', 'Unknown Type Artikal', NULL, 40.00, 40.00, 'existing')
RETURNING "Id";

-- Insert sales (ProdajaZaglavlja)
INSERT INTO prodaja_zaglavlje (datum_prodaje, id_objekat, data_origin)
VALUES
  ('2026-06-15', 1, 'existing'),
  ('2026-06-16', 1, 'existing'),
  ('2026-07-01', 1, 'access')
RETURNING id;

-- Insert sales line items (ProdajaStavke)
INSERT INTO prodaja_stavke
  (id_prodaja, id_artikal, kolicina, cena, nabavna_cena, shoe_type_id_at_sale, attribution_basis)
VALUES
  -- 2026-06-15
  (1, 1, 10, 100.00, 50.00, 1, 'sale_snapshot'), -- Patike
  (1, 3, 5, 200.00, 100.00, 2, 'sale_snapshot'), -- Cipele
  
  -- 2026-06-16
  (2, 2, 15, 120.00, 60.00, 1, 'sale_snapshot'), -- Patike
  (2, 4, 8, 180.00, NULL, 2, 'sale_snapshot'),   -- Cipele (missing cost)
  
  -- 2026-07-01
  (3, 5, 20, 50.00, 25.00, 3, 'sale_snapshot'),  -- Sandale
  (3, 6, 12, 80.00, 40.00, NULL, 'sale_snapshot');  -- Unknown Type

-- Insert Nivelacija for split testing
-- TipPromene constants: Nivelacija=2, NivelacijaCena=3 (common in the app)
INSERT INTO "DnevnikPromena" ("ArtikalId", "Datum", "TipPromene", "Iznos", "IDObjekat")
VALUES
  (1, '2026-06-15 12:00:00', 'Nivelacija', 0.00, 1), -- Nivelacija for Patika Runner mid-sale date
  (5, '2026-06-30 09:00:00', 'Nivelacija cena', 0.00, 1); -- Nivelacija for Sandala Beach before 2026-07-01 sale

-- ============================================================================
-- Fixture summary:
-- Types: Patike, Cipele, Sandale, Unknown
-- Articles: 6
-- Total Sales: 3 sessions, 6 lines
-- Nivelacija: 2 events
-- ============================================================================
