const { Client } = require('pg');

(async () => {
  const client = new Client({
    connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require'
  });
  await client.connect();

  // 1) Check PostgreSQL timezone
  const tz = await client.query("SHOW timezone");
  console.log('=== DB TIMEZONE ===');
  console.log(tz.rows[0].TimeZone);

  // 2) Check raw timestamps for latest records
  const raw = await client.query(`
    SELECT id, datum_prodaje, datum_prodaje AT TIME ZONE 'UTC' AS utc_time,
           datum_prodaje AT TIME ZONE 'Europe/Belgrade' AS belgrade_time,
           datum_prodaje::date AS pg_date,
           (datum_prodaje AT TIME ZONE 'Europe/Belgrade')::date AS belgrade_date
    FROM prodaja_zaglavlje
    WHERE datum_prodaje >= '2026-04-01'
    ORDER BY datum_prodaje DESC
    LIMIT 20
  `);
  console.log('\n=== RAW TIMESTAMPS (latest records) ===');
  console.log('id         | datum_prodaje                        | pg_date    | belgrade_date');
  raw.rows.forEach(r => {
    console.log(`${r.id} | ${r.datum_prodaje} | ${r.pg_date?.toISOString().slice(0,10)} | ${r.belgrade_date?.toISOString().slice(0,10)}`);
  });

  // 3) Check column type
  const colType = await client.query(`
    SELECT column_name, data_type, is_nullable
    FROM information_schema.columns
    WHERE table_name = 'prodaja_zaglavlje' AND column_name = 'datum_prodaje'
  `);
  console.log('\n=== datum_prodaje COLUMN TYPE ===');
  console.log(colType.rows[0]);

  // 4) What does the C# service query actually produce?
  // It does: pz.DatumProdaje.Date which translates to DATE(datum_prodaje)
  // And groups by pz.DatumProdaje.Date
  // If the column is 'timestamp without time zone', then .Date = ::date and no TZ shift
  // If the column is 'timestamp with time zone', then ::date depends on session TZ

  await client.end();
})().catch(e => { console.error(e.message); process.exit(1); });
