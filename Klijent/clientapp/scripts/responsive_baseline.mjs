import fs from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import puppeteer from "puppeteer";

export const VIEWPORTS = [320, 360, 375, 390, 768, 1024, 1280, 1800, 2048, 2400];
export const PUI39_LONG_STORE_OPTION = "Sintetička prodavnica sa izuzetno dugim nazivom za proveru overflow-safe kontrola u responsive rasporedu";

const ROUTES = [
  { id: "app_shell", path: "/analytics", pui40Shell: true, pui45Spinner: true },
  { id: "home", path: "/", readySelector: '[data-testid="home-seasonal-carousel"]', pui45Carousel: true },
  { id: "prodaja", path: "/prodaja", readySelector: ".mobile-entry-form" },
  { id: "unos_robe", path: "/unos-robe", readySelector: ".mobile-entry-form" },
  { id: "nivelacija_cena", path: "/nivelacija", readySelector: ".form-page" },
  { id: "analytics", path: "/analytics", readySelector: '[data-testid="analytics-control-bar"]', expandSelector: ".details-expand", afterExpandSelector: ".analytics-chart-grid .chart-wrap", captureSelector: ".analytics-chart-grid", pui43Selector: ".analytics-executive-kpis", pui43DesktopFold: true },
  { id: "daily_sales", path: "/analytics/daily-sales", readySelector: ".daily-sales-chart-wrap", captureSelector: ".daily-sales-section-grid--double", pui43Selector: ".daily-sales-kpis", pui43MobileKpi: true },
  { id: "supplier", path: "/analytics/supplier", readySelector: ".supplier-consolidated-filters", captureSelector: ".supplier-consolidated-filters", pui43Selector: ".supplier-decision-kpis.analytics-kpi-tier--primary", pui43DesktopFold: true },
  { id: "inventory", path: "/analytics/inventory", readySelector: '[data-testid="analytics-control-bar"]' },
  { id: "pilot_intake", path: "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all", readySelector: ".pilot-intake-durable-table-wrap", printSmoke: true },
  { id: "color_sales", path: "/analytics/color-sales-stats", readySelector: '[data-testid="analytics-data-table"]', captureSelector: '[data-testid="analytics-data-table"]', pui39Overflow: true },
  { id: "shoe_type", path: "/analytics/shoe-type-sales-stats", readySelector: '[data-testid="analytics-control-bar"]', pui39Overflow: true },
  { id: "pre_nivelacija", path: "/analytics/pre-nivelacija-prioriteti", readySelector: '[data-testid="analytics-control-bar"]', pui39Overflow: true, pui43Selector: ".pnp-decision-kpis", pui43TabletGrid: true, pui43DesktopFold: true },
  { id: "products", path: "/analytics/products", readySelector: ".product-decision-table", captureSelector: ".product-decision-table-wrap", pui43Selector: ".product-decision-kpis", pui43MobileKpi: true, pui43DesktopFold: true },
  { id: "actions", path: "/analytics/actions", readySelector: ".aaq-filters" },
  { id: "articles", path: "/artikli/lista", readySelector: '[data-testid="article-list-table"]' },
  { id: "nivelacija_pre_post", path: "/analytics/nivelacije-pre-post", readySelector: '[data-testid="analytics-control-bar"]', pui39Overflow: true },
];

const THEMES = ["light", "soft-gray", "dark"];
const DEFAULT_BASE_URL = "http://127.0.0.1:5174";
const DEFAULT_OUTPUT_DIR = path.resolve("tmp/ui-visual/responsive-baseline");
const DEFAULT_TIMEOUT_MS = 20_000;

function themeNameForBaseline(theme) {
  return theme === "dark" ? "neon-dark" : theme;
}

function parseArgs(argv) {
  const options = {
    baseUrl: DEFAULT_BASE_URL,
    outputDir: DEFAULT_OUTPUT_DIR,
    mode: "fixture",
    timeoutMs: DEFAULT_TIMEOUT_MS,
    routeId: null,
    routeIds: [],
    viewportOnly: false,
    theme: null,
    strict: false,
    selfTest: false,
    productRowCount: 1200,
    viewportWidth: null,
    touchProfile: false,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--base-url") options.baseUrl = argv[++index];
    else if (argument === "--output-dir") options.outputDir = path.resolve(argv[++index]);
    else if (argument === "--mode") options.mode = argv[++index];
    else if (argument === "--timeout-ms") options.timeoutMs = Number(argv[++index]);
    else if (argument === "--route-id") options.routeId = argv[++index];
    else if (argument === "--route-ids") options.routeIds = argv[++index].split(",").filter(Boolean);
    else if (argument === "--viewport-only") options.viewportOnly = true;
    else if (argument === "--theme") options.theme = argv[++index];
    else if (argument === "--strict") options.strict = true;
    else if (argument === "--self-test") options.selfTest = true;
    else if (argument === "--product-row-count") options.productRowCount = Number(argv[++index]);
    else if (argument === "--viewport-width") options.viewportWidth = Number(argv[++index]);
    else if (argument === "--touch-profile") options.touchProfile = true;
    else throw new Error(`Unknown argument: ${argument}`);
  }

  if (!["fixture", "connected-local"].includes(options.mode)) {
    throw new Error(`Unsupported mode: ${options.mode}`);
  }
  if (options.routeId && !ROUTES.some((route) => route.id === options.routeId)) {
    throw new Error(`Unknown responsive baseline route id: ${options.routeId}`);
  }
  const unknownRouteIds = options.routeIds.filter((id) => !ROUTES.some((route) => route.id === id));
  if (unknownRouteIds.length > 0) {
    throw new Error(`Unknown responsive baseline route ids: ${unknownRouteIds.join(", ")}`);
  }
  if (options.theme && !THEMES.includes(options.theme)) {
    throw new Error(`Unknown responsive baseline theme: ${options.theme}`);
  }

  if (!Number.isInteger(options.productRowCount) || options.productRowCount < 1 || options.productRowCount > 1200) {
    throw new Error("Product Decision fixture row count must be an integer between 1 and 1200");
  }
  if (options.viewportWidth != null && !VIEWPORTS.includes(options.viewportWidth)) {
    throw new Error(`Unsupported viewport width: ${options.viewportWidth}`);
  }
  return options;
}

export function assertNoRootOverflow(metrics) {
  const overflowing = metrics.scrollWidth > metrics.viewportWidth + 1
    || metrics.bodyScrollWidth > metrics.viewportWidth + 1
    || (metrics.windowInnerWidth != null && metrics.windowInnerWidth !== metrics.viewportWidth);
  if (overflowing) {
    throw new Error(
      `root horizontal overflow: viewport=${metrics.viewportWidth}, innerWidth=${metrics.windowInnerWidth ?? "n/a"}, document=${metrics.scrollWidth}, body=${metrics.bodyScrollWidth}`,
    );
  }
  return true;
}

export function assertPui40Shell(metrics) {
  if ([1024, 1280, 2400].includes(metrics.viewportWidth) && (!metrics.header || metrics.header.height > 88)) {
    throw new Error(`P-UI-40 header height exceeds 88px at ${metrics.viewportWidth}px: ${metrics.header?.height ?? "missing"}`);
  }
  if (metrics.viewportWidth === 1024 && (!metrics.main || metrics.main.width < 900)) {
    throw new Error(`P-UI-40 main content is narrower than 900px at 1024px: ${metrics.main?.width ?? "missing"}`);
  }
  return true;
}

export function assertPui42TouchTargets(metrics) {
  const smallControls = (metrics.touchAudit?.controls ?? []).filter((control) =>
    control.width < 44 || control.height < 44
      || (control.kind === "text-entry" && control.fontSizePx < 16));
  if (smallControls.length > 0) {
    throw new Error(`P-UI-42 shared touch controls below 44px/16px: ${smallControls.slice(0, 8).map((control) => `${control.selector} ${control.width}x${control.height} ${control.fontSizePx}px`).join("; ")}`);
  }
  return true;
}

export function evaluateGeometry(documentMetrics, viewportWidth) {
  return {
    viewportWidth,
    windowInnerWidth: documentMetrics.windowInnerWidth,
    viewportHeight: documentMetrics.viewportHeight,
    scrollWidth: documentMetrics.scrollWidth,
    bodyScrollWidth: documentMetrics.bodyScrollWidth,
    rootOverflow: documentMetrics.scrollWidth > viewportWidth + 1
      || documentMetrics.bodyScrollWidth > viewportWidth + 1
      || documentMetrics.windowInnerWidth !== viewportWidth,
    header: documentMetrics.header,
    main: documentMetrics.main,
    visibleControlCount: documentMetrics.controls.length,
    minVisibleControlFontPx: documentMetrics.controls.length > 0
      ? Math.min(...documentMetrics.controls.map((control) => control.fontSizePx))
      : null,
    controls: documentMetrics.controls,
    touchAudit: { controls: documentMetrics.touchAudit ?? [] },
    pageLocalTouchOffenders: documentMetrics.pageLocalTouchOffenders ?? [],
    relevantRegions: documentMetrics.relevantRegions,
    overflowingElements: documentMetrics.overflowingElements,
    chartRegions: documentMetrics.chartRegions,
    responsiveGrids: documentMetrics.responsiveGrids,
  };
}

