import assert from "node:assert/strict";
import puppeteer from "puppeteer";

const baseUrl = (process.argv[2] ?? "http://127.0.0.1:4174").replace(/\/$/, "");
const timeoutMs = Number(process.argv[3] ?? 30000);
const fixtureEnd = new Date("2026-10-02T00:00:00Z");
const dateRows = Array.from({ length: 14 }, (_, index) => {
  const date = new Date(fixtureEnd);
  date.setUTCDate(fixtureEnd.getUTCDate() - (13 - index));
  return {
    date: date.toISOString().slice(0, 10),
    firstShiftTotalItems: 34 + index * 2,
    secondShiftTotalItems: 28 + index,
    totalRevenue: 88000 + index * 3200,
    topSupplierCounts: [18 + index, 12 + index],
    othersCount: 16,
    totalItemsSold: 62 + index * 3,
  };
});
const suppliers = [
  { supplierId: 11, supplierName: "Fixture dobavljač A", isUnknown: false, totalQty: 390, totalRevenue: 620000 },
  { supplierId: 12, supplierName: "Fixture dobavljač B", isUnknown: false, totalQty: 300, totalRevenue: 480000 },
];

function fixtureFor(url) {
  if (url.pathname === "/api/analytics/refresh-status") {
    return { isRunning: false, refreshedObjects: [], failedObjects: [], dataFreshnessStatus: "fresh", jobs: [] };
  }
  if (url.pathname === "/api/analytics/cached/filters/stores" || url.pathname === "/api/analytics/filters/stores") {
    return [{ storeId: 1, storeName: "Fixture prodavnica", city: "Beograd", region: "Test" }];
  }
  if (url.pathname === "/api/analytics/daily-sales") {
    const fromDate = url.searchParams.get("fromDate") ?? "2026-09-03";
    const toDate = url.searchParams.get("toDate") ?? "2026-10-02";
    return {
      requestedFrom: fromDate,
      requestedTo: toDate,
      storeId: null,
      topN: Number(url.searchParams.get("topN") ?? 5),
      dataScope: "all",
      topSuppliers: suppliers,
      topSuppliersOrder: suppliers.map(({ supplierName }) => supplierName),
      dateRows,
      metadata: {
        totalDays: 30,
        uniqueSuppliersInRange: suppliers.length,
        unknownSupplierPct: 0,
        unknownSupplierItems: 0,
        shiftAssignmentStatus: "measured",
        shiftTimeZone: "Europe/Belgrade",
        shiftTimestampBasis: "fixture",
        shiftTimestampBasisKnownRows: dateRows.length,
        shiftTimestampBasisUnknownRows: 0,
        shiftTimestampBasisUnknownRevenue: 0,
        offShiftItems: 4,
        offShiftRevenue: 9200,
        noTimeFallbackItems: 0,
        noTimeFallbackRevenue: 0,
        totalItemsInRange: 1100,
        duplicateReceiptGroupCount: 0,
        duplicateReceiptHeaderCount: 0,
        receiptAmountMismatchCount: 0,
        receiptAmountMismatchRevenue: 0,
        nonStandardReceiptCount: 0,
        nonStandardReceiptRevenue: 0,
        debtReceiptCount: 0,
        debtReceiptRevenue: 0,
        minAvailableDate: fromDate,
        maxAvailableDate: toDate,
        warnings: ["Sintetički browser fixture; nisu stvarni podaci."],
      },
      meta: { success: true, dataQualityStatus: "warning", warnings: ["synthetic_fixture"] },
    };
  }
  return { errorCode: "perf18_fixture_unexpected_api", message: `No fixture for ${url.pathname}` };
}

