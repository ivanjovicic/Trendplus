#!/usr/bin/env node
/**
 * RQ664 repository-local browser rehearsal.
 *
 * This command intentionally uses synthetic intercepted responses. It proves that
 * the shipped Supplier/Shoe Type routes render and export one consistent fixture;
 * it is not production, deployed, source-lineage or customer certification.
 */
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { existsSync } from "node:fs";
import { mkdir, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { createReadStream } from "node:fs";
import { extname, join, resolve, dirname, relative, basename } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "puppeteer";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const CLIENT_DIR = resolve(SCRIPT_DIR, "../..");
const DIST_DIR = resolve(CLIENT_DIR, "dist");
const FIXTURE_PATH = resolve(SCRIPT_DIR, "rq448-fixture.json");
const ARTIFACT_PATH = resolve(CLIENT_DIR, "../../docs/qa/RQ664_LOCAL_BROWSER_FIXTURE_VERDICT_2026-10-14.json");
const BASE_PATH = "/analytics";
const ROUTE_QUERY = "fromDate=2026-09-01&toDate=2026-09-01&storeId=2&dataScope=imported";
const SERVER_TIMEOUT_MS = 20_000;

const sleep = (ms) => new Promise((resolvePromise) => setTimeout(resolvePromise, ms));

function jsonResponse(body, status = 200, headers = {}) {
  return {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "access-control-allow-origin": "*", ...headers },
    body: JSON.stringify(body),
  };
}

