const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // Check zaglavlje IDs for the 3 receipts on 2026-03-26
  const zags = await client.query(`
    SELECT id, broj_racuna, datum_prodaje::text, data_origin
    FROM prodaja_zaglavlje
    WHERE broj_racuna IN ('DUG','309','310')
      AND datum_prodaje::date = '2026-03-26'
    ORDER BY broj_racuna, id
  `);
  console.log('=== ZAGLAVLJA FOR 2026-03-26 ===');
  zags.rows.forEach(r => {
    console.log(`id=${r.id} | racun=${r.broj_racuna} | date=${r.datum_prodaje} | origin=${r.data_origin}`);
  });

  // Check how many stavke per zaglavlje ID  
  const stavkePerZag = await client.query(`
    SELECT ps.id_prodaja, pz.broj_racuna, COUNT(*) AS cnt, SUM(ps.kolicina) AS qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric,2) AS rev
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date = '2026-03-26'
    GROUP BY ps.id_prodaja, pz.broj_racuna
    ORDER BY pz.broj_racuna, ps.id_prodaja
  `);
  console.log('\n=== STAVKE PER ZAGLAVLJE ===');
  stavkePerZag.rows.forEach(r => {
    console.log(`zag_id=${r.id_prodaja} | racun=${r.broj_racuna} | stavki=${r.cnt} | qty=${r.qty} | rev=${r.rev}`);
  });

  // Check if DUG receipt has duplicated zaglavlje in other dates  
  const dugAll = await client.query(`
    SELECT id, broj_racuna, datum_prodaje::date::text AS sale_date, data_origin,
      (SELECT COUNT(*) FROM prodaja_stavke WHERE id_prodaja = pz.id) AS num_stavke,
      (SELECT SUM(kolicina) FROM prodaja_stavke WHERE id_prodaja = pz.id) AS total_qty
    FROM prodaja_zaglavlje pz
    WHERE broj_racuna = 'DUG'
    ORDER BY datum_prodaje
  `);
  console.log('\n=== ALL "DUG" RECEIPTS ACROSS ALL DATES ===');
  dugAll.rows.forEach(r => {
    console.log(`id=${r.id} | date=${r.sale_date} | stavke=${r.num_stavke} | qty=${r.total_qty} | origin=${r.data_origin}`);
  });

  // Check stavke detail for each DUG zaglavlje on Mar 26
  const dugStavke = await client.query(`
    SELECT ps.id, ps.id_prodaja, ps.id_artikal, ps.kolicina, ps.cena
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.broj_racuna = 'DUG' AND pz.datum_prodaje::date = '2026-03-26'
    ORDER BY ps.id_prodaja, ps.id_artikal
  `);
  console.log('\n=== DUG STAVKE ON 2026-03-26 ===');
  dugStavke.rows.forEach(r => {
    console.log(`stavka=${r.id} | zag=${r.id_prodaja} | artikal=${r.id_artikal} | qty=${r.kolicina} | cena=${r.cena}`);
  });

  // CRITICAL: Check for duplicated prodaja_stavke across the WHOLE db for same zaglavlje
  // Are stavke 65584-65623 and 131273-131312 duplicates?
  const dupeAnalysis = await client.query(`
    WITH s1 AS (
      SELECT id_prodaja, id_artikal, kolicina, cena, COUNT(*) AS cnt
      FROM prodaja_stavke
      WHERE id_prodaja IN (
        SELECT id FROM prodaja_zaglavlje WHERE datum_prodaje::date = '2026-03-26'
      )
      GROUP BY id_prodaja, id_artikal, kolicina, cena
      HAVING COUNT(*) > 1
    )
    SELECT * FROM s1 ORDER BY id_prodaja, id_artikal
  `);
  console.log('\n=== DUPLICATE STAVKE (same zag+artikal+qty+cena) ON 2026-03-26 ===');
  console.log(`Found ${dupeAnalysis.rows.length} dupes`);
  dupeAnalysis.rows.forEach(r => {
    console.log(`zag=${r.id_prodaja} | artikal=${r.id_artikal} | qty=${r.kolicina} | cena=${r.cena} | count=${r.cnt}`);
  });

  // Are there globally duplicate stavke per zaglavlje?
  const globalDupes = await client.query(`
    SELECT id_prodaja, id_artikal, kolicina, cena, COUNT(*) AS cnt
    FROM prodaja_stavke
    GROUP BY id_prodaja, id_artikal, kolicina, cena
    HAVING COUNT(*) > 2
    ORDER BY cnt DESC
    LIMIT 20
  `);
  console.log('\n=== GLOBAL DUPLICATES (>2 same rows per zaglavlje) ===');
  console.log(`Found ${globalDupes.rows.length}`);
  globalDupes.rows.forEach(r => {
    console.log(`zag=${r.id_prodaja} | artikal=${r.id_artikal} | qty=${r.kolicina} | cnt=${r.cnt}`);
  });

  // The real check: Half of 465680 = 232840. Is that closer to Access 106110?
  // 465680 / 80 items = 5821 avg price. 106110 / 40 items = 2652.75 avg price. Hmm.
  // Let's check: what's the sum for only the FIRST set of stavke (ids 65584-65623)?
  const firstHalf = await client.query(`
    SELECT COUNT(*) AS cnt, SUM(kolicina) AS qty, ROUND(SUM(kolicina * cena)::numeric, 2) AS rev
    FROM prodaja_stavke
    WHERE id BETWEEN 65584 AND 65623
  `);
  console.log('\n=== FIRST BATCH (stavke 65584-65623) ===');
  console.log(`count=${firstHalf.rows[0].cnt}, qty=${firstHalf.rows[0].qty}, rev=${firstHalf.rows[0].rev}`);

  const secondHalf = await client.query(`
    SELECT COUNT(*) AS cnt, SUM(kolicina) AS qty, ROUND(SUM(kolicina * cena)::numeric, 2) AS rev
    FROM prodaja_stavke
    WHERE id BETWEEN 131273 AND 131312
  `);
  console.log(`\n=== SECOND BATCH (stavke 131273-131312) ===`);
  console.log(`count=${secondHalf.rows[0].cnt}, qty=${secondHalf.rows[0].qty}, rev=${secondHalf.rows[0].rev}`);

  // Check total across the whole month in 2026 - how much is doubled?
  const wholePeriod = await client.query(`
    WITH dupe_check AS (
      SELECT id_prodaja, id_artikal, kolicina, cena, COUNT(*) AS cnt
      FROM prodaja_stavke ps
      JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
      WHERE pz.datum_prodaje >= '2026-03-01' AND pz.datum_prodaje < '2026-04-15'
      GROUP BY id_prodaja, id_artikal, kolicina, cena
    )
    SELECT
      SUM(CASE WHEN cnt > 1 THEN cnt ELSE 0 END) AS duplicated_rows,
      SUM(CASE WHEN cnt > 1 THEN (cnt - 1) ELSE 0 END) AS extra_rows,
      SUM(CASE WHEN cnt > 1 THEN (cnt - 1) * kolicina * cena ELSE 0 END) AS extra_revenue,
      COUNT(*) AS total_unique_combos
    FROM dupe_check
  `);
  console.log('\n=== DUPLICATION SUMMARY (2026 Mar-Apr) ===');
  const ws = wholePeriod.rows[0];
  console.log(`duplicated_rows=${ws.duplicated_rows}, extra_rows=${ws.extra_rows}, extra_revenue=${ws.extra_revenue}`);

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
