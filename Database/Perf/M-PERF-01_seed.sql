-- M-PERF-01: pilot-like performance seed (disposable DB only)
-- Recipe: M-PERF-01 | RNG label: M-PERF-01-2026
-- Targets: 12k products, 45k sale headers, 180k sale lines, 180-day window, 5 stores, 8 suppliers

\set ON_ERROR_STOP on

DO $$
DECLARE
    v_as_of timestamptz := TIMESTAMPTZ '2026-08-12 06:00:00+00';
    v_from timestamptz := v_as_of - INTERVAL '180 days';
    v_tip_id integer;
    v_sezona_id integer;
    v_dob_ids integer[];
    v_i integer;
BEGIN
    RAISE NOTICE 'M-PERF-01 seed starting. asOf=%, from=%', v_as_of, v_from;

    -- dimension anchors
    INSERT INTO "TipoviObuce" ("Naziv")
    SELECT 'Patike'
    WHERE NOT EXISTS (SELECT 1 FROM "TipoviObuce" WHERE "Naziv" = 'Patike');

    SELECT "Id" INTO v_tip_id FROM "TipoviObuce" WHERE "Naziv" = 'Patike' LIMIT 1;

    INSERT INTO "Sezone" ("Naziv", "DatumOd", "DatumDo")
    SELECT 'M-PERF Sezona 2026', v_from, v_as_of
    WHERE NOT EXISTS (SELECT 1 FROM "Sezone" WHERE "Naziv" = 'M-PERF Sezona 2026');

    SELECT "Id" INTO v_sezona_id FROM "Sezone" WHERE "Naziv" = 'M-PERF Sezona 2026' LIMIT 1;

    FOR v_i IN 1..8 LOOP
        INSERT INTO "Dobavljaci" ("Naziv", "Adresa", "Telefon", "Napomena")
        SELECT
            'M-PERF Supplier ' || v_i,
            'Perf Street ' || v_i || ', Beograd',
            '+381 11 100 ' || LPAD(v_i::text, 4, '0'),
            'M-PERF-01 seed supplier'
        WHERE NOT EXISTS (SELECT 1 FROM "Dobavljaci" WHERE "Naziv" = 'M-PERF Supplier ' || v_i);
    END LOOP;

    SELECT ARRAY_AGG("Id" ORDER BY "Id") INTO v_dob_ids
    FROM "Dobavljaci"
    WHERE "Naziv" LIKE 'M-PERF Supplier %';

    -- clear prior perf sales/products
    TRUNCATE TABLE prodaja_stavke RESTART IDENTITY CASCADE;
    TRUNCATE TABLE prodaja_zaglavlje RESTART IDENTITY CASCADE;
    DELETE FROM "OutboxMessages" WHERE "EventType" = 'ProdajaKreirana';
    DELETE FROM "Artikli" WHERE "Naziv" LIKE 'M-PERF Product %';

    INSERT INTO "Artikli" (
        "Naziv", "IDTipObuce", "IDDobavljac", "IDSezona", "IDObjekat",
        "NabavnaCena", "ProdajnaCena", "Kolicina", "Velicina", "Boja",
        "UpdatedAt")
    SELECT
        'M-PERF Product ' || g,
        v_tip_id,
        v_dob_ids[1 + ((g - 1) % array_length(v_dob_ids, 1))],
        v_sezona_id,
        1 + ((g - 1) % 5),
        2000 + (g % 7000),
        4000 + (g % 9000),
        3 + (g % 20),
        (40 + (g % 6))::text,
        CASE (g % 4) WHEN 0 THEN 'Crna' WHEN 1 THEN 'Bela' WHEN 2 THEN 'Plava' ELSE 'Siva' END,
        v_as_of
    FROM generate_series(1, 12000) AS g;

    INSERT INTO prodaja_zaglavlje (broj_racuna, datum_prodaje, nacin_placanja, id_objekat)
    SELECT
        'MPERF-' || LPAD(g::text, 6, '0'),
        v_from + ((g::numeric / 45000.0) * (v_as_of - v_from)),
        CASE WHEN g % 2 = 0 THEN 'Gotovina' ELSE 'Kartica' END,
        1 + ((g - 1) % 5)
    FROM generate_series(1, 45000) AS g;

    INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena)
    SELECT
        1 + ((g - 1) / 4),
        1 + ((g - 1) % 12000),
        1 + (g % 3),
        3000 + (g % 6000)
    FROM generate_series(1, 180000) AS g;

    RAISE NOTICE 'M-PERF-01 seed complete.';
END $$;

-- verification counts for evidence harness
SELECT 'Artikli' AS entity, COUNT(*) AS row_count FROM "Artikli" WHERE "Naziv" LIKE 'M-PERF Product %'
UNION ALL
SELECT 'ProdajaZaglavlja', COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'MPERF-%'
UNION ALL
SELECT 'ProdajaStavke', COUNT(*) FROM prodaja_stavke ps JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja WHERE pz.broj_racuna LIKE 'MPERF-%'
UNION ALL
SELECT 'Dobavljaci', COUNT(*) FROM "Dobavljaci" WHERE "Naziv" LIKE 'M-PERF Supplier %';
