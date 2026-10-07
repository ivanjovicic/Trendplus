import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = fileURLToPath(new URL("..", import.meta.url));
const assetsDir = join(projectRoot, "dist", "assets");
const chartChunkLimitBytes = 560_000;
const unexpectedChunkLimitBytes = 500_000;

function findRechartsModulepreloads(html) {
  return [...html.matchAll(/<link\b[^>]*>/gi)]
    .map(([tag]) => {
      const rel = tag.match(/\brel=["']([^"']+)["']/i)?.[1].split(/\s+/) ?? [];
      const href = tag.match(/\bhref=["']([^"']+)["']/i)?.[1] ?? "";
      return rel.includes("modulepreload") && /(?:^|\/)recharts-[^/]+\.js(?:$|[?#])/.test(href) ? href : null;
    })
    .filter(Boolean);
}

function runSelfTest() {
  const recurrence = '<link rel="modulepreload" crossorigin href="/assets/recharts-a1b2.js">';
  const routeIsolated = '<link rel="modulepreload" crossorigin href="/assets/vendor-react-a1b2.js">';
  if (findRechartsModulepreloads(recurrence).length !== 1) {
    throw new Error("seeded Recharts modulepreload recurrence was not detected");
  }
  if (findRechartsModulepreloads(routeIsolated).length !== 0) {
    throw new Error("non-Recharts modulepreload was incorrectly treated as a regression");
  }
  console.log("Bundle budget self-test: PASS (seeded Recharts preload recurrence detected)");
}

if (process.argv.includes("--self-test")) {
  runSelfTest();
  process.exit(0);
}

let assetNames;
try {
  assetNames = readdirSync(assetsDir);
} catch {
  console.error("Bundle budget check: dist/assets is missing; run npm run build first.");
  process.exit(1);
}

let indexHtml;
try {
  indexHtml = readFileSync(join(projectRoot, "dist", "index.html"), "utf8");
} catch {
  console.error("Bundle budget check: dist/index.html is missing; run npm run build first.");
  process.exit(1);
}

const javascriptAssets = assetNames
  .filter((name) => name.endsWith(".js"))
  .map((name) => ({ name, bytes: statSync(join(assetsDir, name)).size }))
  .sort((left, right) => right.bytes - left.bytes);

if (javascriptAssets.length === 0) {
  console.error("Bundle budget check: no JavaScript assets found in dist/assets.");
  process.exit(1);
}

const chartChunk = javascriptAssets.find((asset) => asset.name.startsWith("recharts-"));
const failures = [];

for (const preload of findRechartsModulepreloads(indexHtml)) {
  failures.push(`${preload} is modulepreloaded by the application entry; chart code must remain route-lazy`);
}

if (!chartChunk) {
  failures.push("expected recharts-*.js shared chunk is missing");
} else if (chartChunk.bytes > chartChunkLimitBytes) {
  failures.push(
    `${chartChunk.name} is ${chartChunk.bytes} bytes; the measured Recharts exception is ${chartChunkLimitBytes} bytes`,
  );
}

for (const asset of javascriptAssets) {
  if (asset.name.startsWith("recharts-") || asset.bytes <= unexpectedChunkLimitBytes) {
    continue;
  }

  failures.push(`${asset.name} is ${asset.bytes} bytes; unexpected chunks must stay at or below ${unexpectedChunkLimitBytes} bytes`);
}

console.log("Bundle budget baseline:");
for (const asset of javascriptAssets.slice(0, 5)) {
  console.log(`- ${asset.name}: ${asset.bytes} bytes`);
}

if (failures.length > 0) {
  console.error("Bundle budget check: FAIL");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }
  process.exit(1);
}

console.log("Bundle budget check: PASS");
