#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const migrationId = '20261010160000_AddPurchaseCostProvenance';
const query = `
WITH column_info AS (
  SELECT data_type, character_maximum_length, is_nullable
  FROM information_schema.columns
  WHERE table_schema = current_schema()
    AND table_name = 'prodaja_stavke'
    AND column_name = 'purchase_cost_provenance'
)
SELECT json_build_object(
  'target', json_build_object(
    'host', inet_server_addr()::text,
    'port', inet_server_port(),
    'database', current_database(),
    'user', current_user
  ),
  'column', CASE WHEN EXISTS (SELECT 1 FROM column_info) THEN
    (SELECT json_build_object(
      'exists', true,
      'dataType', data_type,
      'characterMaximumLength', character_maximum_length,
      'nullable', is_nullable = 'YES'
    ) FROM column_info)
    ELSE json_build_object('exists', false, 'dataType', null, 'characterMaximumLength', null, 'nullable', null)
  END,
  'migrationHistoryExists', to_regclass('public."__EFMigrationsHistory"') IS NOT NULL
);`;
const migrationQuery = `SELECT EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '${migrationId}');`;

export function parseConnection(raw) {
  if (!raw || !raw.trim()) throw new Error('connection_missing');
  const value = raw.trim();
  const result = {};
  if (/^postgres(?:ql)?:\/\//i.test(value)) {
    const url = new URL(value);
    result.PGHOST = decodeURIComponent(url.hostname);
    if (url.port) result.PGPORT = url.port;
    if (url.pathname.length > 1) result.PGDATABASE = decodeURIComponent(url.pathname.slice(1));
    if (url.username) result.PGUSER = decodeURIComponent(url.username);
    if (url.password) result.PGPASSWORD = decodeURIComponent(url.password);
    const uriOptions = new Set(['sslmode', 'application_name', 'connect_timeout', 'sslrootcert', 'sslcert', 'sslkey', 'gssencmode', 'target_session_attrs', 'options', 'channel_binding']);
    for (const [key, val] of url.searchParams) {
      if (!uriOptions.has(key)) throw new Error('connection_option_unsupported');
      const envKey = ({ application_name: 'PGAPPNAME', target_session_attrs: 'PGTARGETSESSIONATTRS', channel_binding: 'PGCHANNELBINDING' })[key]
        ?? `PG${key.replaceAll('-', '_').toUpperCase()}`;
      result[envKey] = key === 'sslmode' ? normalizeSslMode(val) : val;
    }
  } else {
    for (const item of splitConnectionString(value)) {
      const equals = item.indexOf('=');
      if (equals < 1) throw new Error('connection_invalid');
      const key = item.slice(0, equals).trim().toLowerCase().replace(/[\s_]/g, '');
      const val = unquote(item.slice(equals + 1).trim());
      const pgKey = ({ host: 'PGHOST', server: 'PGHOST', port: 'PGPORT', database: 'PGDATABASE', initialcatalog: 'PGDATABASE', username: 'PGUSER', userid: 'PGUSER', user: 'PGUSER', password: 'PGPASSWORD', sslmode: 'PGSSLMODE' })[key];
      if (pgKey) result[pgKey] = pgKey === 'PGSSLMODE' ? normalizeSslMode(val) : val;
      else if (!['timeout', 'commandtimeout', 'applicationname', 'pooling', 'minimumpoolsize', 'maximumpoolsize', 'trustservercertificate', 'includeerrordetail'].includes(key)) {
        throw new Error('connection_option_unsupported');
      }
    }
  }
  if (!result.PGHOST || !result.PGDATABASE) throw new Error('connection_target_incomplete');
  return result;
}

export function normalizeResult(payload) {
  const col = payload?.column;
  const compatible = col?.exists === true
    && col.dataType === 'character varying'
    && col.characterMaximumLength === 64
    && col.nullable === true;
  const status = compatible ? 'ready' : col?.exists === false ? 'missing' : 'unknown';
  return {
    verdict: status,
    target: payload?.target ?? null,
    column: col ?? null,
    efMigration: payload?.efMigration ?? 'unknown',
    schemaSource: compatible ? payload.efMigration === 'applied' ? 'ef_migration' : 'bootstrap_or_unrecorded' : 'unknown',
    required: { dataType: 'character varying', characterMaximumLength: 64, nullable: true }
  };
}

export function exitCodeFor(report) {
  return report?.verdict === 'ready' ? 0 : 2;
}

export function runPreflight({ rawConnection, execute = executePsql } = {}) {
  let env;
  try {
    env = { ...process.env, ...parseConnection(rawConnection) };
  } catch (error) {
    return { verdict: 'unknown', target: null, column: null, efMigration: 'unknown', schemaSource: 'unknown', errorCode: error.message };
  }
  const result = execute(query, env);
  if (result.status !== 0 || !result.stdout) {
    return { verdict: 'unknown', target: null, column: null, efMigration: 'unknown', schemaSource: 'unknown', errorCode: 'database_probe_failed' };
  }
  try {
    const payload = JSON.parse(result.stdout.trim());
    let efMigration = 'unknown';
    if (payload.migrationHistoryExists) {
      const migration = execute(migrationQuery, env);
      if (migration.status !== 0 || !migration.stdout) {
        return { verdict: 'unknown', target: payload.target ?? null, column: payload.column ?? null, efMigration: 'unknown', schemaSource: 'unknown', errorCode: 'migration_history_probe_failed' };
      }
      efMigration = migration.stdout.trim() === 't' ? 'applied' : 'not_applied';
    }
    return normalizeResult({ ...payload, efMigration });
  } catch {
    return { verdict: 'unknown', target: null, column: null, efMigration: 'unknown', schemaSource: 'unknown', errorCode: 'database_probe_invalid_response' };
  }
}

function executePsql(sql, env) {
  return spawnSync(process.env.PSQL_PATH || 'psql', ['--no-psqlrc', '--quiet', '--tuples-only', '--no-align', '--set=ON_ERROR_STOP=1', '--command', sql], {
    encoding: 'utf8', env, windowsHide: true, timeout: 20_000
  });
}

function normalizeSslMode(mode) {
  const normalized = mode.toLowerCase();
  if (['disable', 'allow', 'prefer', 'require', 'verify-ca', 'verify-full'].includes(normalized)) return normalized;
  if (normalized === 'verifyfull') return 'verify-full';
  if (normalized === 'verifyca') return 'verify-ca';
  throw new Error('connection_sslmode_unsupported');
}

function splitConnectionString(value) {
  const items = [];
  let current = '';
  let quote = null;
  for (let i = 0; i < value.length; i++) {
    const char = value[i];
    if (quote) {
      current += char;
      if (char === quote && value[i + 1] === quote) current += value[++i];
      else if (char === quote) quote = null;
    } else if (char === '"' || char === "'") {
      quote = char;
      current += char;
    } else if (char === ';') {
      if (current.trim()) items.push(current);
      current = '';
    } else current += char;
  }
  if (quote) throw new Error('connection_invalid');
  if (current.trim()) items.push(current);
  return items;
}

function unquote(value) {
  if (value.length >= 2 && ['"', "'"].includes(value[0]) && value.at(-1) === value[0]) {
    const quote = value[0];
    return value.slice(1, -1).replaceAll(quote + quote, quote);
  }
  return value;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const report = runPreflight({ rawConnection: process.env.TRENDPLUS_PREFLIGHT_CONNECTION || process.env.DATABASE_URL });
  process.stdout.write(`${JSON.stringify(report)}\n`);
  process.exitCode = exitCodeFor(report);
}
