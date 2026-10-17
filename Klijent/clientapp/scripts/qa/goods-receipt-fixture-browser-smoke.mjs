#!/usr/bin/env node
/**
 * Isolated browser fixture proof for the goods-receipt flow.
 * Intercepts every API call: NO network write and NO real Access/PostgreSQL data.
 * Covers desktop + 375px viewport and linked storno after a synthetic confirmation.
 */
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { createReadStream, existsSync } from "node:fs";
import { extname, resolve, join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "puppeteer";

const DIR = dirname(fileURLToPath(import.meta.url));
const DIST = resolve(DIR, "../../dist");
const receipt = {
  id: 81, status: "confirmed", supplierId: -3, supplierName: "Dobavljač",
  storeId: -4, documentType: "invoice", documentNumber: "FA-BROWSER",
  documentDate: "2026-10-16", receivedDate: "2026-10-16",
  currency: "RSD", lineCount: 1, totalQuantity: 2, purchaseTotal: 27,
  createdBy: "stock-clerk-1", createdAtUtc: "2026-10-16T08:00:00Z",
  lines: [{ id: 100, lineNumber: 1, articleId: -7, articleName: "Čizma 42",
    quantity: 2, purchaseUnitPrice: 13.5, purchaseLineTotal: 27, currency: "RSD" }],
};
const json = (body, status = 200) => ({
  status, contentType: "application/json; charset=utf-8",
  headers: { "Access-Control-Allow-Origin": "*", "Access-Control-Allow-Headers": "*",
    "Access-Control-Allow-Methods": "GET,POST,OPTIONS" }, body: JSON.stringify(body),
});
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const server = createServer((req, res) => {
  const path = new URL(req.url ?? "/", "http://localhost").pathname;
  const filePath = !extname(path) ? join(DIST, "index.html") : resolve(DIST, "." + path);
  if (!filePath.startsWith(DIST) || !existsSync(filePath)) return res.writeHead(404).end("Not found");
  const type = path.endsWith(".css") ? "text/css" : path.endsWith(".js") ? "text/javascript"
    : path.endsWith(".svg") ? "image/svg+xml" : "text/html";
  res.writeHead(200, { "content-type": type }); createReadStream(filePath).pipe(res);
});

async function until(page, text) {
  await page.waitForFunction(
    (value) => document.body?.innerText.includes(value), { timeout: 20000 }, text);
}
async function labelledControl(page, labelText) {
  const jsHandle = await page.evaluateHandle((text) => [...document.querySelectorAll("label")]
    .find((label) => (label.textContent || "").includes(text))
    ?.querySelector("input,textarea,select") ?? null, labelText);
  const element = jsHandle.asElement();
  assert.ok(element, "Missing form label " + labelText);
  return element;
}
async function fill(page, labelText, value) {
  const el = await labelledControl(page, labelText);
  if (await el.evaluate((node) => node.tagName === "SELECT")) {
    await el.select(String(value));
  } else {
    await el.click({ clickCount: 3 });
    await el.press("Backspace");
    await el.type(String(value));
  }
  await el.dispose();
}
async function clickButton(page, text) {
  const result = await page.evaluate((name) => {
    const button = [...document.querySelectorAll("button")]
      .find((item) => (item.textContent || "").includes(name) && !item.disabled);
    if (!button) return false;
    button.click(); return true;
  }, text);
  assert.equal(result, true, "Missing enabled button " + text);
}
async function execute(viewport, base) {
  const browser = await puppeteer.launch({ headless: true, args: ["--no-sandbox"] });
  const page = await browser.newPage();
  await page.setViewport(viewport);
  const state = { writes: 0, reversals: 0, requests: [], reversed: false };
  page.on("dialog", async (dialog) => {
    if (dialog.type() === "prompt") await dialog.accept("test-admin-key-only-for-synthetic-browser");
    else if (dialog.type() === "confirm") await dialog.accept();
    else await dialog.dismiss();
  });
  await page.setRequestInterception(true);
  page.on("request", async (request) => {
    try {
      const uri = new URL(request.url());
      const path = uri.pathname;
      if (path.startsWith("/api/")) {
        state.requests.push({ method: request.method(), path });
        if (request.method() === "OPTIONS") return await request.respond(json(null));
        if (path === "/api/dobavljaci") return await request.respond(json([{ id: -3, naziv: "Dobavljač" }]));
        if (path === "/api/analytics/filters/stores" || path === "/api/analytics/cached/filters/stores")
          return await request.respond(json([{ storeId: -4, storeName: "Magacin" }]));
        if (path === "/api/tipovi-obuce") return await request.respond(json([{ id: -11, naziv: "Čizme" }]));
        if (path === "/api/sezone") return await request.respond(json([{ id: -12, naziv: "Jesen" }]));
        if (path === "/api/goods-receipts/articles")
          return await request.respond(json([{ id: -7, name: "Čizma 42", code: "C42", quantity: 4,
            purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access" }]));
        if (path.startsWith("/api/goods-receipts/request/")) return await request.respond(json(null, 404));
        if (path === "/api/goods-receipts" && request.method() === "POST") {
          const headers = request.headers();
          assert.equal(headers["x-receipt-operator-id"], "stock-clerk-1");
          assert.equal(headers["x-receipt-operator-key"], "named-operator-only-test-key-over-32-characters");
          const body = JSON.parse(request.postData() ?? "{}");
          assert.equal(body.lines[0].articleId, -7);
          state.writes++;
          return await request.respond(json(receipt, 201));
        }
        if (path === "/api/goods-receipts" && request.method() === "GET") {
          const headers = request.headers();
          if (!headers["x-admin-key"]) return await request.respond(json({}, 401));
          return await request.respond(json([{ ...receipt, status: state.reversed ? "reversed" : "confirmed" }]));
        }
        if (path === "/api/goods-receipts/81/reverse" && request.method() === "POST") {
          assert.equal(request.headers()["x-receipt-operator-id"], "stock-clerk-1");
          assert.equal(JSON.parse(request.postData() ?? "{}").reason, "Pogrešno evidentirana količina");
          state.reversals++;
          state.reversed = true;
          return await request.respond(json({ id: 81, status: "reversed", replayed: false }));
        }
        if (path === "/api/goods-receipts/81" && request.method() === "GET")
          return await request.respond(json({ ...receipt, status: state.reversed ? "reversed" : "confirmed",
            reversal: state.reversed ? { reversedBy: "stock-clerk-1",
              reversedAtUtc: "2026-10-16T09:00:00Z", reason: "Pogrešno evidentirana količina" } : null }));
        return await request.respond(json([], 200));
      }
      if (uri.origin !== base) return await request.abort();
      await request.continue();
    } catch (err) {
      console.error("Fixture intercept error:", err);
      await request.abort().catch(() => {});
    }
  });
  try {
    await page.goto(base + "/unos-robe", { waitUntil: "networkidle2", timeout: 20000 });
    await until(page, "Podaci o prijemu");
    await fill(page, "Dobavljač", -3);
    await fill(page, "Broj računa ili otpremnice", "FA-BROWSER");
    await fill(page, "Prodavnica ili skladište prijema", -4);
    await clickButton(page, "Nastavi na stavke");
    await until(page, "Stavke prijema");
    const finder = await page.waitForSelector('input[placeholder="Pretražite artikle ili unesite novi naziv"]');
    await finder.type("Čizma");
    await until(page, "Čizma 42");
    await clickButton(page, "Čizma 42");
    await fill(page, "Količina", "2");
    await fill(page, "Nabavna cena po komadu", "13.50");
    await fill(page, "Prodajna cena po komadu", "25");
    await fill(page, "ID ovlašćenog operatera", "stock-clerk-1");
    await fill(page, "Lični ključ operatera", "named-operator-only-test-key-over-32-characters");
    await clickButton(page, "Pregledaj i potvrdi prijem");
    await until(page, "Prijem je potvrđen");
    assert.equal(state.writes, 1, "only one synthetic receipt write");
    await clickButton(page, "Novi prijem");
    await until(page, "Istorija prijema robe");
    await page.evaluate(() => [...document.querySelectorAll("summary")]
      .find((s) => s.textContent?.includes("Istorija prijema robe"))?.click());
    await clickButton(page, "Pretraži");
    await until(page, "FA-BROWSER");
    await clickButton(page, "Otvori");
    await until(page, "Prijem 81");
    await fill(page, "ID operatera", "stock-clerk-1");
    await fill(page, "Lični ključ operatera", "named-operator-only-test-key-over-32-characters");
    await fill(page, "Razlog storna", "Pogrešno evidentirana količina");
    await clickButton(page, "Storniraj potvrđeni prijem");
    await until(page, "Razlog: Pogrešno evidentirana količina");
    assert.equal(state.reversals, 1, "only one synthetic storno");
    return { width: viewport.width, receiptPosts: state.writes, reversals: state.reversals,
      browser: "Puppeteer Chromium", fixture: true, realDatabaseTouched: false };
  } finally {
    await browser.close();
  }
}
async function main() {
  assert.ok(existsSync(resolve(DIST, "index.html")), "Build the frontend before browser smoke");
  await new Promise((resolveStart) => server.listen(0, "127.0.0.1", resolveStart));
  const base = "http://127.0.0.1:" + server.address().port;
  try {
    const cases = [];
    for (const width of [375, 1280]) cases.push(await execute({ width, height: 850 }, base));
    console.log(JSON.stringify({ verdict: "synthetic_receipt_and_storno_browser_pass",
      productionProof: false, cases }, null, 2));
  } finally {
    await new Promise((done) => server.close(done));
  }
}
main().catch((error) => {
  console.error(JSON.stringify({ verdict: "synthetic_receipt_and_storno_browser_fail_closed",
    error: error instanceof Error ? error.message : String(error) }));
  process.exitCode = 1;
});