function runSelfTest() {
  if (!THEMES.includes("soft-gray") || themeNameForBaseline("dark") !== "neon-dark") {
    throw new Error("responsive baseline theme selection did not retain the light, soft-gray and dark mappings");
  }

  const intentionalOverflow = {
    viewportWidth: 320,
    windowInnerWidth: 320,
    viewportHeight: 900,
    scrollWidth: 321,
    bodyScrollWidth: 480,
    header: null,
    main: null,
    controls: [],
    relevantRegions: [],
    overflowingElements: [],
    chartRegions: [],
    responsiveGrids: [],
  };

  let failedAsExpected = false;
  try {
    assertNoRootOverflow(intentionalOverflow);
  } catch (error) {
    failedAsExpected = /root horizontal overflow/.test(String(error));
  }

  if (!failedAsExpected) {
    throw new Error("intentional overflow fixture did not fail the geometry assertion");
  }

  let shellFailedAsExpected = false;
  try {
    assertPui40Shell({ viewportWidth: 1024, header: { height: 89 }, main: { width: 899 } });
  } catch (error) {
    shellFailedAsExpected = /P-UI-40 header height exceeds/.test(String(error));
  }
  if (!shellFailedAsExpected) {
    throw new Error("intentional P-UI-40 shell fixture did not fail the geometry assertion");
  }

  assertPui42TouchTargets({ touchAudit: { controls: [{ selector: "button", kind: "target", width: 44, height: 44, fontSizePx: 13 }] } });
  let touchTargetFailedAsExpected = false;
  try {
    assertPui42TouchTargets({ touchAudit: { controls: [{ selector: "input", kind: "text-entry", width: 43, height: 43, fontSizePx: 15 }] } });
  } catch (error) {
    touchTargetFailedAsExpected = /P-UI-42 shared touch controls below/.test(String(error));
  }
  if (!touchTargetFailedAsExpected) {
    throw new Error("intentional P-UI-42 touch target fixture did not fail as expected");
  }

  return {
    name: "intentional-overflow-fixture",
    status: "PASS",
    detail: "overflow and P-UI-40/P-UI-42 regression fixtures failed as expected and were caught by the self-test",
  };
}

