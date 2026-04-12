const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) Daily totals - mirrors DailySalesStatsService query
  const daily = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('=== DAILY TOTALS (DB - all data_origins) ===');
  console.log('sale_date      | total_qty | total_revenue');
  daily.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 2) Check hour distribution
  const hours = await client.query(`
    SELECT
      EXTRACT(HOUR FROM pz.datum_prodaje)::int AS hour_of_day,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
    GROUP BY EXTRACT(HOUR FROM pz.datum_prodaje)::int
    ORDER BY hour_of_day
  `);
  console.log('\n=== HOUR DISTRIBUTION ===');
  console.log('hour | total_qty');
  hours.rows.forEach(r => {
    console.log(`  ${String(r.hour_of_day).padStart(2)} | ${r.total_qty}`);
  });

  // 3) Daily totals filtered by Artikli.DataOrigin (like the service does with existingOnly)
  const dailyExisting = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
      AND (a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = '')
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== DAILY TOTALS (existing only) ===');
  console.log('sale_date      | total_qty | total_revenue');
  dailyExisting.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 4) Daily totals filtered by Artikli.DataOrigin = 'access' (importedOnly)
  const dailyAccess = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
      AND a."DataOrigin" = 'access'
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== DAILY TOTALS (access/imported only) ===');
  console.log('sale_date      | total_qty | total_revenue');
  dailyAccess.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 5) Shift classification check - items per shift
  const shifts = await client.query(`
    SELECT
      pz.datum_prodaje::date AS sale_date,
      CASE
        WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 6 AND 13 THEN 1
        WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 14 AND 21 THEN 2
        ELSE 0
      END AS shift,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
    GROUP BY pz.datum_prodaje::date, 
      CASE
        WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 6 AND 13 THEN 1
        WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 14 AND 21 THEN 2
        ELSE 0
      END
    ORDER BY sale_date DESC, shift
  `);
  console.log('\n=== SHIFT BREAKDOWN (0=off-shift/excluded) ===');
  console.log('sale_date      | shift | total_qty');
  shifts.rows.forEach(r => {
    const d = r.sale_date.toISOString().slice(0, 10);
    console.log(`${d} |     ${r.shift} | ${String(r.total_qty).padStart(9)}`);
  });

  // 6) Check data_origin on prodaja_zaglavlje too
  const zagOrigins = await client.query(`
    SELECT
      COALESCE(pz.data_origin, 'NULL') AS zag_origin,
      COUNT(DISTINCT pz.id) AS num_sales,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
    GROUP BY COALESCE(pz.data_origin, 'NULL')
    ORDER BY total_qty DESC
  `);
  console.log('\n=== ZAGLAVLJE DATA_ORIGIN DISTRIBUTION ===');
  console.log('zag_origin      | num_sales | total_qty');
  zagOrigins.rows.forEach(r => {
    console.log(`${r.zag_origin.padEnd(15)} | ${String(r.num_sales).padStart(9)} | ${r.total_qty}`);
  });

  // 7) Artikli data_origin distribution for items sold
  const artOrigins = await client.query(`
    SELECT
      COALESCE(a."DataOrigin", 'NULL') AS art_origin,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje >= '2025-03-12'
      AND pz.datum_prodaje < '2025-04-12'
    GROUP BY COALESCE(a."DataOrigin", 'NULL')
    ORDER BY total_qty DESC
  `);
  console.log('\n=== ARTIKLI DATA_ORIGIN DISTRIBUTION ===');
  console.log('art_origin      | total_qty');
  artOrigins.rows.forEach(r => {
    console.log(`${r.art_origin.padEnd(15)} | ${r.total_qty}`);
  });

  // 8) Check date range in database
  const dateRange = await client.query(`
    SELECT
      MIN(datum_prodaje) AS min_date,
      MAX(datum_prodaje) AS max_date,
      COUNT(*) AS total_records
    FROM prodaja_zaglavlje
  `);
  console.log('\n=== DATE RANGE IN DB ===');
  dateRange.rows.forEach(r => {
    console.log(`Min: ${r.min_date}, Max: ${r.max_date}, Total records: ${r.total_records}`);
  });

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
