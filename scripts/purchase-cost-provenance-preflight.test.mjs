import test from 'node:test';
import assert from 'node:assert/strict';
import { exitCodeFor, normalizeResult, parseConnection, runPreflight } from '../tools/purchase-cost-provenance-preflight.mjs';

test('preflight parses a PostgreSQL connection without putting credentials in the report', () => {
  const env = parseConnection('Host=db.example.test;Port=5433;Database=trend;Username=operator;Password="secret;value";SSL Mode=Require');
  assert.equal(env.PGHOST, 'db.example.test');
  assert.equal(env.PGPORT, '5433');
  assert.equal(env.PGPASSWORD, 'secret;value');
  assert.equal(env.PGSSLMODE, 'require');
  const uri = parseConnection('postgres://operator:secret@db.example.test/trend?sslmode=verify-full&application_name=preflight');
  assert.equal(uri.PGSSLMODE, 'verify-full');
  assert.equal(uri.PGAPPNAME, 'preflight');
});

test('preflight reports a canonical nullable varchar schema as ready, including bootstrap schemas', () => {
  const result = normalizeResult({
    target: { host: '10.0.0.8', port: 5432, database: 'isolated', user: 'tester' },
    column: { exists: true, dataType: 'character varying', characterMaximumLength: 64, nullable: true },
    efMigration: 'not_applied'
  });
  assert.equal(result.verdict, 'ready');
  assert.equal(exitCodeFor(result), 0);
  assert.equal(result.schemaSource, 'bootstrap_or_unrecorded');
  assert.equal(JSON.stringify(result).includes('secret'), false);
});

test('preflight distinguishes a missing column from an incompatible schema and fails closed', () => {
  assert.equal(normalizeResult({ column: { exists: false }, efMigration: 'unknown' }).verdict, 'missing');
  assert.equal(normalizeResult({ column: { exists: true, dataType: 'text', characterMaximumLength: null, nullable: true }, efMigration: 'applied' }).verdict, 'unknown');
  assert.equal(exitCodeFor({ verdict: 'missing' }), 2);
  assert.equal(exitCodeFor({ verdict: 'unknown' }), 2);
});

test('preflight emits an unknown verdict on probe failure without forwarding database errors or secrets', () => {
  const result = runPreflight({
    rawConnection: 'Host=db.example.test;Database=trend;Username=operator;Password=do-not-print',
    execute: () => ({ status: 2, stdout: '', stderr: 'password=do-not-print connection refused' })
  });
  assert.equal(result.verdict, 'unknown');
  assert.equal(JSON.stringify(result).includes('do-not-print'), false);
  assert.equal(result.errorCode, 'database_probe_failed');
});

test('preflight queries migration history only when the history table exists', () => {
  const sql = [];
  const result = runPreflight({
    rawConnection: 'postgres://operator:secret@db.example.test/trend?sslmode=require',
    execute: (query) => {
      sql.push(query);
      return sql.length === 1
        ? { status: 0, stdout: JSON.stringify({ target: { database: 'trend' }, column: { exists: false }, migrationHistoryExists: false }) }
        : { status: 0, stdout: 't' };
    }
  });
  assert.equal(result.verdict, 'missing');
  assert.equal(sql.length, 1);
  assert.equal(JSON.stringify(result).includes('secret'), false);
});