async function fixtureResponse(request, options) {
  const url = new URL(request.url());
  if (url.pathname === "/health" || url.pathname === "/ready") {
    return { status: 200, body: JSON.stringify({ status: "fixture" }) };
  }

  if (url.pathname === "/api/analytics/refresh-status") {
    return { status: 200, body: JSON.stringify({
      isRunning: false,
      refreshedObjects: [],
      failedObjects: [],
      dataFreshnessStatus: "fresh",
      processMode: "worker",
      processType: "worker",
      workersEnabled: true,
      generatedAtUtc: "2026-10-02T00:00:00Z",
      jobs: [],
    }) };
  }

  if (url.pathname === "/api/trends/seasonal-images") {
    return { status: 200, body: JSON.stringify([{
      id: 1,
      imageUrl: "https://example.com/seasonal-fixture.jpg",
      source: "unsplash",
      photographerName: "Fixture photographer",
    }]) };
  }

  if (url.pathname === "/api/analytics/cached/filters/stores") {
    return { status: 200, body: JSON.stringify([{ storeId: 1, storeName: PUI39_LONG_STORE_OPTION, city: "Beograd", region: "Centar" }]) };
  }

  if (url.pathname === "/api/analytics/cached/filters/suppliers") {
    return { status: 200, body: JSON.stringify([{ supplierId: 21, supplierName: "Sintetički dobavljač za responsive proveru" }]) };
  }

  if (url.pathname === "/api/analytics/reports/pilot-intake") {
    const fromDate = url.searchParams.get("fromDate") ?? "2026-06-01";
    const toDate = url.searchParams.get("toDate") ?? "2026-06-30";
    return { status: 200, body: JSON.stringify({
      reportId: "responsive-pilot-intake-fixture",
      stableQueryUrl: `/analytics/reports/pilot-intake?fromDate=${fromDate}&toDate=${toDate}&dataScope=all`,
      reportTitle: "Sintetički pilot izveštaj",
      reportType: "pilot-intake",
      generatedAtUtc: "2026-10-02T00:00:00Z",
      periodFrom: fromDate,
      periodTo: toDate,
      period: { fromUtc: fromDate, toUtc: toDate, label: "Sintetički period", scope: "all" },
      lastRefreshAtUtc: "2026-10-02T00:00:00Z",
      dataFreshnessStatus: "fresh",
      dataQualityStatus: "good",
      recommendationAllowed: true,
      usedFallback: false,
      warnings: [],
      kpis: [{ key: "intakeRows", label: "Učitani redovi", value: 24, unit: "redova", tone: "positive" }],
      recommendedActions: [],
      methodology: { summary: "Sintetički dokaz za responsive merenje.", notes: [] },
      rows: [{ section: "Učitano", item: "Sintetički dobavljač", value: "24" }],
      sections: [{
        key: "responsive-width",
        title: "Sintetička tabela prijema",
        description: "Podaci su sintetički i služe samo proveri prikaza.",
        rowCount: 1,
        columns: [
          { key: "supplier", label: "Dobavljač" },
          { key: "articles", label: "Artikli", dataType: "number" },
          { key: "receipts", label: "Prijemi", dataType: "number" },
          { key: "revenue", label: "Promet", dataType: "currency" },
          { key: "coverage", label: "Pokrivenost", dataType: "percent" },
          { key: "note", label: "Napomena" },
        ],
        rows: [{
          supplier: "Sintetički dobavljač sa dugim nazivom",
          articles: 1200,
          receipts: 48,
          revenue: 1456000,
          coverage: 82.5,
          note: "Sintetički sadržaj za proveru da široka tabela ostaje dostupna u svom scroll regionu.",
        }],
      }],
      payload: { tableKey: "responsive-pilot-intake-fixture", columns: [], rows: [], metadata: [] },
      meta: { success: true, dataQualityStatus: "good" },
    }) };
  }

  if (url.pathname === "/api/analytics/cached/inventory/insights" || url.pathname === "/api/analytics/inventory/insights") {
    const insightItem = {
      id: 330,
      plu: "RESP-330",
      naziv: "Sintetički model sa izuzetno dugim nazivom koji mora ostati dostupan kroz naslov kartice",
      supplierName: "Sintetički dobavljač sa dugim nazivom za responsive proveru",
      storeName: "Sintetička prodavnica sa dugim nazivom",
      supplierId: 21,
      storeId: 31,
      quantity: 4,
      minimum: 12,
      reorderGap: 8,
      estimatedValue: 18400,
      unitCost: 4600,
      costSource: "fixture",
      costMissing: false,
      daysSinceMovement: 104,
      agingBucket: "90+",
      agingLabel: "90+ dana",
      abcClass: "A",
      stockState: "critical",
      stockCoverDays: 3,
      stockCoverStatus: "low_cover",
      stockCoverStatusLabel: "Niska pokrivenost",
      sellThroughRatio: 82,
      sellThroughStatus: "good",
      sellThroughStatusLabel: "Dobra prodajnost",
      signalConfidencePct: 88,
      recommendationAllowed: true,
      dataQualityStatus: "good",
      reasonCodes: ["fixture"],
    };
    return { status: 200, body: JSON.stringify({
      totalItems: 1,
      totalEstimatedValue: 18400,
      aging: [{ bucketKey: "90+", label: "90+ dana", itemCount: 1, totalUnits: 4, estimatedValue: 18400 }],
      abc: [{ bucketKey: "A", label: "A", itemCount: 1, estimatedValue: 18400, valueSharePct: 100 }],
      topAgedItems: [insightItem],
      topCapitalLockedItems: [insightItem],
      meta: { success: true, dataQualityStatus: "good" },
    }) };
  }

  if (url.pathname === "/api/artikli/lookup") {
    return { status: 200, body: JSON.stringify([
      { id: 101, naziv: "Sintetičke patike A", cena: 7490, kolicina: 8 },
      { id: 102, naziv: "Sintetičke patike B", cena: 8290, kolicina: 5 },
    ]) };
  }
  if (url.pathname === "/api/dobavljaci") {
    return { status: 200, body: JSON.stringify([
      { id: 21, naziv: "Sintetički dobavljač A", adresa: "Novi Sad", telefon: "0600000000" },
      { id: 22, naziv: "Sintetički dobavljač B", adresa: "Beograd", telefon: "0610000000" },
    ]) };
  }
  if (url.pathname === "/api/sezone") {
    return { status: 200, body: JSON.stringify([
      { id: 31, naziv: "Sintetička sezona", datumOd: "2026-01-01T00:00:00Z", datumDo: "2026-12-31T23:59:59Z" },
    ]) };
  }
  if (url.pathname === "/api/workers/health") {
    return { status: 200, body: JSON.stringify({
      totalWorkers: 0, healthyWorkers: 0, runningWorkers: 0, errorWorkers: 0,
      stoppedWorkers: 0, staleWorkers: 0, hasCriticalIssues: false, workers: [],
    }) };
  }
  if (url.pathname === "/artikli") {
    return { status: 200, body: JSON.stringify([
      { id: 101, naziv: "Sintetičke patike A", prodajnaCena: 7490, nabavnaCena: 4200, prvaProdajnaCena: 7990, kolicina: 8 },
      { id: 102, naziv: "Sintetičke patike B", prodajnaCena: 8290, nabavnaCena: 4700, prvaProdajnaCena: 8790, kolicina: 5 },
    ]) };
  }
  if (url.pathname === "/api/artikli" && request.method() === "GET") {
    const pageNumber = Number(url.searchParams.get("pageNumber") ?? 1);
    return { status: 200, body: JSON.stringify({
      items: [{
        id: 700 + pageNumber, naziv: `Sintetičke patike ${pageNumber}`, prodajnaCena: 7490,
        kolicina: 8, nabavnaCena: 4200, dobavljacId: 21, dobavljacNaziv: "Sintetički dobavljač A",
      }],
      totalCount: 85, pageNumber, pageSize: Number(url.searchParams.get("pageSize") ?? 50),
    }) };
  }
  if (url.pathname === "/api/prodaja" && request.method() === "POST") {
    return { status: 201, body: JSON.stringify({ id: 1, status: "fixture" }) };
  }
  if (url.pathname === "/api/analytics/actions" && request.method() === "GET") {
    return { status: 200, body: JSON.stringify({ items: [
      {
        id: 330, sourceType: "inventory", sourceKey: "responsive-fixture-330", sourceId: 330,
        title: "Sintetička akcija za responsive test", description: "Fixture akcija za filter, pregled i modal.",
        recommendationStatus: "REPLENISH", priority: "P1", impactEstimateRsd: 12000, dueAtUtc: "2026-10-10T10:00:00Z",
        expectedImpactRsd: 8000, measuredImpactRsd: null, outcomeStatus: "pending", outcomeMeasuredAtUtc: null,
        outcomeNotes: null, confidencePct: 82, reliabilityPct: 76, dataQualityStatus: "good", status: "new",
        actionUrl: "/analytics/inventory", metadataJson: null, ledgerSnapshot: null, impactLedger: null,
        createdAtUtc: "2026-10-01T08:00:00Z", updatedAtUtc: "2026-10-01T08:00:00Z", resolvedAtUtc: null,
        createdByUserId: "fixture", updatedByUserId: null, updatedByUserName: null, notes: [],
      },
    ], totalCount: 1, page: 1, pageSize: 50, totalPages: 1, meta: { success: true, dataQualityStatus: "good" } }) };
  }
  if (url.pathname === "/api/analytics/actions/counts") {
    return { status: 200, body: JSON.stringify({ new: 1, accepted: 0, deferred: 0, rejected: 0, done: 0, p1Open: 1, meta: { success: true, dataQualityStatus: "good" } }) };
  }
  if (url.pathname === "/api/analytics/actions/outcomes/summary") {
    return { status: 200, body: JSON.stringify({
      meta: { success: true, periodMode: "created", generatedAtUtc: "2026-10-02T00:00:00Z", sampleSize: 1, measuredSampleSize: 0, warnings: [], emptyReason: null },
      totals: { createdCount: 1, closedCount: 0, openCount: 1 },
      impact: { expectedImpactRsd: null, measuredImpactRsd: null, measuredImpactSampleCount: 0 },
      bySourceType: [], byPriority: [], byOutcomeStatus: [], byDataQuality: [], byConfidenceBucket: [], byReliabilityBucket: [],
    }) };
  }
  if (/^\/api\/analytics\/actions\/\d+\/status$/.test(url.pathname) && request.method() === "PATCH") {
    return { status: 200, body: JSON.stringify({
      id: 330, sourceType: "inventory", sourceKey: "responsive-fixture-330", sourceId: 330,
      title: "Sintetička akcija za responsive test", description: "Fixture akcija za filter, pregled i modal.",
      recommendationStatus: "REPLENISH", priority: "P1", impactEstimateRsd: 12000, dueAtUtc: "2026-10-10T10:00:00Z",
      expectedImpactRsd: 8000, measuredImpactRsd: null, outcomeStatus: "pending", outcomeMeasuredAtUtc: null,
      outcomeNotes: null, confidencePct: 82, reliabilityPct: 76, dataQualityStatus: "good", status: "deferred",
      actionUrl: "/analytics/inventory", metadataJson: null, ledgerSnapshot: null, impactLedger: null,
      createdAtUtc: "2026-10-01T08:00:00Z", updatedAtUtc: "2026-10-02T00:00:00Z", resolvedAtUtc: null,
      createdByUserId: "fixture", updatedByUserId: "fixture", updatedByUserName: "Fixture", notes: [],
    }) };
  }

  if (url.pathname === "/api/analytics/pre-nivelacija-prioriteti") {
    return { status: 200, body: JSON.stringify({
      generatedAtUtc: "2026-10-02T00:00:00Z",
      formulaVersion: "responsive-fixture",
      formulaDescription: "Sintetički fixture za responsive kontrolu filtera.",
      modelEvidence: {
        scoreBasis: "synthetic fixture",
        scoreReferencePopulation: "synthetic fixture",
        scoreNormalization: "synthetic fixture",
        scenarioBasis: "synthetic fixture",
        scenarioParameterVersion: "synthetic fixture",
        scenarioDisclaimer: "Sintetički podaci; nije poslovna preporuka.",
      },
      summary: {
        supplierCount: 1, candidatesCount: 1, highPriorityCount: 1, increaseFocusCount: 0,
        maintainCount: 0, reviewCount: 0, doNotTrustCount: 0, insufficientDataCount: 1,
        totalStockAtRisk: 12, totalStockAtRiskCoverageEligible: 1, totalStockAtRiskCoverageTotal: 1,
        estimatedAvoidableMarkdownLoss: null, estimatedAvoidableMarkdownLossCoverageEligible: 0,
        estimatedAvoidableMarkdownLossCoverageTotal: 0, expectedHighlightRevenueUplift: null,
        expectedHighlightRevenueUpliftCoverageEligible: 0, expectedHighlightRevenueUpliftCoverageTotal: 0,
        averagePreNivelacijaScore: 0,
      },
      supplierLeaderboard: [],
      filterFacets: {
        suppliers: [], seasons: [], footwearTypes: [], stores: [{ id: 1, label: PUI39_LONG_STORE_OPTION }],
      },
      candidates: [{
        artikalId: 701, sku: "RESP-701", storeId: 1, storeName: "Fixture prodavnica", supplierId: 21,
        seasonId: 31, footwearTypeId: 4, supplierName: "Fixture dobavljač", category: "Patike",
        footwearType: "Patike", season: "Fixture sezona", stockUnits: 12, units180: 24,
        positiveUnits180: 24, negativeUnits180: 0, velocity180: 0.8, daysSinceLastSale: 45,
        salesHistoryStatus: "sold", firstReceiptDateUtc: "2026-01-01T00:00:00Z", daysSinceReceipt: 90,
        receiptEvidenceStatus: "received", stockAgeStatus: "established", markdownEvents: 1,
        avgMarkdownPct: 12, grossMarginPctEst: 34, grossMarginPctSigned: 34, belowCost: false,
        seasonRecencyBoost: 18, preNivelacijaScore: 86, priorityBand: "high",
        scoreBreakdown: { stockPressure: 71, velocityRisk: 63, recencyRisk: 54, markdownOpportunity: 42, marginPotential: 61, seasonRecencyBoost: 18 },
        scenarioHighlightNow: { expectedUnits30d: 16, expectedRevenue30d: 124000, expectedMargin30d: 36000, effectivePrice: 7990 },
        scenarioMarkdownNow: { expectedUnits30d: 18, expectedRevenue30d: 117000, expectedMargin30d: 33000, effectivePrice: 7190 },
        marginDeltaHighlightVsMarkdown: 3000, revenueDeltaHighlightVsMarkdown: 7000,
        hasCompleteEvidence: false, evidenceReason: "synthetic_fixture", salesEvidenceStatus: "positive_net_sales",
        salesEvidenceReason: null, confidence: "Srednja", reliabilityPct: 62, decisionScore: 72,
        recommendationAllowed: false,
        recommendation: { status: "insufficient_data", label: "Nedovoljno podataka", summary: "Sintetički signal za responsive merenje.", confidencePct: 22, reliabilityPct: 28, dataQualityStatus: "insufficient_data", recommendationAllowed: false, reasonCodes: ["responsive_fixture"] },
      }],
      queues: { highlightNow: [], monitor: [], likelyMarkdownSoon: [] },
      alerts: [], page: 1, pageSize: 20, totalCandidates: 0, recommendationAllowed: false,
      evidenceWindow: null,
      meta: { success: true, dataQualityStatus: "insufficient_data", emptyReason: "no_data_in_period" },
    }) };
  }

  if (url.pathname === "/api/analytics/supplier-sales-stats") {
    const supplier = {
      dobavljacId: 21, dobavljacNaziv: "Sintetički dobavljač A", isUnknown: false,
      preNivelacijePromet: 0, preNivelacijeKolicina: 0, posleNivelacijePromet: 100000,
      posleNivelacijeKolicina: 12, ukupanPromet: 100000, ukupnaKolicina: 12,
      previousPeriodRevenue: 80000, previousPeriodUnits: 10, brojArtikalaSaNivelacijom: 2,
      brojArtikalaUkupno: 8, revenueWithCost: 100000, estimatedCostRevenue: 0,
      marginContribution: 38000, marginDataCoveragePct: 100, fallbackCostCoveragePct: 0,
      marginPct: 38, totalCost: 62000, historicalCostRevenue: 100000,
      historicalCostCoveragePct: 100, estimatedCostCoveragePct: 0, snapshotCostRevenue: 0,
      snapshotCostCoveragePct: 0, noCostRevenue: 0, noCostCoveragePct: 0, isEstimatedMargin: false,
      marginQualityLabel: "Istorijski potvrđena", marginQualityTier: "confirmed",
      marginQualityShortLabel: "Potvrđena", marginQualityTooltip: "Sintetički responsive fixture.",
      popRevenueChangePct: 25, popUnitsChangePct: 20, prePostNivelacijaRevenueImpactPct: null,
      prePostNivelacijaUnitsImpactPct: null, prePostNivelacijaRevenueCoveragePct: null,
      prePostSignalNote: null, prePostComparableArticleCount: 0, primaryFootwearType: "Patike",
      primaryFootwearTypeSharePct: 100, footwearTypeCount: 1,
      footwearBreakdown: [{ tipObuceId: 4, tipObuceNaziv: "Patike", ukupanPromet: 100000,
        ukupnaKolicina: 12, brojArtikala: 8, totalCost: 62000, marginContribution: 38000,
        marginPct: 38, shareOfSupplierRevenuePct: 100, shareOfSupplierMarginContributionPct: 100,
        previousPeriodRevenue: 80000, previousPeriodUnits: 10, popRevenueChangePct: 25,
        popUnitsChangePct: 20 }],
      sharePct: 100, shareOfMarginContribution: 100, shareOfProfit: 100, shareOfUnits: 100,
      reliabilityPct: 88, recommendation: { status: "maintain", label: "Zadrži",
        summary: "Sintetički signal za responsive merenje.", confidencePct: 88, reliabilityPct: 88,
        dataQualityStatus: "good", recommendationAllowed: true, reasonCodes: ["responsive_fixture"] },
      promenaKolicine: 20,
    };
    const totals = {
      ukupanPromet: 100000, ukupanMarzniDoprinos: 38000, ukupanTrosak: 62000,
      prosecnaMarza: 38, weightedMarginRevenue: 100000, weightedMarginContribution: 38000,
      marginBenchmarkBasis: "known_supplier_covered_revenue_weighted", historicalCostCoveragePct: 100,
      estimatedCostCoveragePct: 0, noCostCoveragePct: 0, snapshotCostRevenue: 0, snapshotCostCoveragePct: 0,
      isSnapshotActive: false, snapshotGeneratedAtUtc: null, isEstimatedMargin: false,
      marginQualityLabel: "Istorijski potvrđena", marginQualityTier: "confirmed",
      marginQualityShortLabel: "Potvrđena", marginQualityTooltip: "Sintetički responsive fixture.",
      prePromet: 0, poslePromet: 100000, ukupnaKolicina: 12, preKolicina: 0, posleKolicina: 12,
      previousPeriodRevenue: 80000, previousPeriodUnits: 10, brojDobavljaca: 1,
      popRevenueChangePct: 25, popUnitsChangePct: 20, prePostNivelacijaRevenueImpactPct: null,
      prePostNivelacijaUnitsImpactPct: null,
      recommendationSummary: { increaseFocus: 0, maintain: 1, review: 0, doNotTrust: 0, insufficientData: 0 },
    };
    return { status: 200, body: JSON.stringify({
      generatedAt: "2026-10-02T00:00:00Z", meta: { success: true, dataQualityStatus: "good" },
      fromDate: "2026-09-01T00:00:00Z", toDate: "2026-10-01T00:00:00Z",
      dataWindowFrom: "2026-04-01T00:00:00Z", dataWindowTo: "2026-10-01T00:00:00Z",
      sezonaId: null, storeId: null, dataScope: "all", provenanceBasis: "responsive_fixture",
      recommendationAllowed: true, recommendationReferenceCohort: { scope: "all_response_suppliers",
        supplierCount: 1, includesUnknown: false, basis: "responsive_fixture" },
      suppliers: [supplier], totals, dataQuality: {
        missingCostQty: 0, missingCostRevenue: 0, missingCostRevenueSharePct: 0,
        noCostRevenue: 0, noCostRevenueSharePct: 0, costCoveredRevenue: 100000,
        costCoveredRevenueSharePct: 100, historicalCostRevenue: 100000,
        historicalCostRevenueSharePct: 100, snapshotCostRevenue: 0, snapshotCostRevenueSharePct: 0,
        estimatedCostRevenue: 0, estimatedCostRevenueSharePct: 0,
        costSourceBasis: "responsive_fixture", unknownSupplierRevenue: 0,
        unknownSupplierRevenueSharePct: 0, revenueWithNivelacijaSplit: 0,
        revenueWithNivelacijaSplitSharePct: null,
      }, sezone: [],
    }) };
  }

  if (url.pathname === "/api/analytics/color-sales-stats") {
    const colors = [
      {
        boja: "Crna", preNivelacijePromet: 90000, preNivelacijeKolicina: 9,
        posleNivelacijePromet: 30000, posleNivelacijeKolicina: 3, ukupanPromet: 120000,
        ukupnaKolicina: 12, previousPeriodRevenue: 80000, previousPeriodUnits: 8,
        brojArtikalaSaNivelacijom: 5, brojArtikalaUkupno: 8, revenueWithCost: 100000,
        estimatedCostRevenue: 20000, marginContribution: 46000, marginDataCoveragePct: 83.3,
        fallbackCostCoveragePct: 16.7, marginPct: 38.3, revenueWithNivelacijaSplit: 100000,
        comparablePreRevenue: 90000, comparablePostRevenue: 30000, comparablePreQuantity: 9,
        comparablePostQuantity: 3, popRevenueChangePct: 50, popUnitsChangePct: 20,
        prePostNivelacijaRevenueImpactPct: -12.5, prePostNivelacijaUnitsImpactPct: -10,
        prePostNivelacijaRevenueCoveragePct: 75, prePostSignalNote: "Fixture: uporediv signal.",
        prePostComparableArticleCount: 5, sharePct: 60, recommendation: {
          status: "increase_focus", label: "Povećaj fokus", summary: "Sintetički fixture red.",
          confidencePct: 88, reliabilityPct: 82, dataQualityStatus: "good",
          recommendationAllowed: true, reasonCodes: ["fixture"],
        },
      },
      {
        boja: "Bež", preNivelacijePromet: 30000, preNivelacijeKolicina: 3,
        posleNivelacijePromet: 15000, posleNivelacijeKolicina: 2, ukupanPromet: 45000,
        ukupnaKolicina: 5, previousPeriodRevenue: 56000, previousPeriodUnits: 6,
        brojArtikalaSaNivelacijom: 2, brojArtikalaUkupno: 4, revenueWithCost: 40000,
        estimatedCostRevenue: 5000, marginContribution: 12000, marginDataCoveragePct: 80,
        fallbackCostCoveragePct: 10, marginPct: 30, revenueWithNivelacijaSplit: 40000,
        comparablePreRevenue: 30000, comparablePostRevenue: 15000, comparablePreQuantity: 3,
        comparablePostQuantity: 2, popRevenueChangePct: -20, popUnitsChangePct: -10,
        prePostNivelacijaRevenueImpactPct: -8, prePostNivelacijaUnitsImpactPct: -5,
        prePostNivelacijaRevenueCoveragePct: 60, prePostSignalNote: "Fixture: manji uzorak.",
        prePostComparableArticleCount: 2, sharePct: 22.5, recommendation: {
          status: "review", label: "Pregledaj", summary: "Sintetički fixture red.",
          confidencePct: 61, reliabilityPct: 70, dataQualityStatus: "warning",
          recommendationAllowed: false, reasonCodes: ["fixture"],
        },
      },
    ];
    const rowValues = colors.map((color) => color.ukupanPromet);
    return {
      status: 200,
      body: JSON.stringify({
        generatedAt: "2026-10-02T00:00:00Z",
        meta: { success: true, requestedPeriodFromUtc: "2026-09-03T00:00:00Z", requestedPeriodToUtc: "2026-10-02T23:59:59Z", effectivePeriodFromUtc: "2026-09-03T00:00:00Z", effectivePeriodToUtc: "2026-10-02T23:59:59Z", dataQualityStatus: "warning" },
        fromDate: "2026-09-03T00:00:00Z", toDate: "2026-10-02T23:59:59Z",
        dataWindowFrom: "2024-01-01T00:00:00Z", dataWindowTo: "2026-10-02T23:59:59Z",
        sezonaId: null, storeId: null, dataScope: "all", colors,
        lineage: {
          storeId: null, dataScope: "all", sourceFamily: "fixture", sourceLabel: "Responsive test fixture — sintetički podaci",
          eventCount: 2, eventArticleCount: 2, salesArticleCount: 12, salesArticlesWithMatchingNivelacija: 7,
          storePolicy: "fixture", originPolicy: "fixture",
        },
        totals: {
          ukupanPromet: rowValues.reduce((sum, value) => sum + value, 0), ukupanMarzniDoprinos: 58000,
          prePromet: 120000, poslePromet: 45000, ukupnaKolicina: 17, preKolicina: 12, posleKolicina: 5,
          previousPeriodRevenue: 136000, previousPeriodUnits: 14, popRevenueChangePct: 21,
          popUnitsChangePct: 18, prePostNivelacijaRevenueImpactPct: -8, prePostNivelacijaUnitsImpactPct: -6,
          comparablePreRevenue: 120000, comparablePostRevenue: 45000, comparablePreQuantity: 12,
          comparablePostQuantity: 5, comparableArticleCount: 10, comparableRevenueCoveragePct: 75,
          prePostSignalNote: "Sintetički layout fixture.", observedPreRevenue: 120000, observedPostRevenue: 45000,
          observedPreQuantity: 12, observedPostQuantity: 5, weightedKnownMarginPct: 35,
          weightedKnownMarginRevenue: 100000,
          recommendationSummary: { increaseFocus: 1, maintain: 0, review: 1, doNotTrust: 0, insufficientData: 0 },
        },
        dataQuality: {
          missingCostRevenue: 5000, missingCostRevenueSharePct: 3, estimatedCostRevenue: 25000,
          estimatedCostRevenueSharePct: 15, unknownColorRevenue: 0, unknownColorRevenueSharePct: 0,
          revenueWithNivelacijaSplit: 140000, revenueWithNivelacijaSplitSharePct: 85,
          observedRevenueWithNivelacijaSplit: 140000, observedRevenueWithNivelacijaSplitSharePct: 85,
          weightedKnownMarginPct: 35, weightedKnownMarginRevenue: 100000,
        },
        sezone: [],
      }),
    };
  }

  if (url.pathname === "/api/analytics/cached/dashboard/bootstrap") {
    const fixtureEnd = new Date("2026-10-02T00:00:00Z");
    const dailySales = Array.from({ length: 14 }, (_, index) => {
      const date = new Date(fixtureEnd);
      date.setUTCDate(fixtureEnd.getUTCDate() - (13 - index));
      return {
        date: date.toISOString().slice(0, 10),
        totalRevenue: 84000 + index * 2750,
        transactionCount: 32 + index,
        totalUnits: 58 + index * 2,
      };
    });
    return {
      status: 200,
      body: JSON.stringify({
        summary: { totalRevenue: 1380000, totalTransactions: 512, totalItems: 1082, supplierCount: 4 },
        inventory: { totalSkuCount: 540, totalOnHand: 2180, outOfStockCount: 18, lowStockCount: 42 },
        dailySales,
        categoryData: [
          { kategorija: "Patike", pol: "Ženski", totalRevenue: 640000, totalUnits: 420, transactionCount: 180 },
          { kategorija: "Čizme", pol: "Muški", totalRevenue: 430000, totalUnits: 280, transactionCount: 126 },
          { kategorija: "Sandale", pol: "Ženski", totalRevenue: 310000, totalUnits: 382, transactionCount: 206 },
        ],
        genderData: [
          { pol: "Ženski", totalRevenue: 810000, totalUnits: 648, transactionCount: 290 },
          { pol: "Muški", totalRevenue: 570000, totalUnits: 434, transactionCount: 222 },
        ],
        supplierData: [
          { dobavljacId: 1, dobavljacNaziv: "Sintetički dobavljač A", totalRevenue: 620000, totalUnits: 390, transactionCount: 180 },
          { dobavljacId: 2, dobavljacNaziv: "Sintetički dobavljač B", totalRevenue: 480000, totalUnits: 300, transactionCount: 140 },
          { dobavljacId: 3, dobavljacNaziv: "Sintetički dobavljač C", totalRevenue: 280000, totalUnits: 220, transactionCount: 92 },
        ],
        supplierOptions: [
          { supplierId: 1, supplierName: "Sintetički dobavljač A" },
          { supplierId: 2, supplierName: "Sintetički dobavljač B" },
        ],
        weekdayData: [
          { dayOfWeek: 0, dayName: "Ponedeljak", totalRevenue: 175000, transactionCount: 52 },
          { dayOfWeek: 1, dayName: "Utorak", totalRevenue: 182000, transactionCount: 55 },
          { dayOfWeek: 2, dayName: "Sreda", totalRevenue: 191000, transactionCount: 58 },
          { dayOfWeek: 3, dayName: "Četvrtak", totalRevenue: 204000, transactionCount: 61 },
          { dayOfWeek: 4, dayName: "Petak", totalRevenue: 230000, transactionCount: 68 },
          { dayOfWeek: 5, dayName: "Subota", totalRevenue: 276000, transactionCount: 81 },
          { dayOfWeek: 6, dayName: "Nedelja", totalRevenue: 122000, transactionCount: 37 },
        ],
        hourData: [
          { hour: 9, totalRevenue: 82000, transactionCount: 31 }, { hour: 12, totalRevenue: 176000, transactionCount: 64 },
          { hour: 15, totalRevenue: 211000, transactionCount: 76 }, { hour: 18, totalRevenue: 259000, transactionCount: 92 },
        ],
        paymentData: [
          { nacinPlacanja: "Kartica", totalRevenue: 790000, transactionCount: 301 },
          { nacinPlacanja: "Gotovina", totalRevenue: 410000, transactionCount: 169 },
          { nacinPlacanja: "Ostalo", totalRevenue: 180000, transactionCount: 42 },
        ],
        quickInsights: null,
        transactionStats: null,
        advanced: null,
        topAdvanced: null,
        validationCompleteness: null,
        validationFreshness: null,
        validationLostSales: null,
        decisionActions: [],
        executive: null,
        errors: [],
        meta: { success: true, dataQualityStatus: "warning", warnings: ["Sintetički responsive fixture — vrednosti nisu stvarni podaci."] },
      }),
    };
  }

  if (url.pathname === "/api/analytics/daily-sales") {
    const toDate = url.searchParams.get("toDate") || "2026-10-02";
    const fromDate = url.searchParams.get("fromDate") || "2026-09-03";
    const end = new Date(`${toDate.slice(0, 10)}T00:00:00Z`);
    const dateRows = Array.from({ length: 14 }, (_, index) => {
      const date = new Date(end);
      date.setUTCDate(end.getUTCDate() - (13 - index));
      const key = date.toISOString().slice(0, 10);
      const firstShift = 34 + index * 2;
      const secondShift = 28 + index;
      const firstSupplier = 18 + index;
      const secondSupplier = 12 + index;
      return {
        date: key,
        firstShiftTotalItems: firstShift,
        secondShiftTotalItems: secondShift,
        totalRevenue: 88000 + index * 3200,
        topSupplierCounts: [firstSupplier, secondSupplier],
        othersCount: 16,
        totalItemsSold: firstShift + secondShift,
      };
    });
    const topSuppliers = [
      { supplierId: 11, supplierName: "Sintetički dobavljač A", isUnknown: false, totalQty: 390, totalRevenue: 620000 },
      { supplierId: 12, supplierName: "Sintetički dobavljač B", isUnknown: false, totalQty: 300, totalRevenue: 480000 },
    ];
    return {
      status: 200,
      body: JSON.stringify({
        requestedFrom: fromDate,
        requestedTo: toDate,
        storeId: null,
        topN: 5,
        dataScope: "all",
        topSuppliers,
        topSuppliersOrder: topSuppliers.map((supplier) => supplier.supplierName),
        dateRows,
        metadata: {
          totalDays: 30,
          uniqueSuppliersInRange: 2,
          unknownSupplierPct: 0,
          unknownSupplierItems: 0,
          shiftAssignmentStatus: "measured",
          shiftTimeZone: "Europe/Belgrade",
          shiftTimestampBasis: "fixture",
          shiftTimestampBasisKnownRows: 14,
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
          receiptReconciliation: { status: "unavailable", reasonCode: "synthetic_fixture", matchedReceiptCount: 0, unmatchedReceiptCount: 0, unmatchedDnevnikReceiptCount: 0, mismatchCount: 0, mismatchAmount: 0 },
          nonStandardReceiptCount: 0,
          nonStandardReceiptRevenue: 0,
          debtReceiptCount: 0,
          debtReceiptRevenue: 0,
          diagnosticsDataScope: "all",
          availabilityDataScope: "all",
          minAvailableDate: fromDate,
          maxAvailableDate: toDate,
          warnings: ["Sintetički responsive fixture — vrednosti nisu stvarni podaci."],
        },
        meta: { success: true, dataQualityStatus: "warning", warnings: ["Sintetički responsive fixture — vrednosti nisu stvarni podaci."] },
      }),
    };
  }

  if (url.pathname === "/api/analytics/cached/filters/stores") {
    return { status: 200, body: JSON.stringify([{ storeId: 1, storeName: PUI39_LONG_STORE_OPTION }]) };
  }

  if (url.pathname === "/api/analytics/cached/products/decision-center") {
    const rows = Array.from({ length: options.productRowCount }, (_, index) => {
      const productId = index + 1;
      return {
        productId,
        recommendationId: `product:${productId}:REPLENISH:20260903:20261002`,
        sourceType: "product",
        sourceKey: `product:${productId}`,
        recommendationType: "REPLENISH",
        sku: `FIX-${String(productId).padStart(4, "0")}`,
        productName: `Sintetički model ${String(productId).padStart(4, "0")}`,
        supplierId: 1,
        supplierName: "Sintetički dobavljač",
        category: "Obuća",
        revenue: 120000 - index,
        unitsSold: 40,
        velocityUnitsPerDay: 1.2,
        marginContribution: 24000,
        marginPct: 24,
        marginQualityLabel: "Dobro",
        marginCoveragePct: 90,
        currentStock: 10,
        minStock: 5,
        stockGap: 0,
        trendPct: 3,
        lostSalesEstimate: 25000,
        slowStockCapital: 0,
        stockCoverDays: 5,
        stockCoverStatus: "low_cover",
        stockCoverStatusLabel: "Niska pokrivenost",
        sellThroughRatio: 0.6,
        sellThroughStatus: "good",
        sellThroughStatusLabel: "Dobra prodajnost",
        confidencePct: 88,
        reliabilityPct: 80,
        confidenceLevel: "high",
        confidenceScore: 88,
        recommendationAllowed: true,
        dataQualityStatus: "good",
        recommendationStatus: "REPLENISH",
        recommendationLabel: "Dopuni",
        recommendationReason: "Sintetički razlog za responsive merenje.",
        reasonCodes: ["fixture"],
        warningCodes: [],
        primaryDrivers: ["sales_velocity"],
        expectedImpactRsd: 25000,
        impactWindowDays: 14,
        explainabilityText: "Sintetički dokaz; nije stvarna preporuka.",
        inputFreshnessStatus: "fresh",
        recommendedAction: "Dopuni zalihe",
        daysSinceLastSale: 12,
      };
    });
    return {
      status: 200,
      body: JSON.stringify({
        generatedAtUtc: "2026-10-02T00:00:00Z",
        periodFromUtc: "2026-09-03T00:00:00Z",
        periodToUtc: "2026-10-02T23:59:59Z",
        totalRows: rows.length,
        analyzedRows: rows.length,
        ignoredRowsCount: 0,
        summary: { replenishCount: rows.length, markdownCount: 0, highPotentialCount: rows.length, badDataCount: 0, actionableCount: rows.length, blockedCount: 0, lostSalesEstimate: 30000000, slowStockCapital: 0 },
        rows,
        meta: { success: true, dataQualityStatus: "good", requestedPeriodFromUtc: "2026-09-03T00:00:00Z", requestedPeriodToUtc: "2026-10-02T23:59:59Z", effectivePeriodFromUtc: "2026-09-03T00:00:00Z", effectivePeriodToUtc: "2026-10-02T23:59:59Z" },
      }),
    };
  }

  if (url.pathname.includes("source-statuses")) {
    return { status: 200, body: JSON.stringify({ items: [] }) };
  }

  return {
    status: 503,
    body: JSON.stringify({
      errorCode: "responsive_baseline_fixture",
      message: "Fixture režim: backend podaci nisu deo responsive baseline dokaza.",
      meta: {
        success: false,
        dataQualityStatus: "insufficient_data",
      },
    }),
  };
}

