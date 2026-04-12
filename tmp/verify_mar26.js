const { Client, types } = require('pg');
types.setTypeParser(1082, val => val);

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) All stavke for March 26, 2026 - detailed
  const detail = await client.query(`
    SELECT
      ps.id AS stavka_id,
      ps.id_prodaja,
      ps.id_artikal,
      ps.kolicina,
      ps.cena,
      ps.kolicina * ps.cena AS line_total,
      pz.broj_racuna,
      pz.datum_prodaje::text AS raw_date,
      pz.data_origin AS zag_origin,
      a."Id" AS art_id,
      a."PLU" AS plu,
      a."Naziv" AS naziv,
      a."DataOrigin" AS art_origin
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje::date = '2026-03-26'
    ORDER BY ps.id
  `);
  console.log(`=== ALL STAVKE FOR 2026-03-26 (${detail.rows.length} rows) ===`);
  let totalQty = 0, totalRev = 0;
  detail.rows.forEach(r => {
    totalQty += r.kolicina;
    totalRev += parseFloat(r.line_total);
    console.log(`stavka=${r.stavka_id} | racun=${r.broj_racuna} | artikal=${r.id_artikal} | plu=${r.plu||'NULL'} | kolicina=${r.kolicina} | cena=${r.cena} | total=${r.line_total} | art_origin=${r.art_origin||'MISSING'}`);
  });
  console.log(`\nSUM: qty=${totalQty}, revenue=${totalRev}`);

  // 2) Group by zaglavlje (racun) for that date
  const byReceipt = await client.query(`
    SELECT
      pz.id AS zag_id,
      pz.broj_racuna,
      pz.datum_prodaje::text AS raw_date,
      COUNT(ps.id) AS num_stavki,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date = '2026-03-26'
    GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje
    ORDER BY pz.datum_prodaje, pz.id
  `);
  console.log('\n=== BY RECEIPT (RACUN) ===');
  console.log('zag_id      | broj_racuna | raw_date                   | stavki | qty | revenue');
  byReceipt.rows.forEach(r => {
    console.log(`${String(r.zag_id).padEnd(11)} | ${(r.broj_racuna||'NULL').padEnd(11)} | ${r.raw_date.padEnd(26)} | ${String(r.num_stavki).padStart(6)} | ${String(r.total_qty).padStart(3)} | ${r.total_revenue}`);
  });

  // 3) Check if there are multiple imports (duplicate zaglavlje for same receipt)
  const dupeCheck = await client.query(`
    SELECT broj_racuna, COUNT(*) AS cnt, 
      array_agg(id ORDER BY id) AS zag_ids,
      array_agg(datum_prodaje::text ORDER BY id) AS dates
    FROM prodaja_zaglavlje
    WHERE datum_prodaje::date = '2026-03-26'
    GROUP BY broj_racuna
    HAVING COUNT(*) > 1
  `);
  console.log('\n=== DUPLICATE RECEIPTS ON 2026-03-26 ===');
  if (dupeCheck.rows.length === 0) {
    console.log('None found');
  } else {
    dupeCheck.rows.forEach(r => {
      console.log(`${r.broj_racuna}: ${r.cnt}x, ids=[${r.zag_ids}], dates=[${r.dates}]`);
    });
  }

  // 4) Check nearby dates - maybe real date in Access is different  
  const nearby = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      COUNT(DISTINCT pz.id) AS num_racuna,
      COUNT(ps.id) AS num_stavki,
      SUM(ps.kolicina) AS total_qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date BETWEEN '2026-03-24' AND '2026-03-28'
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date
  `);
  console.log('\n=== NEARBY DATES (Mar 24-28) ===');
  console.log('sale_date  | racuna | stavki | qty | revenue');
  nearby.rows.forEach(r => {
    console.log(`${r.sale_date} | ${String(r.num_racuna).padStart(6)} | ${String(r.num_stavki).padStart(6)} | ${String(r.total_qty).padStart(3)} | ${r.total_revenue}`);
  });

  // 5) Check cena values - are they correct?
  const cenaCheck = await client.query(`
    SELECT
      ps.id_artikal,
      a."PLU",
      a."Naziv",
      ps.cena AS stavka_cena,
      a."ProdajnaCena" AS artikal_prodajna_cena,
      ps.kolicina,
      ps.kolicina * ps.cena AS calc_total
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje::date = '2026-03-26'
    ORDER BY ps.kolicina * ps.cena DESC
  `);
  console.log('\n=== CENA CHECK FOR 2026-03-26 ===');
  console.log('artikal | plu        | naziv                     | stavka_cena | art_cena | qty | line_total');
  cenaCheck.rows.forEach(r => {
    console.log(`${String(r.id_artikal).padEnd(7)} | ${(r.PLU||'?').padEnd(10)} | ${(r.Naziv||'?').slice(0,25).padEnd(25)} | ${String(r.stavka_cena).padStart(11)} | ${String(r.artikal_prodajna_cena||'?').padStart(8)} | ${String(r.kolicina).padStart(3)} | ${r.calc_total}`);
  });

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
