#!/usr/bin/env node
/**
 * Regression check: every Microsoft.VisualStudio.JavaScript.Sdk pin in *.esproj
 * must exist on nuget.org. Prevents unavailable SDK versions from blocking
 * unrelated solution restore (BCI03).
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..");
const SDK_NAME = "Microsoft.VisualStudio.JavaScript.Sdk";
const INDEX_URL = `https://api.nuget.org/v3-flatcontainer/${SDK_NAME.toLowerCase()}/index.json`;
const PIN_RE = /Sdk\s*=\s*"Microsoft\.VisualStudio\.JavaScript\.Sdk\/([^"]+)"/g;

function walkEsproj(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (
      entry.name === "node_modules" ||
      entry.name === "dist" ||
      entry.name === "obj" ||
      entry.name === "bin" ||
      entry.name === ".git" ||
      entry.name === ".vs"
    ) {
      continue;
    }
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walkEsproj(full, out);
    } else if (entry.isFile() && entry.name.endsWith(".esproj")) {
      out.push(full);
    }
  }
  return out;
}

async function main() {
  const files = walkEsproj(repoRoot);
  if (files.length === 0) {
    console.error("FAIL: no .esproj files found.");
    process.exit(1);
  }

  const pins = [];
  for (const file of files) {
    const text = fs.readFileSync(file, "utf8");
    for (const match of text.matchAll(PIN_RE)) {
      pins.push({ file: path.relative(repoRoot, file).replaceAll("\\", "/"), version: match[1] });
    }
  }

  if (pins.length === 0) {
    console.error("FAIL: no Microsoft.VisualStudio.JavaScript.Sdk pins found in .esproj files.");
    process.exit(1);
  }

  const response = await fetch(INDEX_URL);
  if (!response.ok) {
    console.error(`FAIL: nuget.org index HTTP ${response.status} for ${SDK_NAME}`);
    process.exit(1);
  }

  const body = await response.json();
  const available = new Set(body.versions ?? []);
  let failed = false;

  for (const pin of pins) {
    if (!available.has(pin.version)) {
      failed = true;
      console.error(`FAIL: ${pin.file} pins ${SDK_NAME}/${pin.version} which is not on nuget.org`);
    } else {
      console.log(`OK: ${pin.file} -> ${SDK_NAME}/${pin.version}`);
    }
  }

  if (failed) {
    console.error("Pinned JavaScript SDK version is unavailable. Update the .esproj pin or remove the wrapper from the supported build path.");
    process.exit(1);
  }

  console.log(`Checked ${pins.length} pin(s) across ${files.length} .esproj file(s).`);
}

main().catch((error) => {
  console.error(`FAIL: ${error instanceof Error ? error.message : String(error)}`);
  process.exit(1);
});
