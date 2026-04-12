const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) Dry run - count duplicates
  const dryRun = await client.query(`
    SELECT
      COUNT(*) AS rows_to_delete,
      ROUND(SUM(kolicina * cena)::numeric, 2) AS revenue_to_remove
    FROM prodaja_stavke
    WHERE id NOT IN (
      SELECT MIN(id)
      FROM prodaja_stavke
      GROUP BY id_prodaja, id_artikal, kolicina, cena
    )
  `);
  console.log('=== DRY RUN ===');
  console.log(`Rows to delete: ${dryRun.rows[0].rows_to_delete}`);
  console.log(`Revenue to remove: ${dryRun.rows[0].revenue_to_remove}`);

  // 2) Total rows before
  const totalBefore = await client.query('SELECT COUNT(*) AS cnt FROM prodaja_stavke');
  console.log(`\nTotal stavke before: ${totalBefore.rows[0].cnt}`);
  console.log(`Total stavke after: ${parseInt(totalBefore.rows[0].cnt) - parseInt(dryRun.rows[0].rows_to_delete)}`);

  // 3) Check March 26 specifically
  const mar26Before = await client.query(`
    SELECT SUM(kolicina) AS qty, ROUND(SUM(kolicina * cena)::numeric, 2) AS rev
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date = '2026-03-26'
  `);
  console.log(`\nMar 26 BEFORE: qty=${mar26Before.rows[0].qty}, rev=${mar26Before.rows[0].rev}`);

  const mar26After = await client.query(`
    SELECT SUM(kolicina) AS qty, ROUND(SUM(kolicina * cena)::numeric, 2) AS rev
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date = '2026-03-26'
      AND ps.id IN (
        SELECT MIN(id) FROM prodaja_stavke GROUP BY id_prodaja, id_artikal, kolicina, cena
      )
  `);
  console.log(`Mar 26 AFTER:  qty=${mar26After.rows[0].qty}, rev=${mar26After.rows[0].rev}`);

  // 4) Actually delete
  console.log('\n=== EXECUTING DELETE ===');
  const deleteResult = await client.query(`
    DELETE FROM prodaja_stavke
    WHERE id NOT IN (
      SELECT MIN(id)
      FROM prodaja_stavke
      GROUP BY id_prodaja, id_artikal, kolicina, cena
    )
  `);
  console.log(`Deleted: ${deleteResult.rowCount} rows`);

  // 5) Verify after
  const mar26Final = await client.query(`
    SELECT SUM(kolicina) AS qty, ROUND(SUM(kolicina * cena)::numeric, 2) AS rev
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje::date = '2026-03-26'
  `);
  console.log(`\nMar 26 FINAL: qty=${mar26Final.rows[0].qty}, rev=${mar26Final.rows[0].rev}`);

  const totalAfter = await client.query('SELECT COUNT(*) AS cnt FROM prodaja_stavke');
  console.log(`Total stavke after: ${totalAfter.rows[0].cnt}`);

  // 6) Spot check a few more dates
  const spotCheck = await client.query(`
    SELECT
      pz.datum_prodaje::date::text AS sale_date,
      SUM(ps.kolicina) AS qty,
      ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS rev
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    WHERE pz.datum_prodaje >= '2026-03-01' AND pz.datum_prodaje < '2026-04-15'
    GROUP BY pz.datum_prodaje::date
    ORDER BY sale_date DESC
  `);
  console.log('\n=== POST-CLEANUP: 2026 Mar-Apr ===');
  console.log('sale_date  | qty | revenue');
  spotCheck.rows.forEach(r => {
    console.log(`${r.sale_date} | ${String(r.qty).padStart(3)} | ${r.rev}`);
  });

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