async function collectGeometry(page, viewportWidth) {
  const documentMetrics = await page.evaluate(() => {
    const isVisible = (element) => {
      const style = window.getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== "none"
        && style.visibility !== "hidden"
        && Number.parseFloat(style.opacity || "1") > 0
        && rect.width > 0
        && rect.height > 0;
    };
    const rectValue = (rect) => rect
      ? {
        x: Math.round(rect.x * 100) / 100,
        y: Math.round(rect.y * 100) / 100,
        width: Math.round(rect.width * 100) / 100,
        height: Math.round(rect.height * 100) / 100,
      }
      : null;
    const controls = [...document.querySelectorAll("input, button, select, textarea")]
      .filter(isVisible)
      .slice(0, 40)
      .map((element) => {
        const rect = element.getBoundingClientRect();
        return {
          tag: element.tagName.toLowerCase(),
          role: element.getAttribute("role"),
          label: element.getAttribute("aria-label") || element.textContent?.trim().slice(0, 60) || null,
          fontSizePx: Number.parseFloat(window.getComputedStyle(element).fontSize),
          rect: rectValue(rect),
        };
      });
    const touchAuditSelectors = [
      "button", ".btn", ".button-big", "[role='button']",
      "input:not([type='checkbox']):not([type='radio']):not([type='range'])",
      "select", "textarea", "[role='banner'] a", ".analytics-trust-header a:not(p a)",
      ".analytics-trust-header button", ".analytics-data-table__toolbar a",
      ".analytics-data-table__toolbar button", ".analytics-data-table__toolbar [role='button']",
      ".analytics-empty-state a:not(p a)", ".arb-link", ".kpi-explain-button", ".info-tip",
    ].join(",");
    const touchAudit = [...new Set(document.querySelectorAll(touchAuditSelectors))]
      .filter(isVisible)
      .map((element) => {
        const rect = element.getBoundingClientRect();
        const style = window.getComputedStyle(element);
        const isTextEntry = element.matches("input:not([type='checkbox']):not([type='radio']):not([type='range']), select, textarea");
        return {
          selector: [element.tagName.toLowerCase(), element.className?.baseVal ?? element.className ?? "", element.getAttribute("aria-label") || element.textContent?.trim().slice(0, 40) || ""].filter(Boolean).join("."),
          kind: isTextEntry ? "text-entry" : "target",
          width: Math.round(rect.width * 100) / 100,
          height: Math.round(rect.height * 100) / 100,
          fontSizePx: Number.parseFloat(style.fontSize),
        };
      });
    const pageLocalTouchOffenders = [...document.querySelectorAll("a:not(p a)")]
      .filter((element) => isVisible(element)
        && !element.closest(".sr-only, [role='banner'], header, .analytics-trust-header, .analytics-data-table__toolbar, .analytics-empty-state, .analytics-refresh-status-banner"))
      .map((element) => {
        const rect = element.getBoundingClientRect();
        return {
          className: typeof element.className === "string" ? element.className : null,
          label: element.getAttribute("aria-label") || element.textContent?.trim().slice(0, 60) || null,
          width: Math.round(rect.width * 100) / 100,
          height: Math.round(rect.height * 100) / 100,
        };
      })
      .filter((element) => element.width < 44 || element.height < 44);

    const regions = [...document.querySelectorAll(
      ".pilot-intake-durable-table-wrap, .pilot-intake-durable-sections, .pilot-intake-durable-section, .pilot-intake-card, .analytics-trust-header, [data-testid='analytics-control-bar'], [class~='analytics-data-table__scroll'], [class*='control-bar'], [class*='chart-grid'], [class*='chart-wrap'], [class*='card-grid'], [class*='daily-sales-kpis'], [class*='command-center__hero'], table, [role='dialog'], [role='banner'], [data-testid*='data-table'], [class*='filter'], [class*='toolbar']",
    )]
      .filter(isVisible)
      .slice(0, 40)
      .map((element) => ({
        tag: element.tagName.toLowerCase(),
        testId: element.getAttribute("data-testid"),
        className: typeof element.className === "string" ? element.className.slice(0, 120) : null,
        rect: rectValue(element.getBoundingClientRect()),
        scrollWidth: element.scrollWidth,
        clientWidth: element.clientWidth,
      }));

    const overflowingElements = [...document.querySelectorAll("body *")]
      .map((element) => ({ element, rect: element.getBoundingClientRect() }))
      .filter(({ element, rect }) => rect.left < -1 || rect.right > window.innerWidth + 1 || element.scrollWidth > element.clientWidth + 1)
      .sort((left, right) => Math.max(right.rect.right - window.innerWidth, right.element.scrollWidth - right.element.clientWidth) - Math.max(left.rect.right - window.innerWidth, left.element.scrollWidth - left.element.clientWidth))
      .slice(0, 20)
      .map(({ element, rect }) => ({
        tag: element.tagName.toLowerCase(),
        className: typeof element.className === "string" ? element.className.slice(0, 120) : null,
        text: element.textContent?.trim().slice(0, 80) || null,
        rect: rectValue(rect),
        scrollWidth: element.scrollWidth,
        clientWidth: element.clientWidth,
        overflowX: window.getComputedStyle(element).overflowX,
        ancestors: [...function* ancestors(node) {
          for (let parent = node.parentElement; parent && parent !== document.body; parent = parent.parentElement) yield parent;
        }(element)].slice(0, 5).map((parent) => ({
          tag: parent.tagName.toLowerCase(),
          className: typeof parent.className === "string" ? parent.className.slice(0, 100) : null,
          rect: rectValue(parent.getBoundingClientRect()),
          overflowX: window.getComputedStyle(parent).overflowX,
        })),
      }));
    const chartRegions = [...document.querySelectorAll(".chart-wrap, .daily-sales-chart-wrap")]
      .filter(isVisible)
      .map((element) => {
        const rect = element.getBoundingClientRect();
        const chartSurface = element.querySelector(".recharts-surface");
        return {
          className: typeof element.className === "string" ? element.className : null,
          rect: rectValue(rect),
          chartSurface: chartSurface ? rectValue(chartSurface.getBoundingClientRect()) : null,
          svgCount: element.querySelectorAll("svg.recharts-surface").length,
        };
      });
    const responsiveGrids = [...document.querySelectorAll(
      ".analytics-card-grid, .daily-sales-kpis, .analytics-chart-grid",
    )]
      .filter(isVisible)
      .map((element) => ({
        className: typeof element.className === "string" ? element.className : null,
        rect: rectValue(element.getBoundingClientRect()),
        columns: window.getComputedStyle(element).gridTemplateColumns,
      }));

    return {
      windowInnerWidth: window.innerWidth,
      viewportHeight: window.innerHeight,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body?.scrollWidth ?? 0,
      header: rectValue(
        document.querySelector("[role='banner'], header")?.getBoundingClientRect(),
      ),
      main: rectValue(document.querySelector("main")?.getBoundingClientRect()),
      controls,
      touchAudit,
      pageLocalTouchOffenders,
      relevantRegions: regions,
      overflowingElements,
      chartRegions,
      responsiveGrids,
    };
  });

  return evaluateGeometry(documentMetrics, viewportWidth);
}

