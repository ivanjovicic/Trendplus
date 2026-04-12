const { Client, types } = require('pg');

// Fix pg DATE type parsing - use UTC instead of local timezone
types.setTypeParser(1082, val => val); // return DATE as raw string

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) Latest 30 days - correct dates
  const latest = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    WHERE pz.datum_prodaje >= (SELECT MAX(datum_prodaje)::date - interval '35 days' FROM prodaja_zaglavlje)
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('=== LATEST DATA (with Artikli+Dobavljaci join, like service) ===');
  console.log('sale_date  | total_qty | total_revenue');
  latest.rows.forEach(r => {
    console.log(`${r.sale_date} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 2) Same query WITHOUT Artikli join (just stavke+zaglavlje)
  const latestNoArt = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= (SELECT MAX(datum_prodaje)::date - interval '35 days' FROM prodaja_zaglavlje)
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== LATEST DATA (WITHOUT Artikli join) ===');
  console.log('sale_date  | total_qty | total_revenue');
  latestNoArt.rows.forEach(r => {
    console.log(`${r.sale_date} | ${String(r.total_qty).padStart(9)} | ${r.total_revenue}`);
  });

  // 3) Check if there are stavke with missing Artikli
  const missingArt = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      COUNT(*) AS orphan_count,
      SUM(ps.kolicina) AS orphan_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje >= (SELECT MAX(datum_prodaje)::date - interval '35 days' FROM prodaja_zaglavlje)
      AND a."Id" IS NULL
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== MISSING ARTIKLI (orphan stavke) ===');
  if (missingArt.rows.length === 0) {
    console.log('None found - all stavke have matching Artikli');
  } else {
    missingArt.rows.forEach(r => {
      console.log(`${r.sale_date} | orphans: ${r.orphan_count}, qty: ${r.orphan_qty}`);
    });
  }

  // 4) Shift classification simulation for latest data
  const shiftSim = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      SUM(ps.kolicina) AS total_qty,
      SUM(CASE WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 6 AND 13 THEN ps.kolicina ELSE 0 END) AS shift1,
      SUM(CASE WHEN EXTRACT(HOUR FROM pz.datum_prodaje) BETWEEN 14 AND 21 THEN ps.kolicina ELSE 0 END) AS shift2,
      SUM(CASE WHEN EXTRACT(HOUR FROM pz.datum_prodaje) NOT BETWEEN 6 AND 21 THEN ps.kolicina ELSE 0 END) AS offshift,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    WHERE pz.datum_prodaje >= (SELECT MAX(datum_prodaje)::date - interval '35 days' FROM prodaja_zaglavlje)
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== SHIFT SIMULATION (fallback: all→shift1 since all hours=0) ===');
  console.log('sale_date  | total | s1 | s2 | off | revenue');
  shiftSim.rows.forEach(r => {
    console.log(`${r.sale_date} | ${String(r.total_qty).padStart(5)} | ${String(r.shift1).padStart(2)} | ${String(r.shift2).padStart(2)} | ${String(r.offshift).padStart(3)} | ${r.revenue}`);
  });

  // 5) Check Apr 5-6 2026 spike (487 items)
  const spike = await client.query(`
    SELECT
      pz.datum_prodaje::text AS raw_timestamp,
      pz.datum_prodaje::date::text AS sale_date,
      COUNT(*) AS num_stavke,
      SUM(ps.kolicina) AS total_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date BETWEEN '2026-04-05' AND '2026-04-06'
    GROUP BY pz.datum_prodaje, pz.datum_prodaje::date
    ORDER BY pz.datum_prodaje
  `);
  console.log('\n=== APR 5-6 2026 SPIKE ANALYSIS ===');
  console.log('raw_timestamp                     | sale_date  | stavke | qty');
  spike.rows.forEach(r => {
    console.log(`${r.raw_timestamp.padEnd(33)} | ${r.sale_date} | ${String(r.num_stavke).padStart(6)} | ${r.total_qty}`);
  });

  // 6) Check for data in daily_sales_facts (pre-aggregated table)
  const factsCheck = await client.query(`
    SELECT COUNT(*) AS cnt FROM daily_sales_facts
    WHERE sale_date >= '2026-03-01'
  `);
  console.log('\n=== daily_sales_facts (pre-aggregated) COUNT for 2026-03+ ===');
  console.log(`Count: ${factsCheck.rows[0].cnt}`);
  
  if (parseInt(factsCheck.rows[0].cnt) > 0) {
    const facts = await client.query(`
      SELECT sale_date::text, total_items, total_revenue
      FROM daily_sales_facts
      WHERE sale_date >= '2026-03-01'
      ORDER BY sale_date DESC
      LIMIT 20
    `);
    console.log('sale_date  | total_items | total_revenue');
    facts.rows.forEach(r => {
      console.log(`${r.sale_date} | ${String(r.total_items).padStart(11)} | ${r.total_revenue}`);
    });
  }

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
