#!/usr/bin/env node
/**
 * Isolated SEF XML draft browser proof. All API calls are intercepted and answered
 * with synthetic fixtures; external requests and non-pilot API routes fail closed.
 */
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { createReadStream, existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { extname, resolve, join, dirname, sep } from "node:path";
import { fileURLToPath } from "node:url";
import puppeteer from "puppeteer";

const DIR = dirname(fileURLToPath(import.meta.url));
const CLIENT = resolve(DIR, "../..");
const DIST = resolve(CLIENT, "dist");
const XML_FIXTURE = resolve(DIR, "fixtures/synthetic-sef-invoice.xml");
const ADMIN_KEY = "synthetic-browser-fixture-key-never-valid-in-a-real-environment";
let currentStep = "start";
const invoice = {
  isReadOnlyPreview: true,
  documentKind: "Invoice",
  invoiceNumber: "SYNTHETIC-ONLY-20261016",
  issueDate: "2026-10-16",
  invoiceTypeCode: "380",
  documentCurrencyCode: "RSD",
  supplier: { name: "Sintetički dobavljač za test", companyId: null },
  lines: [
    {
      sourceOrder: 1, sourceLineId: "SYN-1", supplierItemId: "SYN-CODE-42",
      buyerItemId: null, standardItemId: null, name: "Sintetička patika model A broj 42",
      rawUnitCode: "H87", invoicedQuantity: 2, physicallyReceivedQuantity: null,
      priceAmount: 1200, priceCurrencyCode: "RSD", priceBaseQuantity: 1,
      netLineAmount: 2400, netLineCurrencyCode: "RSD", allowances: [],
      priceBasisExplanation: "Sintetička cena po jednoj jedinici.", diagnostics: [],
    },
    {
      sourceOrder: 2, sourceLineId: "SYN-2", supplierItemId: "SYN-CODE-38",
      buyerItemId: null, standardItemId: null, name: "Sintetička patika model B broj 38",
      rawUnitCode: "H87", invoicedQuantity: 1, physicallyReceivedQuantity: null,
      priceAmount: 900, priceCurrencyCode: "RSD", priceBaseQuantity: 1,
      netLineAmount: 900, netLineCurrencyCode: "RSD", allowances: [],
      priceBasisExplanation: "Sintetička cena po jednoj jedinici.", diagnostics: [],
    },
  ],
  totals: {
    lineExtensionAmount: { value: 3300, currencyCode: "RSD" },
    taxExclusiveAmount: { value: 3300, currencyCode: "RSD" },
    taxInclusiveAmount: { value: 3960, currencyCode: "RSD" },
    payableAmount: { value: 3960, currencyCode: "RSD" },
  },
  taxSubtotals: [],
  diagnostics: [],
};

const json = (body, status = 200) => ({
  status,
  contentType: "application/json; charset=utf-8",
  headers: { "Access-Control-Allow-Origin": "*", "Access-Control-Allow-Headers": "*",
    "Access-Control-Allow-Methods": "GET,POST,OPTIONS" },
  body: JSON.stringify(body),
});

function reconciliation(manualSelections) {
  const manualSelection = manualSelections.find((selection) => selection.sourceOrder === 2);
  const makeLine = (source, status, selected = null, candidates = []) => ({
    sourceOrder: source.sourceOrder,
    sourceXmlLineId: source.sourceLineId,
    sourceDocumentNumber: invoice.invoiceNumber,
    sourceSupplierItemId: source.supplierItemId,
    sourceBuyerItemId: source.buyerItemId,
    sourceStandardItemId: source.standardItemId,
    sourceName: source.name,
    rawUnitCode: source.rawUnitCode,
    invoicedQuantity: source.invoicedQuantity,
    physicallyReceivedQuantity: null,
    priceAmount: source.priceAmount,
    priceCurrencyCode: source.priceCurrencyCode,
    netLineAmount: source.netLineAmount,
    netLineCurrencyCode: source.netLineCurrencyCode,
    allowances: [],
    sourcePriceBasisExplanation: source.priceBasisExplanation,
    status,
    confidence: selected ? "OperatorSelected" : status === "ExactIdMatch" ? "ProvenExact" : "ReviewRequired",
    matchReasonCode: selected ? "ExplicitManualMatch" : status,
    matchReason: selected
      ? "Ručni izbor u sintetičkom browser testu; proverite model i veličinu."
      : status === "ExactIdMatch"
        ? "Tekstualna šifra se poklapa; identitet i veličina nisu dokazani."
        : "Više mogućih artikala; potreban je ručni izbor i fizička provera.",
    selectedCatalogArticleId: selected?.catalogArticleId ?? (status === "ExactIdMatch" ? 42 : null),
    selectedCatalogExternalArticleId: selected?.externalArticleId ?? (status === "ExactIdMatch" ? "SYN-CODE-42" : null),
    selectedCatalogName: selected?.name ?? (status === "ExactIdMatch" ? "Sintetička patika model A broj 42" : null),
    candidates,
    priceComparison: { status: "Unknown", reason: "Cena nije poređena u ovom sintetičkom fixture-u.", difference: null },
  });

  return {
    isReadOnlyPreview: true,
    canPostInventory: false,
    sourceDocumentNumber: invoice.invoiceNumber,
    sourceDocumentCurrencyCode: "RSD",
    supplier: invoice.supplier,
    effectiveDataScope: "all",
    accessReceiptVerificationStatus: "NotAvailable",
    accessReceiptVerificationReason: "Ovaj lokalni fixture ne proverava Access prijem.",
    lines: [
      makeLine(invoice.lines[0], "ExactIdMatch"),
      makeLine(invoice.lines[1], manualSelection ? "ExplicitManualMatch" : "Ambiguous", manualSelection ? {
        catalogArticleId: manualSelection.catalogArticleId,
        externalArticleId: "SYN-CODE-38-A",
        name: "Sintetička patika model B broj 38",
      } : null, [
        { catalogArticleId: 38, externalArticleId: "SYN-CODE-38-A", name: "Sintetička patika model B broj 38" },
        { catalogArticleId: 39, externalArticleId: "SYN-CODE-38-B", name: "Sintetička patika model C broj 38" },
      ]),
    ],
    diagnostics: [],
  };
}

const server = createServer((req, res) => {
  const path = new URL(req.url ?? "/", "http://localhost").pathname;
  const filePath = !extname(path) ? join(DIST, "index.html") : resolve(DIST, "." + path);
  if (!filePath.startsWith(DIST) || !existsSync(filePath)) return res.writeHead(404).end("Not found");
  const type = path.endsWith(".css") ? "text/css" : path.endsWith(".js") ? "text/javascript"
    : path.endsWith(".svg") ? "image/svg+xml" : "text/html";
  res.writeHead(200, { "content-type": type });
  createReadStream(filePath).pipe(res);
});

async function waitForText(page, text) {
  try {
    await page.waitForFunction((value) => document.body?.innerText.includes(value), { timeout: 20000 }, text);
  } catch (error) {
    const bodyText = await page.evaluate(() => document.body?.innerText.slice(-3000) ?? "").catch(() => "<unavailable>");
    throw new Error(`Text not found: ${text}; page text tail: ${bodyText}`, { cause: error });
  }
}

async function clickButton(page, text) {
  const clicked = await page.evaluate((name) => {
    const button = [...document.querySelectorAll("button")]
      .find((candidate) => !candidate.disabled && (candidate.innerText || "").includes(name));
    if (!button) return false;
    button.click();
    return true;
  }, text);
  assert.equal(clicked, true, `Missing enabled button: ${text}`);
}

async function main() {
  assert.ok(existsSync(join(DIST, "index.html")), "Build the frontend before the browser smoke test.");
  assert.ok(existsSync(XML_FIXTURE), "Synthetic XML fixture is missing.");
  assert.ok(statSync(XML_FIXTURE).size < 1024 * 1024, "Synthetic fixture exceeds the test size limit.");
  await new Promise((resolveStart) => server.listen(0, "127.0.0.1", resolveStart));
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await puppeteer.launch({ headless: true, args: ["--no-sandbox", "--disable-dev-shm-usage"] });
  const downloadDir = await import("node:fs/promises").then(({ mkdtemp }) => mkdtemp(join(DIR, ".sef-xml-download-")));
  const state = { requests: [], blockedExternalRequests: 0, blockedExternalPaths: [], authPrompts: 0, healthProbesMocked: 0 };
  currentStep = "launch browser";
  const page = await browser.newPage();
  try {
    await page.setViewport({ width: 375, height: 850 });
    const cdp = await page.createCDPSession();
    await cdp.send("Page.setDownloadBehavior", { behavior: "allow", downloadPath: downloadDir });
    page.on("dialog", async (dialog) => {
      if (dialog.type() === "prompt") {
        state.authPrompts++;
        assert.match(dialog.message(), /read-only SEF XML/);
        await dialog.accept(ADMIN_KEY);
      } else {
        await dialog.dismiss();
      }
    });
    page.on("pageerror", (error) => console.error("Synthetic browser page error:", error.message));
    await page.setRequestInterception(true);
    page.on("request", async (request) => {
      try {
        const uri = new URL(request.url());
        const path = uri.pathname;
        if (uri.protocol === "data:" || uri.protocol === "blob:") return request.continue();
        // A production-configured bundle can contain the normal API host. Route all
        // API traffic to the local synthetic fixture before Chromium can send it.
        if (!path.startsWith("/api/")) {
          if (uri.origin === base) return request.continue();
          if (path === "/ready" || path === "/health") {
            state.healthProbesMocked++;
            return request.respond(json({ status: "ready" }));
          }
          state.blockedExternalRequests++;
          state.blockedExternalPaths.push(path);
          return request.abort();
        }

        const method = request.method();
        state.requests.push({ method, path });
        if (method === "OPTIONS") return request.respond(json(null));
        if (path === "/api/dobavljaci" && method === "GET")
          return request.respond(json([{ id: -13, naziv: "Sintetički dobavljač za test" }]));
        if ((path === "/api/analytics/filters/stores" || path === "/api/analytics/cached/filters/stores") && method === "GET")
          return request.respond(json([{ storeId: -14, storeName: "Lokacija fixture" }]));
        if (path === "/api/workers/health" && method === "GET")
          return request.respond(json({ totalWorkers: 0, healthyWorkers: 0, runningWorkers: 0,
            errorWorkers: 0, stoppedWorkers: 0, staleWorkers: 0, hasCriticalIssues: false,
            workers: [], workersEnabled: false, runtimeToggleAllowed: false, environment: "synthetic-fixture" }));
        if (path === "/api/redis/status" && method === "GET")
          return request.respond(json({ enabled: false, available: false }));
        if (path === "/api/pilot/sef-xml/preview" && method === "POST") {
          assert.equal(request.headers()["x-admin-key"], ADMIN_KEY);
          assert.match(request.headers()["content-type"] ?? "", /^multipart\/form-data;/);
          return request.respond(json(invoice));
        }
        if (path === "/api/pilot/sef-xml/reconcile" && method === "POST") {
          assert.equal(request.headers()["x-admin-key"], ADMIN_KEY);
          const body = JSON.parse(request.postData() ?? "{}");
          assert.equal(body.dataScope, "all");
          assert.equal(body.invoice?.isReadOnlyPreview, true);
          return request.respond(json(reconciliation(body.manualSelections ?? [])));
        }
        return request.respond(json({ error: "Unexpected API request in isolated fixture." }, 404));
      } catch (error) {
        console.error("Synthetic browser fixture failed closed:", error instanceof Error ? error.message : String(error));
        return request.abort().catch(() => {});
      }
    });

    currentStep = "open /unos-robe";
    await page.goto(`${base}/unos-robe`, { waitUntil: "networkidle2", timeout: 20000 });
    currentStep = "wait for manual receipt screen";
    await waitForText(page, "Unesite račun ili otpremnicu");
    const xmlTab = await page.$("button[aria-pressed][type=button]");
    assert.ok(xmlTab, "The /unos-robe entry mode controls should be available.");
    currentStep = "open XML pilot mode by keyboard";
    await page.evaluate(() => [...document.querySelectorAll("button")]
      .find((button) => (button.innerText || "").includes("Uvezi iz XML-a (pilot)"))?.focus());
    await page.keyboard.press("Enter");
    await page.waitForSelector("#sef-xml-pilot-file");
    assert.equal(await page.$eval("button[aria-pressed=true]", (button) => button.innerText.includes("Uvezi iz XML-a")), true,
      "Keyboard activation should select the XML pilot mode accessibly.");

    currentStep = "check responsive widths";
    const responsiveWidths = [320, 375, 768, 1024, 1280];
    for (const width of responsiveWidths) {
      await page.setViewport({ width, height: 850 });
      const layout = await page.evaluate(() => ({
        pageWidth: document.documentElement.clientWidth,
        componentWidth: document.querySelector(".sef-xml-pilot")?.clientWidth ?? 0,
        componentScrollWidth: document.querySelector(".sef-xml-pilot")?.scrollWidth ?? 0,
      }));
      assert.ok(layout.componentWidth > 0, `Pilot component is not visible at ${width}px.`);
      assert.ok(layout.componentScrollWidth <= layout.componentWidth + 2,
        `Pilot component overflows horizontally at ${width}px (${layout.componentScrollWidth}/${layout.componentWidth}).`);
    }

    currentStep = "select synthetic XML";
    // Upload the checked-in synthetic fixture; it never leaves this intercepted browser.
    const fileInput = await page.$("#sef-xml-pilot-file");
    await fileInput.uploadFile(XML_FIXTURE);
    await waitForText(page, "Izabrano: synthetic-sef-invoice.xml");
    currentStep = "request synthetic preview";
    await clickButton(page, "Pregledaj XML");
    currentStep = "wait for synthetic match review";
    await waitForText(page, "Ovo je samo tekstualno poklapanje.");
    await waitForText(page, "Više mogućih kandidata");
    assert.equal(await page.$eval("#sef-xml-quantity-1", (input) => input.value), "",
      "Physical receipt quantity must not default from invoice quantity.");
    assert.ok(await page.$eval("#sef-xml-quantity-2", (input) => input.value === ""));
    assert.equal(await page.$eval("button[aria-pressed=true]", (button) => button.innerText.includes("Uvezi iz XML-a")), true);

    currentStep = "apply manual catalogue selection";
    await page.select("#sef-xml-candidate-2", "38");
    await clickButton(page, "Primeni ručni izbor");
    currentStep = "wait for manual match result";
    await waitForText(page, "Ručno odabrano");

    currentStep = "confirm synthetic physical quantity";
    await page.$eval("#sef-xml-quantity-1", (input) => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set;
      setter.call(input, "2");
      input.dispatchEvent(new Event("input", { bubbles: true }));
      input.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await page.evaluate(() => {
      const label = [...document.querySelectorAll("label")]
        .find((item) => item.innerText.includes("Potvrđujem fizički prebrojanu količinu"));
      label?.querySelector("input")?.click();
    });
    await waitForText(page, "Operater je potvrdio: 2");
    currentStep = "download synthetic draft CSV";
    await clickButton(page, "Preuzmi CSV nacrt");

    const deadline = Date.now() + 10000;
    let downloaded = [];
    while (Date.now() < deadline) {
      downloaded = readdirSync(downloadDir).filter((file) => file.endsWith(".csv"));
      if (downloaded.length > 0) break;
      await new Promise((resolveWait) => setTimeout(resolveWait, 100));
    }
    assert.equal(downloaded.length, 1, "The synthetic draft CSV should download exactly once.");
    const csv = readFileSync(join(downloadDir, downloaded[0]), "utf8");
    assert.ok(csv.startsWith("\uFEFF"), "CSV should include its UTF-8 BOM.");
    assert.ok(csv.includes("NEKNJIŽENO — NACRT"), "CSV must clearly label the export as an unposted draft.");
    assert.ok(csv.includes("POTVRĐENO OD OPERATERA"), "CSV must preserve explicit physical-count confirmation.");
    assert.ok(csv.includes("NEPOTVRĐENO — NACRT"), "Unconfirmed quantities must remain visibly unconfirmed.");

    const requestSet = new Set(state.requests.map(({ method, path }) => `${method} ${path}`));
    assert.ok(requestSet.has("POST /api/pilot/sef-xml/preview"));
    assert.ok(requestSet.has("POST /api/pilot/sef-xml/reconcile"));
    assert.ok(requestSet.has("GET /api/dobavljaci"));
    assert.ok(requestSet.has("GET /api/analytics/filters/stores"));
    assert.ok(requestSet.has("GET /api/workers/health"));
    assert.ok(requestSet.has("GET /api/redis/status"));
    const writeRequestsOutsideReadOnlyPilot = state.requests.filter(({ method, path }) =>
      ["POST", "PUT", "PATCH", "DELETE"].includes(method)
      && path !== "/api/pilot/sef-xml/preview"
      && path !== "/api/pilot/sef-xml/reconcile");
    const allowedApiRequests = [
      "GET /api/dobavljaci",
      "GET /api/analytics/filters/stores",
      "GET /api/analytics/cached/filters/stores",
      "GET /api/workers/health",
      "GET /api/redis/status",
      "OPTIONS /api/pilot/sef-xml/preview",
      "OPTIONS /api/pilot/sef-xml/reconcile",
      "POST /api/pilot/sef-xml/preview",
      "POST /api/pilot/sef-xml/reconcile",
    ];
    assert.ok([...requestSet].every((request) => allowedApiRequests.includes(request)),
      `Unexpected API request: ${[...requestSet].find((request) => !allowedApiRequests.includes(request))}`);
    assert.equal(writeRequestsOutsideReadOnlyPilot.length, 0,
      `The pilot attempted a non-SEF API write: ${writeRequestsOutsideReadOnlyPilot.map(({ method, path }) => `${method} ${path}`).join(", ")}`);
    assert.equal(state.blockedExternalRequests, 0,
      `Non-fixture external requests were blocked: ${state.blockedExternalPaths.join(", ")}`);
    assert.equal(state.authPrompts, 2,
      "Preview and subsequent manual reconciliation each request transient authorization.");
    assert.ok(!await page.evaluate(() => localStorage.getItem("sefXmlPilotAdminKey")));
    assert.ok(!await page.evaluate(() => sessionStorage.getItem("sefXmlPilotAdminKey")));

    console.log(JSON.stringify({
      verdict: "synthetic_sef_xml_draft_browser_pass",
      testedUrl: `${base}/unos-robe`,
      browser: "Puppeteer Chromium",
      fixture: "synthetic-sef-invoice.xml; locally intercepted API only",
      responsiveWidths,
      apiRequests: [...requestSet],
      nonFixtureExternalRequestsBlocked: state.blockedExternalRequests,
      blockedExternalPaths: state.blockedExternalPaths,
      healthProbesMocked: state.healthProbesMocked,
      inventoryOrJournalWriteRequests: writeRequestsOutsideReadOnlyPilot.length,
      draftCsvDownloaded: true,
      productionProof: false,
      realInvoiceOrOperatorAcceptance: false,
    }, null, 2));
  } finally {
    await browser.close();
    await new Promise((done) => server.close(done));
    const resolvedDownloadDir = resolve(downloadDir);
    assert.ok(resolvedDownloadDir.startsWith(resolve(DIR) + sep), "Refusing to clean a download folder outside the QA script directory.");
    const { rm } = await import("node:fs/promises");
    await rm(resolvedDownloadDir, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error(JSON.stringify({ verdict: "synthetic_sef_xml_draft_browser_fail_closed",
    step: currentStep, error: error instanceof Error ? error.message : String(error),
    stack: error instanceof Error ? error.stack : undefined }));
  process.exitCode = 1;
});
