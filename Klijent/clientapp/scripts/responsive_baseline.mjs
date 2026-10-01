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
    strict: false,
    selfTest: false,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === "--base-url") options.baseUrl = argv[++index];
    else if (argument === "--output-dir") options.outputDir = path.resolve(argv[++index]);
    else if (argument === "--mode") options.mode = argv[++index];
    else if (argument === "--timeout-ms") options.timeoutMs = Number(argv[++index]);
    else if (argument === "--strict") options.strict = true;
    else if (argument === "--self-test") options.selfTest = true;
    else throw new Error(`Unknown argument: ${argument}`);
  }

  if (!["fixture", "connected-local"].includes(options.mode)) {
    throw new Error(`Unsupported mode: ${options.mode}`);
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
      "table, [role='dialog'], [role='banner'], [data-testid*='data-table'], [class*='filter'], [class*='toolbar']",
    )]
      .filter(isVisible)
      .slice(0, 40)
      .map((element) => ({
        tag: element.tagName.toLowerCase(),
        testId: element.getAttribute("data-testid"),
        className: typeof element.className === "string" ? element.className.slice(0, 120) : null,
        rect: rectValue(element.getBoundingClientRect()),
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
- Mode: ${report.mode} (fixture responses intentionally fail closed; no customer metrics are loaded)
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
    for (const route of ROUTES) {
      for (const theme of THEMES) {
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
            await new Promise((resolve) => setTimeout(resolve, 250));
          } catch (error) {
            navigationError = String(error);
          }

          const geometry = await collectGeometry(page, viewportWidth);
          const slug = `${safeFilePart(route.id)}__${theme}__${viewportWidth}`;
          const screenshotPath = path.join(options.outputDir, `${slug}.png`);
          await page.screenshot({ path: screenshotPath, fullPage: true, timeout: options.timeoutMs });
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
