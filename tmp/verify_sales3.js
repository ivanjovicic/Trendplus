const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) 2026 data with data_origin info
  const data2026 = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      pz.data_origin AS zag_origin,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2026-03-01'
      AND pz.datum_prodaje < '2026-04-15'
    GROUP BY pz.datum_prodaje::date, pz.data_origin
    ORDER BY sale_date DESC
  `);
  console.log('=== 2026 MARCH-APRIL DATA ===');
  console.log('sale_date      | origin  | total_qty | revenue');
  data2026.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${(r.zag_origin||'NULL').padEnd(7)} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 2) Compare equivalent months: same day in 2025 vs 2026
  const comparison = await client.query(`
    WITH d25 AS (
      SELECT pz.datum_prodaje::date AS sale_date, SUM(ps.kolicina) AS qty
      FROM prodaja_stavke ps
      JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
      WHERE pz.datum_prodaje >= '2025-03-01' AND pz.datum_prodaje < '2025-04-15'
      GROUP BY pz.datum_prodaje::date
    ),
    d26 AS (
      SELECT pz.datum_prodaje::date AS sale_date, SUM(ps.kolicina) AS qty
      FROM prodaja_stavke ps
      JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
      WHERE pz.datum_prodaje >= '2026-03-01' AND pz.datum_prodaje < '2026-04-15'
      GROUP BY pz.datum_prodaje::date
    )
    SELECT
      COALESCE(TO_CHAR(d25.sale_date, 'MM-DD'), TO_CHAR(d26.sale_date - interval '1 year', 'MM-DD')) AS month_day,
      d25.qty AS qty_2025,
      d26.qty AS qty_2026
    FROM d25
    FULL OUTER JOIN d26 ON d25.sale_date + interval '1 year' = d26.sale_date
    ORDER BY month_day
  `);
  console.log('\n=== 2025 vs 2026 COMPARISON (same month-day) ===');
  console.log('month-day | qty_2025 | qty_2026');
  comparison.rows.forEach(r => {
    console.log(`${(r.month_day||'??').padEnd(9)} | ${String(r.qty_2025||'-').padStart(8)} | ${String(r.qty_2026||'-').padStart(8)}`);
  });

  // 3) Check if there are duplicate sales (same broj_racuna in both years)
  const dupeReceipts = await client.query(`
    SELECT broj_racuna, COUNT(*) as cnt,
      array_agg(DISTINCT EXTRACT(YEAR FROM datum_prodaje)::int ORDER BY EXTRACT(YEAR FROM datum_prodaje)::int) AS years
    FROM prodaja_zaglavlje
    WHERE datum_prodaje >= '2025-03-01'
    GROUP BY broj_racuna
    HAVING COUNT(DISTINCT EXTRACT(YEAR FROM datum_prodaje)::int) > 1
    LIMIT 20
  `);
  console.log('\n=== RECEIPTS APPEARING IN MULTIPLE YEARS ===');
  console.log(`Found: ${dupeReceipts.rows.length}`);
  dupeReceipts.rows.slice(0, 10).forEach(r => {
    console.log(`  ${r.broj_racuna} => count=${r.cnt}, years=${r.years}`);
  });

  // 4) Sample records from 2025 and 2026 for the same receipt number
  if (dupeReceipts.rows.length > 0) {
    const sampleReceipt = dupeReceipts.rows[0].broj_racuna;
    const samples = await client.query(`
      SELECT id, broj_racuna, datum_prodaje, data_origin
      FROM prodaja_zaglavlje
      WHERE broj_racuna = $1
      ORDER BY datum_prodaje
    `, [sampleReceipt]);
    console.log(`\n=== SAMPLE RECEIPT: ${sampleReceipt} ===`);
    samples.rows.forEach(r => {
      console.log(`  id=${r.id}, date=${r.datum_prodaje}, origin=${r.data_origin}`);
    });
  }

  // 5) Check what this month's most recent data looks like (what user likely sees)
  const latestMonth = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue,
      pz.data_origin
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= (SELECT MAX(datum_prodaje)::date - interval '30 days' FROM prodaja_zaglavlje)
    GROUP BY pz.datum_prodaje::date, pz.data_origin
    ORDER BY sale_date DESC
  `);
  console.log('\n=== LATEST 30 DAYS OF DATA ===');
  console.log('sale_date      | origin  | total_qty | revenue');
  latestMonth.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${(r.data_origin||'NULL').padEnd(7)} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