const browser = await puppeteer.launch({ headless: true, args: ["--no-sandbox", "--disable-setuid-sandbox"] });
try {
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 1000 });
  const pageErrors = [];
  const rechartsRequests = [];
  const apiRequests = [];
  const apiFailures = [];
  const apiRouteErrors = [];
  const apiResponses = [];
  page.on("pageerror", (error) => pageErrors.push(error.message));
  page.on("response", (response) => {
    if (new URL(response.url()).pathname.startsWith("/api/")) apiResponses.push({ url: response.url(), status: response.status() });
  });
  page.on("requestfailed", (request) => {
    if (request.url().includes("/api/")) apiFailures.push({ url: request.url(), error: request.failure()?.errorText });
  });
  page.on("request", (request) => {
    if (/recharts/i.test(request.url())) rechartsRequests.push({ url: request.url(), route: new URL(page.url()).pathname });
  });
  await page.setRequestInterception(true);
  page.on("request", async (request) => {
    const url = new URL(request.url());
    if (url.pathname.includes("/api/")) apiRequests.push(request.url());
    if (!url.pathname.startsWith("/api/")) return request.continue();
    const body = fixtureFor(url);
    const status = request.method() === "OPTIONS" ? 204 : url.pathname === "/api/analytics/refresh-status"
      || url.pathname.endsWith("/filters/stores")
      || url.pathname === "/api/analytics/daily-sales" ? 200 : 503;
    try {
      await request.respond({
        status,
        contentType: "application/json; charset=utf-8",
        headers: {
          "access-control-allow-origin": "*",
          "access-control-allow-methods": "GET, POST, PUT, PATCH, DELETE, OPTIONS",
          "access-control-allow-headers": "*",
        },
        body: request.method() === "OPTIONS" ? "" : JSON.stringify(body),
      });
    } catch (error) {
      apiRouteErrors.push({ url: request.url(), error: String(error) });
      if (!request.isInterceptResolutionHandled()) await request.continue();
    }
  });

  await page.goto(`${baseUrl}/prodaja`, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  await page.waitForSelector("body", { timeout: timeoutMs });
  await new Promise((resolve) => setTimeout(resolve, 500));
  assert.equal(new URL(page.url()).pathname, "/prodaja", "starting route should remain /prodaja");
  assert.equal(rechartsRequests.length, 0, "the /prodaja route must not request a Recharts asset");

  await page.$$eval("nav button", (buttons) => {
    const button = buttons.find((candidate) => candidate.innerText.includes("Operacije"));
    if (!button) throw new Error("Operacije navigation group is missing");
    if (button.getAttribute("aria-expanded") !== "true") button.click();
  });
  await page.waitForSelector('a[href="/analytics/daily-sales"]', { visible: true, timeout: timeoutMs });
  await page.click('a[href="/analytics/daily-sales"]');
  await page.waitForFunction(() => location.pathname === "/analytics/daily-sales", { timeout: timeoutMs });
  try {
    await page.waitForSelector(".daily-sales-chart-wrap svg.recharts-surface", { timeout: timeoutMs });
  } catch (error) {
    const diagnostics = await page.evaluate(() => ({
      pageText: document.querySelector("main")?.innerText.slice(0, 1200),
      chartWraps: [...document.querySelectorAll(".daily-sales-chart-wrap")].map((node) => node.innerText),
    }));
    throw new Error(`${error.message}\nDiagnostics: ${JSON.stringify({ diagnostics, apiRequests, apiResponses, apiFailures, apiRouteErrors, pageErrors, rechartsRequests })}`);
  }

  assert.ok(rechartsRequests.some(({ route }) => route === "/analytics/daily-sales"), "Recharts asset should load after SPA navigation");
  assert.deepEqual(pageErrors, [], `browser page errors: ${pageErrors.join(" | ")}`);
  const chartCount = await page.$$eval(".daily-sales-chart-wrap svg.recharts-surface", (charts) => charts.length);
  assert.ok(chartCount > 0, "expected at least one rendered Recharts SVG surface");
  process.stdout.write(`${JSON.stringify({ status: "PASS", startRoute: "/prodaja", destinationRoute: "/analytics/daily-sales", rechartsRequests, renderedChartSurfaces: chartCount, pageErrors })}\n`);
} finally {
  await browser.close();
}