function safeFilePart(value) {
  return value.replace(/[^a-z0-9_-]+/gi, "-").replace(/^-|-$/g, "");
}

function markdownReport(report) {
  const rows = report.results.map((result) => {
    const overflow = result.geometry.rootOverflow ? "FAIL" : "PASS";
    const pageErrors = result.pageErrors.length > 0 ? `; page errors: ${result.pageErrors.length}` : "";
    return `| ${result.routeId} | ${result.theme} | ${result.viewportWidth} | ${overflow} | ${result.geometry.minVisibleControlFontPx ?? "n/a"} | ${result.screenshot}${pageErrors} |`;
  });
  const overflowCount = report.results.filter((result) => result.geometry.rootOverflow).length;
  const pageErrorCount = report.results.reduce((total, result) => total + result.pageErrors.length, 0);

  return `# Responsive browser baseline

- Date (UTC): ${report.generatedAtUtc}
- SHA/branch: ${report.gitSha ?? "not captured"} / ${report.branch ?? "not captured"}
- Browser: Chromium via Puppeteer
- Base URL: ${report.baseUrl}
- Mode: ${report.mode} (API responses fail closed except deterministic synthetic Color Sales, Dashboard, Daily Sales and Product Decision layout data; no customer metrics are loaded)
- Viewports: ${VIEWPORTS.join(", ")}
- Themes: ${THEMES.join(", ")}
- Root overflow observations: ${overflowCount} of ${report.results.length}
- Browser page errors: ${pageErrorCount}
- Intentional overflow self-test: ${report.selfTest.status} — ${report.selfTest.detail}
- Real iOS/iPad Safari evidence: pending; Chromium emulation is not device proof

The runner records observed geometry failures separately from source hypotheses. It does not change production UI or analytics semantics.

| Route | Theme | Width | Root overflow | Min visible control font (px) | Screenshot |
|---|---|---:|---|---:|---|
${rows.join("\n")}
`;
}