function csvEscape(value) {
  const text = value == null ? "" : String(value);
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

function buildCsv(payload) {
  const header = payload.columns.map((column) => column.header);
  const rows = payload.rows.map((row) => payload.columns.map((column) => row[column.key] ?? ""));
  return [header, ...rows].map((row) => row.map(csvEscape).join(",")).join("\r\n") + "\r\n";
}

function parseJsonBody(request) {
  try {
    return JSON.parse(request.postData() ?? "{}");
  } catch {
    return null;
  }
}

function makeEmptyResponse(response) {
  const empty = structuredClone(response);
  const collection = "suppliers" in empty ? "suppliers" : "shoeTypes";
  empty[collection] = [];
  empty.meta = {
    ...empty.meta,
    success: true,
    emptyReason: "no_rows_for_filter",
    dataQualityStatus: "insufficient_data",
    isPartial: false,
  };
  empty.dataQuality = {
    ...empty.dataQuality,
    unknownSupplierRevenue: 0,
    unknownTypeRevenue: 0,
    missingCostRevenue: 0,
  };
  empty.totals = {
    ...empty.totals,
    ukupanPromet: 0,
    ukupnaKolicina: 0,
    ukupanMarzniDoprinos: 0,
    brojDobavljaca: 0,
    brojTipovaObuce: 0,
  };
  return empty;
}

function assertExportPayloadMatchesFixture(payload, response, tableKey) {
  assert.equal(payload.tableKey, tableKey, `${tableKey}: exported table identity`);
  const isSupplier = tableKey === "supplier-sales-stats";
  const rows = isSupplier ? response.suppliers : response.shoeTypes;
  const nameKey = isSupplier ? "dobavljacNaziv" : "tipObuceNaziv";
  assert.equal(payload.columns[0]?.key, nameKey, `${tableKey}: exported entity column`);
  assert.equal(payload.rows.length, rows.length, `${tableKey}: exported row count`);

  // Independent source-response expectations, not values copied from the generated CSV.
  // Sort by revenue/quantity so a legitimate table sort does not alter the verdict.
  const signature = (row) => [Number(row.ukupanPromet), Number(row.ukupnaKolicina)];
  const byValues = (a, b) => a[0] - b[0] || a[1] - b[1];
  const expected = rows.map(signature).sort(byValues);
  const actual = payload.rows.map(signature).sort(byValues);
  assert.deepEqual(actual, expected, `${tableKey}: exported revenue/quantity rows mismatch`);
  assert.equal(rows.reduce((sum, row) => sum + row.ukupanPromet, 0), response.totals.ukupanPromet,
    `${tableKey}: fixture rows do not match the declared revenue total`);
  assert.equal(payload.rows.reduce((sum, row) => sum + Number(row.ukupanPromet), 0), response.totals.ukupanPromet,
    `${tableKey}: exported rows do not match the declared revenue total`);
  assert.equal(payload.rows.reduce((sum, row) => sum + Number(row.ukupnaKolicina), 0), response.totals.ukupnaKolicina,
    `${tableKey}: exported rows do not match the declared quantity total`);
  assert.ok(payload.rows.every((row) => typeof row[nameKey] === "string" && row[nameKey].length > 0),
    `${tableKey}: exported entity names must be present`);
}

function assertCsvMatchesFixture(csv, response, tableKey, payload) {
  assertExportPayloadMatchesFixture(payload, response, tableKey);
  // This mocked document endpoint serializes the actual frontend export payload;
  // compare downloaded bytes to that payload, then assert the payload against
  // independently supplied fixture rows above.
  assert.equal(csv, buildCsv(payload), `${tableKey}: downloaded CSV differs from export payload`);
  const lines = csv.trimEnd().split(/\r?\n/);
  assert.equal(lines.length, payload.rows.length + 1, `${tableKey}: downloaded CSV row count`);
  assert.match(csv, /Promet/);
  assert.match(csv, /Negativni/);
  assert.match(csv, /Nepoznato|Nepoznat/);
  return { lineCount: lines.length, header: lines[0], rows: lines.length - 1 };
}

function createFixtureServer() {
  return createServer((request, response) => {
    const requestPath = new URL(request.url ?? "/", "http://127.0.0.1").pathname;
    const filePath = requestPath === "/" || !extname(requestPath)
      ? join(DIST_DIR, "index.html")
      : resolve(DIST_DIR, `.${requestPath}`);
    if (!filePath.startsWith(DIST_DIR) || !existsSync(filePath)) {
      response.writeHead(404).end("Not found");
      return;
    }
    const mime = requestPath.endsWith(".css") ? "text/css" : requestPath.endsWith(".js") ? "text/javascript" : "text/html";
    response.writeHead(200, { "content-type": `${mime}; charset=utf-8` });
    createReadStream(filePath).pipe(response);
  });
}

async function waitForDownload(downloadDir, before) {
  const deadline = Date.now() + SERVER_TIMEOUT_MS;
  while (Date.now() < deadline) {
    const names = await readdir(downloadDir);
    const candidate = names.find((name) => !before.has(name) && !name.endsWith(".crdownload"));
    if (candidate) return resolve(downloadDir, candidate);
    await sleep(100);
  }
  throw new Error(`Download not observed in ${downloadDir}`);
}

function assertTrustBody(bodyText, tableTitle) {
  assert.match(bodyText, new RegExp(tableTitle));
  assert.ok(/1\.\s*9\.\s*2026\.|2026-09-01/.test(bodyText), "rendered period is visible");
  assert.match(bodyText, /Uvezeni/);
  assert.match(bodyText, /Postoje upozorenja|Upozorenje/);
}

async function runRenderedCase({ page, fixture, baseUrl, kind, downloadDir, exportPayloads }) {
  const isSupplier = kind === "supplier";
  const route = isSupplier ? "supplier" : "shoe-type-sales-stats";
  const tableKey = isSupplier ? "supplier-sales-stats" : "shoe-type-sales-stats";
  const response = isSupplier ? fixture.supplier : fixture.shoeType;
  const collectionSelector = isSupplier ? '[data-testid="supplier-sales-stats-data-table"]' : '[data-testid="shoe-type-sales-stats-data-table"]';
  const kpiSelector = isSupplier ? '[data-testid="supplier-kpis-primary"]' : '[data-testid="shoetype-kpis-primary"]';
  await page.goto(`${baseUrl}${BASE_PATH}/${route}?${ROUTE_QUERY}`, { waitUntil: "domcontentloaded" });
  try {
    await page.waitForSelector(collectionSelector, { timeout: SERVER_TIMEOUT_MS });
  } catch (error) {
    const debug = await page.$eval("body", (node) => node.innerText).catch(() => "<body unavailable>");
    throw new Error(`${error.message}; url=${page.url()}; body=${debug.slice(0, 2000)}`);
  }
  const bodyText = await page.$eval("body", (node) => node.innerText);
  assertTrustBody(bodyText, isSupplier ? "Prodaja po dobavljačima" : "Prodaja po tipu obuće");
  assert.match(bodyText, /Negativni/);
  assert.match(bodyText, /Nepoznat/);
  const kpiText = await page.$eval(kpiSelector, (node) => node.innerText);
  assert.match(kpiText, /240\s*RSD/);
  const tableText = await page.$eval(collectionSelector, (node) => node.innerText);
  assert.match(tableText, /150/);
  assert.match(tableText, /90/);

  await page.evaluate(() => {
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.trim().startsWith("Izvoz"));
    if (!button) throw new Error("Izvoz button not found");
    button.click();
  });
  await page.waitForSelector('button[role="menuitem"][aria-label="Izvezi kao CSV"]');
  await page.click('button[role="menuitem"][aria-label="Izvezi kao CSV"]');
  await page.waitForFunction(() => [...document.querySelectorAll("button")].some((button) => button.textContent?.includes("Pokreni export")), { timeout: SERVER_TIMEOUT_MS });
  const before = new Set(await readdir(downloadDir));
  const exportButtonCount = await page.$$eval("button", (buttons) => buttons.filter((button) => button.textContent?.includes("Pokreni export")).length);
  assert.equal(exportButtonCount, 1, `${tableKey}: CSV modal is open`);
  await page.evaluate(() => {
    const button = [...document.querySelectorAll("button")].find((candidate) => candidate.textContent?.includes("Pokreni export"));
    if (!button) throw new Error("Pokreni export button not found");
    button.click();
  });
  const downloadedPath = await waitForDownload(downloadDir, before);
  const csv = await readFile(downloadedPath, "utf8");
  const payload = exportPayloads.at(-1);
  assert.ok(payload, `${tableKey}: missing intercepted export payload`);
  const csvSummary = assertCsvMatchesFixture(csv, response, tableKey, payload);
  return {
    kind,
    route: `/analytics/${route}`,
    fixtureClass: fixture.fixtureClass,
    requested: fixture.period,
    rendered: { totalRevenue: response.totals.ukupanPromet, rowCount: 2, kpiText, containsUnknown: true, containsNegativeIdLabel: true },
    export: { fileName: basename(downloadedPath), ...csvSummary },
    interceptedExportPayload: true,
  };
}

