const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // Detail for March 26 after cleanup
  const detail = await client.query(`
    SELECT
      ps.id,
      pz.broj_racuna,
      ps.id_artikal,
      a."PLU",
      a."Naziv",
      ps.kolicina,
      ps.cena AS stavka_cena,
      a."ProdajnaCena" AS art_prodajna,
      a."NabavnaCena" AS art_nabavna,
      ps.kolicina * ps.cena AS line_total
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje::date = '2026-03-26'
    ORDER BY pz.broj_racuna, ps.id
  `);
  console.log('=== MAR 26 DETAIL (after cleanup) ===');
  console.log('racun | plu  | naziv                | qty | stavka_cena | art_cena | line_total');
  let total = 0;
  detail.rows.forEach(r => {
    total += parseFloat(r.line_total);
    console.log(`${(r.broj_racuna||'?').padEnd(5)} | ${(r.PLU||'?').padEnd(4)} | ${(r.Naziv||'?').slice(0,20).padEnd(20)} | ${String(r.kolicina).padStart(3)} | ${String(r.stavka_cena).padStart(11)} | ${String(r.art_prodajna||'?').padStart(8)} | ${r.line_total}`);
  });
  console.log(`\nTotal items: ${detail.rows.length}`);
  console.log(`Total revenue (stavka_cena): ${total}`);

  // Check: what if we sum using Artikli.ProdajnaCena instead of stavka.cena?
  const altCalc = await client.query(`
    SELECT
      SUM(ps.kolicina) AS qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS rev_stavka_cena,
      ROUND(SUM(ps.kolicina * a."ProdajnaCena")::numeric, 2) AS rev_art_cena,
      ROUND(SUM(ps.kolicina * a."NabavnaCena")::numeric, 2) AS rev_nabavna
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje::date = '2026-03-26'
  `);
  console.log('\n=== ALTERNATIVE CALCULATIONS ===');
  console.log(`Using stavka.cena:    ${altCalc.rows[0].rev_stavka_cena}`);
  console.log(`Using Artikli.ProdajnaCena: ${altCalc.rows[0].rev_art_cena}`);
  console.log(`Using Artikli.NabavnaCena:  ${altCalc.rows[0].rev_nabavna}`);

  // Also check if Apr 6 still has orphan stavke
  const apr6 = await client.query(`
    SELECT
      COUNT(*) FILTER (WHERE a."Id" IS NOT NULL) AS with_art,
      COUNT(*) FILTER (WHERE a."Id" IS NULL) AS without_art,
      SUM(ps.kolicina) FILTER (WHERE a."Id" IS NOT NULL) AS qty_with,
      SUM(ps.kolicina) FILTER (WHERE a."Id" IS NULL) AS qty_without
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    LEFT JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE pz.datum_prodaje::date = '2026-04-06'
  `);
  console.log('\n=== APR 6 ORPHAN CHECK ===');
  console.log(`With Artikli: ${apr6.rows[0].with_art} rows (qty=${apr6.rows[0].qty_with})`);
  console.log(`Without Artikli: ${apr6.rows[0].without_art} rows (qty=${apr6.rows[0].qty_without})`);

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
