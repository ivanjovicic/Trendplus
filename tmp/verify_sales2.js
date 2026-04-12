const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) Check for data in 2026 (max date showed 2026)
  const recent = await client.query(`
    SELECT
      datum_prodaje::date AS sale_date,
      COUNT(*) AS num_records,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2026-01-01'
    GROUP BY datum_prodaje::date
    ORDER BY sale_date DESC
    LIMIT 30
  `);
  console.log('=== DATA IN 2026+ ===');
  console.log('sale_date      | num_records | total_qty');
  recent.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${String(r.num_records).padStart(11)} | ${r.total_qty}`);
  });

  // 2) Check for items in prodaja_stavke that DON'T match Artikli (orphans)
  const orphans = await client.query(`
    SELECT COUNT(*) AS orphan_count, SUM(ps.kolicina) AS orphan_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
      AND a."Id" IS NULL
  `);
  console.log('\n=== ORPHAN STAVKE (no matching Artikli) ===');
  console.log(`Count: ${orphans.rows[0].orphan_count}, Qty: ${orphans.rows[0].orphan_qty}`);

  // 3) Spot check: total items across ALL shifts vs what service would return
  // Service excludes shift=0 items UNLESS useNoTimeDataFallback is true
  // Since ALL hours are 0, the fallback maps everything to shift 1
  const fallbackCheck = await client.query(`
    WITH classified AS (
      SELECT
        pz.datum_prodaje::date AS sale_date,
        ps.kolicina,
        ps.cena,
        EXTRACT(HOUR FROM pz.datum_prodaje)::int AS hour_of_day,
        CASE
          WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 6 AND 13 THEN 1
          WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 14 AND 21 THEN 2
          ELSE 0
        END AS shift
      FROM prodaja_stavke ps
      JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
      JOIN "Artikli" a ON ps.id_artikal = a."Id"
      LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
      WHERE pz.datum_prodaje >= '2025-03-12'
        AND pz.datum_prodaje < '2025-04-12'
    )
    SELECT
      sale_date,
      SUM(kolicina) AS total_items,
      ROUND(SUM(kolicina * cena)::numeric, 2) AS total_revenue,
      SUM(CASE WHEN shift = 1 THEN kolicina ELSE 0 END) AS shift1_qty,
      SUM(CASE WHEN shift = 2 THEN kolicina ELSE 0 END) AS shift2_qty,
      SUM(CASE WHEN shift = 0 THEN kolicina ELSE 0 END) AS offshift_qty
    FROM classified
    GROUP BY sale_date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== SERVICE SIMULATION (with Artikli+Dobavljaci join) ===');
  console.log('sale_date      | total | shift1 | shift2 | off | revenue');
  fallbackCheck.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${String(r.total_items).padStart(5)} | ${String(r.shift1_qty).padStart(6)} | ${String(r.shift2_qty).padStart(6)} | ${String(r.offshift_qty).padStart(3)} | ${r.total_revenue}`);
  });

  // 4) Count total records by year
  const yearDist = await client.query(`
    SELECT
      EXTRACT(YEAR FROM datum_prodaje)::int AS year,
      COUNT(*) AS num_sales,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    GROUP BY EXTRACT(YEAR FROM datum_prodaje)::int
    ORDER BY year DESC
    LIMIT 10
  `);
  console.log('\n=== SALES BY YEAR ===');
  console.log('year | num_sales | total_qty');
  yearDist.rows.forEach(r => {
    console.log(`${r.year} | ${String(r.num_sales).padStart(9)} | ${r.total_qty}`);
  });

  // 5) Duplicate check - same sale appearing twice in different date ranges?
  const dupes = await client.query(`
    SELECT ps.id, COUNT(*) AS cnt
    FROM prodaja_stavke ps
    GROUP BY ps.id
    HAVING COUNT(*) > 1
    LIMIT 5
  `);
  console.log('\n=== DUPLICATE STAVKE IDs ===');
  console.log(`Found: ${dupes.rows.length}`);

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