async function runStateCase({ page, fixture, baseUrl, mode, kind }) {
  const route = kind === "supplier" ? "supplier" : "shoe-type-sales-stats";
  const collectionSelector = kind === "supplier" ? '[data-testid="supplier-sales-stats-data-table"]' : '[data-testid="shoe-type-sales-stats-data-table"]';
  await page.goto(`${baseUrl}${BASE_PATH}/${route}?${ROUTE_QUERY}`, { waitUntil: "domcontentloaded" });
  if (mode === "error") {
    await page.waitForSelector('[role="alert"]', { timeout: SERVER_TIMEOUT_MS });
    const text = await page.$eval('[role="alert"]', (node) => node.innerText);
    assert.match(text, /Analitika nije dostupna|Greška|dostupni|dostupna/);
    return { mode, kind, state: "error", renderedError: true };
  }
  await page.waitForSelector('[data-state-code]', { timeout: SERVER_TIMEOUT_MS });
  const empty = await page.$eval('[data-state-code]', (node) => ({ code: node.getAttribute("data-state-code"), text: node.innerText }));
  assert.match(empty.text, /Nema podataka|Nema dovoljno|Prikaz nije dostupan|signal nije dovoljno/);
  const emptyTableText = await page.$eval(collectionSelector, (node) => node.innerText).catch(() => "");
  assert.doesNotMatch(emptyTableText, /Negativni|Nepoznato/, `${kind} empty case must not render fixture rows`);
  return { mode, kind, state: "empty", dataStateCode: empty.code, renderedEmpty: true };
}

