import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = fileURLToPath(new URL("..", import.meta.url));
const assetsDir = join(projectRoot, "dist", "assets");
const chartChunkLimitBytes = 560_000;
const unexpectedChunkLimitBytes = 500_000;

let assetNames;
try {
  assetNames = readdirSync(assetsDir);
} catch {
  console.error("Bundle budget check: dist/assets is missing; run npm run build first.");
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
