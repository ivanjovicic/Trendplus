const { Client } = require('pg');
(async () => {
  const c = new Client({ connectionString: 'postgresql://neondb_owner:npg_7hUftT3sXHgR@ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech/trendplus?sslmode=require' });
  await c.connect();
  const r = await c.query("SELECT column_name FROM information_schema.columns WHERE table_name='Artikli' ORDER BY ordinal_position");
  r.rows.forEach(x => console.log(x.column_name));
  await c.end();
})().catch(e => { console.error(e.message); process.exit(1); });