async function main() {
  if (!existsSync(DIST_DIR)) throw new Error(`Missing build artifact: ${DIST_DIR}; run npm run build first.`);
  const fixture = JSON.parse(await readFile(FIXTURE_PATH, "utf8"));
  assert.equal(fixture.fixtureClass, "synthetic_fixture_only");
  const artifactDir = dirname(ARTIFACT_PATH);
  await mkdir(artifactDir, { recursive: true });
  const downloadDir = resolve(CLIENT_DIR, "../../TestResults/rq664-downloads");
  await rm(downloadDir, { recursive: true, force: true });
  await mkdir(downloadDir, { recursive: true });

  const server = createFixtureServer();
  await new Promise((resolvePromise) => server.listen(0, "127.0.0.1", resolvePromise));
  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;
  const browser = await puppeteer.launch({ headless: true, args: ["--no-sandbox", "--disable-dev-shm-usage"] });
  const page = await browser.newPage();
  page.setDefaultTimeout(SERVER_TIMEOUT_MS);
  const downloadClient = await page.target().createCDPSession();
  await downloadClient.send("Browser.setDownloadBehavior", { behavior: "allow", downloadPath: downloadDir });
  const state = { mode: "canonical", exportPayloads: [], csvByPath: new Map(), unexpectedApi: [] };
  await page.evaluateOnNewDocument(() => {
    window.prompt = () => "rq664-synthetic-fixture-admin-key";
  });
  await page.setRequestInterception(true);
  page.on("request", async (request) => {
    const parsed = new URL(request.url());
    const path = parsed.pathname;
    if (request.method() === "OPTIONS") {
      await request.respond({
        status: 204,
        headers: {
          "access-control-allow-origin": "*",
          "access-control-allow-methods": "GET,POST,OPTIONS",
          "access-control-allow-headers": "Content-Type,X-Admin-Key",
        },
      });
      return;
    }
    if (path.endsWith("/ready") || path.endsWith("/health") || path.endsWith("/api/ready") || path.endsWith("/api/health")) {
      await request.respond(jsonResponse({ status: "ok", ready: true }));
      return;
    }
    if (path.endsWith("/api/redis/status")) {
      await request.respond(jsonResponse({ enabled: true, available: true }));
      return;
    }
    if (path.endsWith("/api/analytics/refresh-status")) {
      await request.respond(jsonResponse({
        isRunning: false,
        refreshedObjects: [],
        failedObjects: [],
        dataFreshnessStatus: "fresh",
        processMode: "web",
        processType: "web",
        workersEnabled: true,
        cacheMode: "in-memory",
        isDistributed: false,
        generatedAtUtc: fixture.supplier.generatedAt,
        jobs: [],
      }));
      return;
    }
    if (path.endsWith("/api/analytics/cached/filters/stores") || path.endsWith("/api/analytics/filters/stores")) {
      await request.respond(jsonResponse(fixture.stores));
      return;
    }
    if (path.endsWith("/api/analytics/cached/filters/suppliers") || path.endsWith("/api/analytics/filters/suppliers")) {
      await request.respond(jsonResponse([{ supplierId: -7, supplierName: "Negativni dobavljač" }]));
      return;
    }
    if (path.endsWith("/api/analytics/supplier-sales-stats")) {
      if (state.mode === "error") {
        await request.respond(jsonResponse({ title: "Synthetic upstream error", detail: "synthetic fixture failure", errorCode: "RQ664_SYNTHETIC_ERROR" }, 503));
      } else {
        await request.respond(jsonResponse(state.mode === "empty" ? makeEmptyResponse(fixture.supplier) : fixture.supplier));
      }
      return;
    }
    if (path.endsWith("/api/analytics/shoe-type-sales-stats")) {
      if (state.mode === "error") {
        await request.respond(jsonResponse({ title: "Synthetic upstream error", detail: "synthetic fixture failure", errorCode: "RQ664_SYNTHETIC_ERROR" }, 503));
      } else {
        await request.respond(jsonResponse(state.mode === "empty" ? makeEmptyResponse(fixture.shoeType) : fixture.shoeType));
      }
      return;
    }
    if (path.endsWith("/api/analytics/cached/inventory/balance") || path.endsWith("/api/analytics/inventory/balance")) {
      await request.respond(jsonResponse({ totalSku: 0, totalOnHand: 0, lowStockCount: 0, outOfStockCount: 0, estimatedInventoryValue: null, meta: { success: true, dataQualityStatus: "insufficient_data" } }));
      return;
    }
    if (path.endsWith("/api/analytics/cached/inventory/insights") || path.endsWith("/api/analytics/inventory/insights")) {
      await request.respond(jsonResponse({ totalItems: 0, totalEstimatedValue: null, valueCoveragePct: 0, unknownValueRows: 0, aging: [], abc: [], topAgedItems: [], topCapitalLockedItems: [], meta: { success: true, dataQualityStatus: "insufficient_data" } }));
      return;
    }
    if (path.endsWith("/api/documents/generate") && request.method() === "POST") {
      const payload = parseJsonBody(request);
      assert.ok(payload, "export payload is JSON");
      assert.equal(payload.format, "csv");
      assert.equal(payload.includeFiltersAndMetadata, true);
      state.exportPayloads.push(payload);
      const downloadPath = `/synthetic-exports/${payload.tableKey}.csv`;
      state.csvByPath.set(downloadPath, buildCsv(payload));
      await request.respond(jsonResponse({ documentId: `rq664-${payload.tableKey}`, status: "completed", isAsync: false, fileName: `rq664-${payload.tableKey}.csv`, mimeType: "text/csv", createdAtUtc: fixture.supplier.generatedAt, downloadUrl: downloadPath }));
      return;
    }
    if (path.startsWith("/synthetic-exports/") && state.csvByPath.has(path)) {
      await request.respond({ status: 200, headers: { "content-type": "text/csv; charset=utf-8", "content-disposition": "attachment", "access-control-allow-origin": "*" }, body: state.csvByPath.get(path) });
      return;
    }
    if (path.startsWith("/api/")) {
      state.unexpectedApi.push({ method: request.method(), path });
      await request.respond(jsonResponse({ title: "RQ664 unexpected API", detail: `No synthetic fixture for ${path}` }, 404));
      return;
    }
    await request.continue();
  });

  const results = [];
  state.mode = "canonical";
  results.push(await runRenderedCase({ page, fixture, baseUrl, kind: "supplier", downloadDir, exportPayloads: state.exportPayloads }));
  assert.equal(state.exportPayloads.at(-1)?.rows.length, 2);
  state.mode = "canonical";
  results.push(await runRenderedCase({ page, fixture, baseUrl, kind: "shoeType", downloadDir, exportPayloads: state.exportPayloads }));
  assert.equal(state.exportPayloads.at(-1)?.rows.length, 2);

  state.mode = "empty";
  results.push(await runStateCase({ page, fixture, baseUrl, mode: "empty", kind: "shoeType" }));
  state.mode = "error";
  results.push(await runStateCase({ page, fixture, baseUrl, mode: "error", kind: "supplier" }));

  // Corrupt a real captured export payload and require the same reconciliation
  // checker used for downloaded CSV to reject the altered source row.
  const tampered = structuredClone(state.exportPayloads[0]);
  tampered.rows[0].ukupanPromet = Number(tampered.rows[0].ukupanPromet) + 1;
  let tamperedMismatchDetected = false;
  try {
    assertExportPayloadMatchesFixture(tampered, fixture.supplier, "supplier-sales-stats");
  } catch (error) {
    assert.match(error.message, /exported revenue\/quantity rows mismatch/);
    tamperedMismatchDetected = true;
    results.push({ mode: "tampered", state: "mismatch_detected", message: error.message });
  }
  assert.equal(tamperedMismatchDetected, true, "corrupted export row must fail source-response parity");
  assert.equal(state.unexpectedApi.length, 0, `unexpected API calls: ${JSON.stringify(state.unexpectedApi)}`);
  assert.equal(state.exportPayloads.length, 2, "both routes sent one CSV export payload");

  const artifact = {
    taskId: "RQ664",
    verdict: "synthetic_fixture_browser_render_export_pass",
    fixtureClass: fixture.fixtureClass,
    contractVersion: fixture.contractVersion,
    productionCertification: false,
    deployedSourceLineage: false,
    cases: results,
    counts: { browserRoutes: 2, csvDownloads: 2, stateCases: 2, tamperedMismatchesDetected: 1 },
    exportPayloadTables: state.exportPayloads.map((payload) => ({ tableKey: payload.tableKey, rowCount: payload.rows.length, columnCount: payload.columns.length })),
    unexpectedApiCount: state.unexpectedApi.length,
  };
  await writeFile(ARTIFACT_PATH, `${JSON.stringify(artifact, null, 2)}\n`, "utf8");
  await browser.close();
  await new Promise((resolvePromise) => server.close(resolvePromise));
  console.log(JSON.stringify({ ...artifact, artifactPath: relative(CLIENT_DIR, ARTIFACT_PATH) }, null, 2));
}

main().catch((error) => {
  console.error(JSON.stringify({ taskId: "RQ664", verdict: "synthetic_fixture_browser_render_export_fail_closed", error: error instanceof Error ? error.message : String(error) }, null, 2));
  process.exitCode = 1;
});
