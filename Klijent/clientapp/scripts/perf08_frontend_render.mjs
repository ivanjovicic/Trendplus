import puppeteer from "puppeteer";

const baseUrl = process.argv[2] ?? "http://127.0.0.1:5174";
const route = process.argv[3] ?? "/analytics";
const timeoutMs = Number(process.argv[4] ?? 120000);

const url = `${baseUrl.replace(/\/$/, "")}${route.startsWith("/") ? route : `/${route}`}`;

const startedAt = Date.now();
const browser = await puppeteer.launch({
  headless: true,
  args: ["--no-sandbox", "--disable-setuid-sandbox"],
});
try {
  const page = await browser.newPage();
  const navigationStartedAt = Date.now();

  let bootstrapStatus = null;
  let bootstrapMs = null;

  const bootstrapPromise = page
    .waitForResponse(
      (response) =>
        response.url().includes("/api/analytics/cached/dashboard/bootstrap") &&
        response.status() === 200,
      { timeout: timeoutMs },
    )
    .then((response) => {
      bootstrapStatus = response.status();
      bootstrapMs = Date.now() - navigationStartedAt;
      return response;
    })
    .catch(() => null);

  await page.goto(url, { waitUntil: "domcontentloaded", timeout: timeoutMs });
  await bootstrapPromise;

  const domContentLoadedMs = Date.now() - navigationStartedAt;

  let usefulRenderMs = null;
  let renderTimedOut = false;
  try {
    await page.waitForFunction(
      () => {
        const text = document.body?.innerText ?? "";
        const loading = /Učitavanje|Ucitavanje/i.test(text);
        const hasSignal =
          /RSD|Ukupna promet|Promet|Dashboard/i.test(text) ||
          /Greška|Greska|nije dostupan|empty/i.test(text);
        return hasSignal && !loading;
      },
      { timeout: Math.max(5000, timeoutMs - domContentLoadedMs) },
    );
    usefulRenderMs = Date.now() - navigationStartedAt;
  } catch {
    renderTimedOut = true;
    usefulRenderMs = Date.now() - navigationStartedAt;
  }

  const payload = {
    url,
    domContentLoadedMs,
    bootstrapMs,
    usefulRenderMs,
    totalMs: Date.now() - startedAt,
    bootstrapStatus,
    renderTimedOut,
    processState: "cold-browser",
    cacheState: "cold-vite-dev-warm-api",
    correctnessChecks:
      "bootstrap 200 preferred; useful render = dom signal without loading copy or explicit error state",
  };
  process.stdout.write(JSON.stringify(payload));
} finally {
  await browser.close();
}