async function gitValue(command) {
  try {
    const child = (await import("node:child_process")).execFileSync("git", command, {
      cwd: path.resolve("."),
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    return child.trim();
  } catch {
    return null;
  }
}

async function run(options) {
  const generatedAtUtc = new Date().toISOString();
  await fs.mkdir(options.outputDir, { recursive: true });
  const browser = await puppeteer.launch({
    headless: true,
    protocolTimeout: 120_000,
    args: ["--no-sandbox", "--disable-setuid-sandbox"],
  });
  const results = [];

  try {
    const selectedRouteIds = options.routeIds.length > 0 ? options.routeIds : options.routeId ? [options.routeId] : null;
    const selectedRoutes = selectedRouteIds
      ? ROUTES.filter((route) => selectedRouteIds.includes(route.id))
      : ROUTES;
    const selectedThemes = options.theme
      ? THEMES.filter((theme) => theme === options.theme)
      : THEMES;
    for (const route of selectedRoutes) {
      for (const theme of selectedThemes) {
        for (const viewportWidth of (options.viewportWidth ? [options.viewportWidth] : VIEWPORTS)) {
          const page = await browser.newPage();
          const consoleErrors = [];
          const pageErrors = [];
          const requestFailures = [];
          page.on("console", (message) => {
            if (message.type() === "error") consoleErrors.push(message.text());
          });
          page.on("pageerror", (error) => pageErrors.push(String(error)));
          page.on("requestfailed", (request) => {
            if (request.url().includes("/api/")) {
              requestFailures.push({ url: request.url(), error: request.failure()?.errorText ?? "unknown" });
            }
          });

          const viewportHeight = route.pui40Shell
            ? viewportWidth === 1024 ? 768 : viewportWidth === 1280 ? 800 : 900
            : route.pui43Selector && viewportWidth === 1280 ? 800 : 900;
          await page.setViewport({
            width: viewportWidth,
            height: viewportHeight,
            deviceScaleFactor: 1,
            isMobile: viewportWidth < 768,
            hasTouch: options.touchProfile || Boolean(route.pui45Carousel && viewportWidth < 768),
          });
          const themeName = themeNameForBaseline(theme);
          await page.evaluateOnNewDocument((selectedTheme) => {
            localStorage.setItem("app-theme", selectedTheme);
          }, themeName);

          if (options.mode === "fixture") {
            await page.setRequestInterception(true);
            page.on("request", async (request) => {
              if (!request.url().includes("/api/") && !request.url().includes("/health") && !request.url().includes("/ready")) {
                request.continue();
                return;
              }
              if (route.pui45Spinner && viewportWidth === 360
                && new URL(request.url()).pathname === "/api/analytics/refresh-status") {
                await new Promise((resolve) => setTimeout(resolve, 500));
              }
              const response = await fixtureResponse(request, options);
              request.respond({
                status: response.status,
                contentType: "application/json",
                body: response.body,
              });
            });
          }

          const url = `${options.baseUrl.replace(/\/$/, "")}${route.path}`;
          let navigationError = null;
          let interactionStep = null;
          let interaction = null;
          let pui43DefaultLayout = null;
          let pui45Layout = null;
          try {
            await page.goto(url, { waitUntil: "domcontentloaded", timeout: options.timeoutMs });
            if (route.readySelector) {
              await page.waitForSelector(route.readySelector, { timeout: options.timeoutMs });
            }
            if (route.pui45Spinner && viewportWidth === 360) {
              await page.waitForSelector('[data-testid="global-request-spinner"]', { visible: true, timeout: options.timeoutMs });
              pui45Layout = await page.evaluate(() => {
                const spinnerElement = document.querySelector(".global-request-spinner");
                const spinner = spinnerElement?.getBoundingClientRect();
                const main = document.querySelector("main")?.getBoundingClientRect();
                const firstContentTop = main?.top ?? 0;
                const firstContentBottom = firstContentTop + Math.min(200, main?.height ?? 0);
                const intersectsFirstContent = Boolean(spinner && main
                  && spinner.left < main.right && spinner.right > main.left
                  && spinner.top < firstContentBottom && spinner.bottom > firstContentTop);
                return {
                  spinnerPosition: spinnerElement ? getComputedStyle(spinnerElement).position : null,
                  spinner: spinner ? { top: spinner.top, bottom: spinner.bottom } : null,
                  main: main ? { top: main.top, bottom: main.bottom } : null,
                  intersectsFirstContent,
                };
              });
              if (pui45Layout.intersectsFirstContent) {
                throw new Error(`P-UI-45 loading indicator intersects the first 200px of main content: ${JSON.stringify(pui45Layout)}`);
              }
            }
            if (route.pui45Carousel && viewportWidth === 360) {
              await page.waitForSelector('[data-testid="carousel-strip"]', { visible: true, timeout: options.timeoutMs });
              pui45Layout = await page.evaluate(() => {
                const buttons = [...document.querySelectorAll(".carousel-nav-btn")].map((button) => {
                  const rect = button.getBoundingClientRect();
                  return { width: rect.width, height: rect.height };
                });
                return { coarsePointer: matchMedia("(pointer: coarse)").matches, buttons };
              });
              if (pui45Layout.coarsePointer && pui45Layout.buttons.some((button) => button.width < 44 || button.height < 44)) {
                throw new Error(`P-UI-45 coarse-pointer carousel nav target is below 44px: ${JSON.stringify(pui45Layout)}`);
              }
            }
            if (route.pui39Overflow) {
              const expectedPrefix = PUI39_LONG_STORE_OPTION.slice(0, 36);
              await page.waitForFunction((expectedLabel) => [...document.querySelectorAll(".analytics-control-bar select")]
                .some((select) => [...select.options].some((option) => option.textContent?.trim().startsWith(expectedLabel))),
              { timeout: options.timeoutMs }, expectedPrefix);
            }
            if (route.pui43Selector) {
              await page.waitForSelector(route.pui43Selector, { timeout: options.timeoutMs, visible: true });
              pui43DefaultLayout = await page.evaluate((selector) => ({
                trustHeaderHeight: Math.round(document.querySelector(".analytics-trust-header")?.getBoundingClientRect().height ?? 0),
                firstContentTop: Math.round(((document.querySelector(selector)?.getBoundingClientRect().top ?? 0) + window.scrollY) * 100) / 100,
                viewportHeight: window.innerHeight,
              }), route.pui43Selector);
            }
            if (route.expandSelector) {
              await page.waitForFunction((selector) => {
                const button = document.querySelector(selector);
                return button instanceof HTMLButtonElement && !button.disabled;
              }, { timeout: options.timeoutMs }, route.expandSelector);
              await page.click(route.expandSelector);
              await page.waitForSelector(route.afterExpandSelector, { timeout: options.timeoutMs });
            }
            if (route.id === "prodaja") {
              await page.locator('input[placeholder="Broj racuna"]').fill("PUI30-001");
              await page.locator('input[placeholder="Pretrazi artikle po nazivu..."]').fill("patike");
              await page.waitForSelector(".mobile-entry-search-results button", { timeout: options.timeoutMs });
              await page.click(".mobile-entry-search-results button");
              await page.evaluate(() => {
                const inputs = [...document.querySelectorAll('.mobile-entry-form input[type="number"]')];
                const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set;
                for (const [index, value] of [[2, "2"], [3, "7290.5"]]) {
                  setValue.call(inputs[index], value);
                  inputs[index].dispatchEvent(new Event("input", { bubbles: true }));
                  inputs[index].dispatchEvent(new Event("change", { bubbles: true }));
                }
              });
              await page.evaluate(() => {
                const button = [...document.querySelectorAll(".mobile-entry-form button")]
                  .find((element) => element.textContent.trim() === "Sacuvaj prodaju");
                button?.click();
              });
              await page.waitForFunction(() => document.body.innerText.includes("Prodaja uspesna"), { timeout: options.timeoutMs });
              interaction = { salesSubmitted: true, boundary: "fixture POST /api/prodaja" };
            }
            if (route.id === "supplier") {
              interactionStep = "open-scorecard-filters";
              await page.locator("#supplier-tab-scorecard").click();
              await page.waitForFunction(() => document.querySelector("#supplier-tab-scorecard")?.getAttribute("aria-selected") === "true", { timeout: options.timeoutMs });
              if (viewportWidth <= 640) {
                await page.locator(".supplier-consolidated-filter-summary").click();
                await page.waitForFunction(() => document.querySelector(".supplier-consolidated-filters")?.hasAttribute("open"), { timeout: options.timeoutMs });
              }
              interactionStep = "measure-scorecard-filters";
              interaction = await page.evaluate(() => {
                const filters = document.querySelector(".supplier-consolidated-filters");
                const labels = [...(filters?.querySelectorAll(".supplier-consolidated-field > span:first-child") ?? [])]
                  .map((label) => label.textContent?.trim() ?? "");
                const controls = [...(filters?.querySelectorAll("select, input:not([type='checkbox']), .supplier-consolidated-actions button, .supplier-consolidated-check") ?? [])]
                  .filter((element) => element.getBoundingClientRect().width > 0)
                  .map((element) => ({
                    label: element.closest("label")?.textContent?.trim() ?? element.textContent?.trim() ?? "",
                    height: Math.round(element.getBoundingClientRect().height),
                  }));
                return {
                  disclosureOpen: filters?.hasAttribute("open") ?? false,
                  mobileSummaryAvailable: window.innerWidth > 640
                    || getComputedStyle(filters?.querySelector("summary") ?? document.body).display !== "none",
                  desktopStickyPreserved: window.innerWidth <= 900
                    || getComputedStyle(filters ?? document.body).position === "sticky",
                  scorecardFieldsReachable: ["Kategorija skorkarte", "Pol skorkarte", "Sezona skorkarte", "Minimalni prihod skorkarte"]
                    .every((label) => labels.some((value) => value.includes(label))),
                  confidenceAndStockOptionsReachable: Boolean(filters?.querySelector(".supplier-consolidated-check"))
                    && [...(filters?.querySelectorAll(".supplier-consolidated-check") ?? [])].length === 2,
                  phoneTouchTargetsMeet44px: window.innerWidth > 640 || controls.every((control) => control.height >= 44),
                  controls,
                };
              });
              if (!interaction.disclosureOpen
                || !interaction.mobileSummaryAvailable
                || !interaction.desktopStickyPreserved
                || !interaction.scorecardFieldsReachable
                || !interaction.confidenceAndStockOptionsReachable
                || !interaction.phoneTouchTargetsMeet44px) {
                throw new Error("supplier responsive filter disclosure, reachability, touch-target or sticky check failed");
              }
            }
            if (route.id === "actions") {
              interactionStep = "wait-for-action-row";
              await page.waitForSelector(".aaq-table tbody tr.aaq-row", { timeout: options.timeoutMs });
              interactionStep = "filter-actions";
              await page.locator('input[aria-label="Pretraži akcije"]').fill("Sintetička");
              await page.waitForSelector(".aaq-table tbody tr.aaq-row", { timeout: options.timeoutMs });
              interactionStep = "open-status-dialog";
              await page.locator(".aaq-table .td-actions .btn-defer").click();
              await page.waitForSelector('.modal-content[role="dialog"]', { timeout: options.timeoutMs });
              interactionStep = "measure-status-dialog";
              const statusDialog = await page.$eval(".modal-content", (dialog) => {
                const body = dialog.querySelector(".modal-body");
                const footer = dialog.querySelector(".modal-footer");
                const dialogRect = dialog.getBoundingClientRect();
                const footerRect = footer?.getBoundingClientRect();
                return {
                  height: Math.round(dialogRect.height),
                  withinViewport: dialogRect.top >= 0 && dialogRect.bottom <= window.innerHeight,
                  contentScrollable: Boolean(body && body.scrollHeight > body.clientHeight),
                  footerReachable: Boolean(footerRect && footerRect.bottom <= window.innerHeight),
                };
              });
              interactionStep = "confirm-status-update";
              await page.evaluate(() => {
                const button = [...document.querySelectorAll(".modal-content button")]
                  .find((element) => element.textContent.trim() === "Potvrdi");
                button?.click();
              });
              await page.waitForFunction(() => document.body.innerText.includes("Odloženo"), { timeout: options.timeoutMs });
              interactionStep = "open-outcome-dialog";
              await page.locator(".aaq-table .td-actions .btn-details").click();
              await page.waitForSelector('.modal-content[role="dialog"]', { timeout: options.timeoutMs });
              interactionStep = "measure-outcome-dialog";
              const outcomeDialog = await page.$eval(".modal-content", (dialog) => {
                const footer = dialog.querySelector(".modal-footer")?.getBoundingClientRect();
                return { withinViewport: dialog.getBoundingClientRect().top >= 0 && dialog.getBoundingClientRect().bottom <= window.innerHeight, footerReachable: Boolean(footer && footer.bottom <= window.innerHeight) };
              });
              interactionStep = "close-outcome-dialog";
              await page.keyboard.press("Escape");
              interaction = { filtered: true, statusUpdatedAtFixtureBoundary: true, statusDialog, outcomeDialog, keyboardClose: true };
            }
            if (route.id === "articles") {
              interactionStep = "wait-for-article-row";
              await page.waitForSelector('[data-testid="article-list-table"] tbody tr', { timeout: options.timeoutMs });
              interactionStep = "open-article-filters";
              await page.locator("button[aria-controls='article-list-filters']").click();
              await page.locator('input[aria-label="Filter po nazivu"]').fill("Sintetičke");
              await page.select('select[aria-label="Broj artikala po strani"]', "25");
              await page.waitForFunction(() => document.querySelector('[data-testid="article-list-table"] tbody tr')?.textContent?.includes("Sintetičke patike 1"), { timeout: options.timeoutMs });
              interactionStep = "advance-article-page";
              await page.locator('button[aria-label="Sledeća strana"]').click();
              await page.waitForFunction(() => document.querySelector('[data-testid="article-list-table"] tbody tr')?.textContent?.includes("Sintetičke patike 2"), { timeout: options.timeoutMs });
              interaction = await page.evaluate(() => {
                const table = document.querySelector('[data-testid="article-list-table"]');
                const region = table?.querySelector('[role="region"]');
                const controls = [...document.querySelectorAll(".article-list-pagination button, .article-list-pagination input, .article-list-pagination select, #article-list-filters input, #article-list-filters select")]
                  .filter((element) => element.getBoundingClientRect().width > 0)
                  .map((element) => ({
                    label: element.getAttribute("aria-label"),
                    height: Math.round(element.getBoundingClientRect().height),
                    fontSize: Number.parseFloat(getComputedStyle(element).fontSize),
                  }));
                return {
                  filtersOpened: document.querySelector("button[aria-controls='article-list-filters']")?.getAttribute("aria-expanded") === "true",
                  filterValue: document.querySelector('input[aria-label="Filter po nazivu"]')?.value,
                  pageValue: document.querySelector('input[aria-label="Broj strane"]')?.value,
                  tableRegionKeyboardFocusable: region?.getAttribute("tabindex") === "0",
                  allColumnsReachable: ["ID", "Naziv", "Prodajna cena", "Nabavna cena", "Količina", "Dobavljač", "Akcija"].every((label) => table?.textContent?.includes(label)),
                  controls,
                };
              });
            }
            await new Promise((resolve) => setTimeout(resolve, 250));
          } catch (error) {
            navigationError = `${interactionStep ?? "navigation"}: ${String(error)}`;
          }

          const geometry = await collectGeometry(page, viewportWidth);
          if (route.pui40Shell) assertPui40Shell(geometry);
          if (options.touchProfile && [768, 1024].includes(viewportWidth)) {
            assertPui42TouchTargets(geometry);
          }
          let pui43Layout = null;
          if (route.pui43Selector) {
            const trustLayout = pui43DefaultLayout ?? await page.evaluate((selector) => {
              const trustHeader = document.querySelector(".analytics-trust-header");
              const firstContent = document.querySelector(selector);
              return {
                trustHeaderHeight: trustHeader ? Math.round(trustHeader.getBoundingClientRect().height) : null,
                firstContentTop: firstContent ? Math.round((firstContent.getBoundingClientRect().top + window.scrollY) * 100) / 100 : null,
                viewportHeight: window.innerHeight,
              };
              }, route.pui43Selector);
            pui43Layout = trustLayout;
            if (viewportWidth === 360 && (!trustLayout.trustHeaderHeight || trustLayout.trustHeaderHeight > 260)) {
              throw new Error(`P-UI-43 trust header exceeds 260px at 360px on ${route.id}: ${trustLayout.trustHeaderHeight ?? "missing"}`);
            }
            if (viewportWidth === 1280 && (!trustLayout.trustHeaderHeight || trustLayout.trustHeaderHeight > 56)) {
              throw new Error(`P-UI-43 desktop trust strip exceeds 56px on ${route.id}: ${trustLayout.trustHeaderHeight ?? "missing"}`);
            }
            if (route.pui43MobileKpi && viewportWidth === 360 && (!trustLayout.firstContentTop || trustLayout.firstContentTop > trustLayout.viewportHeight * 1.5)) {
              throw new Error(`P-UI-43 first KPI exceeds 1.5 viewport heights on ${route.id}: ${trustLayout.firstContentTop ?? "missing"}`);
            }
            if (route.pui43TabletGrid && viewportWidth >= 640 && viewportWidth < 1024) {
              await page.click('[data-testid="analytics-trust-details-toggle"]');
              const columnCount = await page.$eval(".ath-full-details .ath-meta-grid", (grid) => getComputedStyle(grid).gridTemplateColumns.split(" ").length);
              if (columnCount !== 2) throw new Error(`P-UI-43 tablet trust details use ${columnCount} columns at ${viewportWidth}px on ${route.id}`);
            }
            if (route.pui43DesktopFold
              && viewportWidth === 1280
              && (!trustLayout.firstContentTop || trustLayout.firstContentTop >= trustLayout.viewportHeight)) {
              throw new Error(`P-UI-43 first decision region is below the desktop fold on ${route.id}: ${trustLayout.firstContentTop ?? "missing"}`);
            }
          }
          const printSmoke = route.printSmoke ? await (async () => {
            await page.emulateMediaType("print");
            return page.evaluate(() => {
              const wrapper = document.querySelector(".pilot-intake-durable-table-wrap");
              const table = wrapper?.querySelector("table");
              const methodology = document.querySelector(".pilot-methodology");
              const result = {
                overflowX: wrapper ? getComputedStyle(wrapper).overflowX : null,
                tableMinWidth: table ? getComputedStyle(table).minWidth : null,
                methodologyDisplay: methodology ? getComputedStyle(methodology).display : null,
              };
              return {
                ...result,
                status: wrapper && table && result.overflowX === "visible" && result.tableMinWidth === "0px"
                  && (!methodology || result.methodologyDisplay === "none") ? "PASS" : "FAIL",
              };
            });
          })() : null;
          const performance = route.id === "products" ? await page.evaluate(async (requestedFixtureRows) => {
            const table = document.querySelector(".product-decision-table");
            const wrapper = document.querySelector(".product-decision-table-wrap");
            const rowsBeforeSort = table?.querySelectorAll("tbody > tr.data-row").length ?? 0;
            const domNodeCount = document.querySelectorAll("*").length;
            const renderSummary = document.querySelector(".product-decision-render-summary")?.textContent ?? "";
            const renderCounts = renderSummary.match(/Prikazano\s+(\d+)\s+od\s+(\d+)\s+redova/);
            const sortHeader = table?.querySelector("thead th");
            let sortInteractionMs = null;
            if (sortHeader) {
              const startedAt = performance.now();
              sortHeader.dispatchEvent(new MouseEvent("click", { bubbles: true }));
              await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
              sortInteractionMs = Math.round((performance.now() - startedAt) * 100) / 100;
            }
            return {
              syntheticFixture: true,
              requestedFixtureRows,
              renderedRowsBeforeSort: rowsBeforeSort,
              renderedRowsAfterSort: table?.querySelectorAll("tbody > tr.data-row").length ?? 0,
              totalRowsAvailable: renderCounts ? Number(renderCounts[2]) : null,
              loadMoreAvailable: Boolean(document.querySelector(".product-decision-render-summary button")),
              domNodeCount,
              sortInteractionMs,
              tableScrollWidth: wrapper?.scrollWidth ?? null,
              tableClientWidth: wrapper?.clientWidth ?? null,
              firstRowReachable: Boolean(table?.querySelector("tbody > tr.data-row:first-child")),
              lastRowReachable: Boolean(table?.querySelector("tbody > tr.data-row:last-child")),
            };
          }, options.productRowCount) : null;
          const slug = `${safeFilePart(route.id)}__${theme}__${viewportWidth}`;
          const screenshotPath = path.join(options.outputDir, `${slug}.png`);
          if (route.captureSelector) {
            await page.$eval(route.captureSelector, (element) => element.scrollIntoView({ block: "center" }));
          }
          await page.screenshot({ path: screenshotPath, fullPage: !options.viewportOnly, timeout: options.timeoutMs });
          results.push({
            routeId: route.id,
            route: route.path,
            theme,
            viewportWidth,
            mode: options.mode,
            geometry,
            pui43Layout,
            pui45Layout,
            screenshot: screenshotPath,
            consoleErrors,
            pageErrors,
            requestFailures,
            navigationError,
            interactionStep,
            interaction,
            performance,
            printSmoke,
          });
          await page.close();
        }
      }
    }
  } finally {
    await browser.close();
  }

  const report = {
    generatedAtUtc,
    baseUrl: options.baseUrl,
    mode: options.mode,
    outputDir: options.outputDir,
    branch: await gitValue(["branch", "--show-current"]),
    gitSha: await gitValue(["rev-parse", "HEAD"]),
    selfTest: runSelfTest(),
    results,
  };
  const jsonPath = path.join(options.outputDir, "responsive-baseline.json");
  const markdownPath = path.join(options.outputDir, "responsive-baseline.md");
  await fs.writeFile(jsonPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
  await fs.writeFile(markdownPath, markdownReport(report), "utf8");

  if (options.strict) {
    const failing = results.filter((result) => result.geometry.rootOverflow
      || result.navigationError
      || (result.printSmoke && result.printSmoke.status !== "PASS")
      || result.pageErrors.length > 0
      || result.requestFailures.some((failure) => failure.error !== "net::ERR_ABORTED"));
    if (failing.length > 0) {
      throw new Error(`responsive baseline strict mode found ${failing.length} failing route observations`);
    }
  }

  process.stdout.write(JSON.stringify({
    status: "PASS",
    mode: options.mode,
    resultCount: results.length,
    rootOverflowObservations: results.filter((result) => result.geometry.rootOverflow).length,
    pageErrors: results.reduce((total, result) => total + result.pageErrors.length, 0),
    jsonPath,
    markdownPath,
    selfTest: report.selfTest,
  }, null, 2));
}

const options = parseArgs(process.argv.slice(2));
if (options.selfTest) {
  process.stdout.write(`${JSON.stringify(runSelfTest(), null, 2)}\n`);
} else {
  await run(options);
}
