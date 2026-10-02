import fs from "node:fs/promises";
import path from "node:path";
import process from "node:process";
import puppeteer from "puppeteer";

export const VIEWPORTS = [320, 375, 768, 1024, 1280];

const ROUTES = [
  { id: "app_shell", path: "/analytics" },
  { id: "prodaja", path: "/prodaja" },
  { id: "analytics", path: "/analytics" },
  { id: "supplier", path: "/analytics/supplier" },
  { id: "inventory", path: "/analytics/inventory", readySelector: '[data-testid="analytics-control-bar"]' },
  { id: "color_sales", path: "/analytics/color-sales-stats", readySelector: '[data-testid="analytics-data-table"]', captureSelector: '[data-testid="analytics-data-table"]' },
  { id: "products", path: "/analytics/products" },
  { id: "actions", path: "/analytics/actions" },
  { id: "nivelacija_pre_post", path: "/analytics/nivelacije-pre-post" },
];

const THEMES = ["light", "dark"];
const DEFAULT_BASE_URL = "http://127.0.0.1:5174";
const DEFAULT_OUTPUT_DIR = path.resolve("tmp/ui-visual/responsive-baseline");
const DEFAULT_TIMEOUT_MS = 20_000;

function parseArgs(argv) {
  const options = {
    baseUrl: DEFAULT_BASE_URL,
    outputDir: DEFAULT_OUTPUT_DIR,
    mode: "fixture",
    timeoutMs: DEFAULT_TIMEOUT_MS,
    routeId: null,
    viewportOnly: false,
    theme: null,
    strict: false,
    selfTest: false,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--base-url") options.baseUrl = argv[++index];
    else if (argument === "--output-dir") options.outputDir = path.resolve(argv[++index]);
    else if (argument === "--mode") options.mode = argv[++index];
    else if (argument === "--timeout-ms") options.timeoutMs = Number(argv[++index]);
    else if (argument === "--route-id") options.routeId = argv[++index];
    else if (argument === "--viewport-only") options.viewportOnly = true;
    else if (argument === "--theme") options.theme = argv[++index];
    else if (argument === "--strict") options.strict = true;
    else if (argument === "--self-test") options.selfTest = true;
    else throw new Error(`Unknown argument: ${argument}`);
  }

  if (!["fixture", "connected-local"].includes(options.mode)) {
    throw new Error(`Unsupported mode: ${options.mode}`);
  }
  if (options.routeId && !ROUTES.some((route) => route.id === options.routeId)) {
    throw new Error(`Unknown responsive baseline route id: ${options.routeId}`);
  }
  if (options.theme && !THEMES.includes(options.theme)) {
    throw new Error(`Unknown responsive baseline theme: ${options.theme}`);
  }

  return options;
}

export function assertNoRootOverflow(metrics) {
  const overflowing = metrics.scrollWidth > metrics.viewportWidth + 1
    || metrics.bodyScrollWidth > metrics.viewportWidth + 1;
  if (overflowing) {
    throw new Error(
      `root horizontal overflow: viewport=${metrics.viewportWidth}, document=${metrics.scrollWidth}, body=${metrics.bodyScrollWidth}`,
    );
  }
  return true;
}

export function evaluateGeometry(documentMetrics, viewportWidth) {
  return {
    viewportWidth,
    viewportHeight: documentMetrics.viewportHeight,
    scrollWidth: documentMetrics.scrollWidth,
    bodyScrollWidth: documentMetrics.bodyScrollWidth,
    rootOverflow: documentMetrics.scrollWidth > viewportWidth + 1
      || documentMetrics.bodyScrollWidth > viewportWidth + 1,
    header: documentMetrics.header,
    visibleControlCount: documentMetrics.controls.length,
    minVisibleControlFontPx: documentMetrics.controls.length > 0
      ? Math.min(...documentMetrics.controls.map((control) => control.fontSizePx))
      : null,
    controls: documentMetrics.controls,
    relevantRegions: documentMetrics.relevantRegions,
  };
}

function runSelfTest() {
  const intentionalOverflow = {
    viewportWidth: 320,
    viewportHeight: 900,
    scrollWidth: 321,
    bodyScrollWidth: 480,
    header: null,
    controls: [],
    relevantRegions: [],
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

  return {
    name: "intentional-overflow-fixture",
    status: "PASS",
    detail: "geometry assertion failed as expected and was caught by the self-test",
  };
}

async function fixtureResponse(request) {
  const url = new URL(request.url());
  if (url.pathname === "/health" || url.pathname === "/ready") {
    return { status: 200, body: JSON.stringify({ status: "fixture" }) };
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

    const regions = [...document.querySelectorAll(
      "[data-testid='analytics-control-bar'], [class~='analytics-data-table__scroll'], [class*='control-bar'], table, [role='dialog'], [role='banner'], [data-testid*='data-table'], [class*='filter'], [class*='toolbar']",
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

    return {
      viewportHeight: window.innerHeight,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body?.scrollWidth ?? 0,
      header: rectValue(
        document.querySelector("[role='banner'], header")?.getBoundingClientRect(),
      ),
      controls,
      relevantRegions: regions,
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
- Mode: ${report.mode} (API responses fail closed except deterministic synthetic Color Sales layout data; no customer metrics are loaded)
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
    const selectedRoutes = options.routeId
      ? ROUTES.filter((route) => route.id === options.routeId)
      : ROUTES;
    const selectedThemes = options.theme
      ? THEMES.filter((theme) => theme === options.theme)
      : THEMES;
    for (const route of selectedRoutes) {
      for (const theme of selectedThemes) {
        for (const viewportWidth of VIEWPORTS) {
          const page = await browser.newPage();
          const consoleErrors = [];
          const pageErrors = [];
          const requestFailures = [];
          page.on("console", (message) => {
            if (message.type() === "error") consoleErrors.push(message.text());
          });
          page.on("pageerror", (error) => pageErrors.push(String(error)));
          page.on("requestfailed", (request) => {
            if (request.url().includes("/api/")) requestFailures.push(request.url());
          });

          await page.setViewport({
            width: viewportWidth,
            height: 900,
            deviceScaleFactor: 1,
            isMobile: viewportWidth < 768,
          });
          await page.evaluateOnNewDocument((selectedTheme) => {
            localStorage.setItem("app-theme", selectedTheme === "dark" ? "neon-dark" : "light");
          }, theme);

          if (options.mode === "fixture") {
            await page.setRequestInterception(true);
            page.on("request", async (request) => {
              if (!request.url().includes("/api/") && !request.url().includes("/health") && !request.url().includes("/ready")) {
                request.continue();
                return;
              }
              const response = await fixtureResponse(request);
              request.respond({
                status: response.status,
                contentType: "application/json",
                body: response.body,
              });
            });
          }

          const url = `${options.baseUrl.replace(/\/$/, "")}${route.path}`;
          let navigationError = null;
          try {
            await page.goto(url, { waitUntil: "domcontentloaded", timeout: options.timeoutMs });
            if (route.readySelector) {
              await page.waitForSelector(route.readySelector, { timeout: options.timeoutMs });
            }
            await new Promise((resolve) => setTimeout(resolve, 250));
          } catch (error) {
            navigationError = String(error);
          }

          const geometry = await collectGeometry(page, viewportWidth);
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
            screenshot: screenshotPath,
            consoleErrors,
            pageErrors,
            requestFailures,
            navigationError,
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
    const failing = results.filter((result) => result.geometry.rootOverflow);
    if (failing.length > 0) {
      throw new Error(`responsive baseline strict mode found ${failing.length} root-overflow observations`);
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
