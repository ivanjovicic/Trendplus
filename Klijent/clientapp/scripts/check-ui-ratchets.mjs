#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

const base = process.cwd();
const srcDir = path.join(base, "src");
const tailwindPath = path.join(srcDir, "tailwind.css");
const excludedProductionPaths = new Set(["pages/ProductDecisionCenterPage.tsx", "pages/ProductDecisionCenterPage.css"]);
const auditedFixedColorPaths = [
  "styles/interactionTokens.ts",
  "components/inventory/SKUDetailModal.tsx",
  "layout/components/Sidebar.tsx",
  "layout/components/HeaderStatus.tsx",
];
const pseudoFallbackAuditPaths = [
  "styles/themes.css",
  "styles/interactionTokens.ts",
];

const semanticClassPattern =
  /(?:^|[\s"'`:])(?:bg|text|border|ring|divide|from|to|via|fill|stroke|outline)-((?:border|foreground|muted|primary|on-primary|success|warning|error|danger|info|accent)(?:-(?:hover|success|warning|error))?)(?:\/\d+)?(?=[\s"'`]|$)/gm;
const fixedColorPattern =
  /(?:^|[\s"'`:])((?:bg|text|border)-(?:white|black)(?:\/\d+)?)(?=[\s"'`]|$)/gm;
const nestedPseudoFallbackPattern =
  /var\(\s*--theme-color-[\w-]+\s*,\s*var\(\s*--theme-color-/g;

const fixedColorAllowlist = new Map([
  ["components/inventory/SKUDetailModal.tsx::text-white", "white text sits on an explicit stock-state gradient panel"],
  ["components/inventory/SKUDetailModal.tsx::text-white/70", "white text sits on an explicit stock-state gradient panel"],
  ["components/inventory/SKUDetailModal.tsx::text-white/75", "white text sits on an explicit stock-state gradient panel"],
  ["components/inventory/SKUDetailModal.tsx::text-white/80", "white text sits on an explicit stock-state gradient panel"],
  ["components/inventory/SKUDetailModal.tsx::border-white/15", "white border is an overlay on an explicit stock-state gradient panel"],
  ["components/inventory/SKUDetailModal.tsx::bg-white/10", "white fill is an overlay on an explicit stock-state gradient panel"],
  ["layout/components/Sidebar.tsx::bg-black/70", "fixed black scrim is the drawer backdrop"],
  ["layout/components/HeaderStatus.tsx::bg-black/60", "fixed black scrim is the modal backdrop"],
  ["layout/components/HeaderStatus.tsx::bg-black/50", "fixed black scrim is the mobile drawer backdrop"],
]);

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) return walk(fullPath);
    return /\.(ts|tsx|css)$/.test(entry.name) ? [fullPath] : [];
  });
}

function relativeSourcePath(filePath) {
  return path.relative(srcDir, filePath).split(path.sep).join("/");
}

function productionFiles() {
  return walk(srcDir).filter((filePath) => {
    const relativePath = relativeSourcePath(filePath);
    return !/\.(spec|test)\.(ts|tsx)$/.test(relativePath)
      && !relativePath.includes("/__tests__/")
      && !excludedProductionPaths.has(relativePath);
  });
}

function mappedSemanticColors(tailwindCss) {
  const themeBlock = tailwindCss.match(/@theme inline\s*\{([^}]*)\}/s)?.[1] ?? "";
  return new Set([...themeBlock.matchAll(/--color-([\w-]+)\s*:/g)].map((match) => match[1]));
}

function semanticMappingProblems(tailwindCss, sources) {
  const mapped = mappedSemanticColors(tailwindCss);
  const missing = new Set();
  for (const source of sources) {
    for (const match of source.matchAll(semanticClassPattern)) {
      if (!mapped.has(match[1])) missing.add(match[1]);
    }
  }
  return [...missing].sort();
}

function fixedColorFindings(relativePath, source) {
  if (!auditedFixedColorPaths.includes(relativePath)) return [];
  return [...source.matchAll(fixedColorPattern)].map((match) => {
    const token = match[1];
    const allowlistKey = `${relativePath}::${token}`;
    return {
      token,
      allowlisted: fixedColorAllowlist.has(allowlistKey),
      reason: fixedColorAllowlist.get(allowlistKey),
    };
  }).filter((finding) => !finding.allowlisted);
}

function nestedFallbackFindings(relativePath, source) {
  if (!pseudoFallbackAuditPaths.includes(relativePath)) return [];
  return [...source.matchAll(nestedPseudoFallbackPattern)].map((match) => ({
    file: relativePath,
    index: match.index,
    value: match[0],
  }));
}

function runSelfTest() {
  const fixtureTailwind = "@theme inline { --color-success: var(--success); }";
  if (semanticMappingProblems(fixtureTailwind, ["text-success bg-success/20"]).length !== 0) {
    throw new Error("mapped semantic colour fixture should pass");
  }
  if (semanticMappingProblems(fixtureTailwind, ["text-missing"]).length !== 1) {
    throw new Error("missing semantic colour fixture should fail");
  }
  if (fixedColorFindings("styles/interactionTokens.ts", "className=\"text-white\"").length !== 1) {
    throw new Error("unsafe fixed-colour fixture should fail");
  }
  if (fixedColorFindings("layout/components/Sidebar.tsx", "className=\"bg-black/70\"").length !== 0) {
    throw new Error("documented fixed scrim fixture should be allowlisted");
  }
  if (nestedFallbackFindings("styles/themes.css", "var(--theme-color-a, var(--theme-color-b, #fff))").length !== 1) {
    throw new Error("nested pseudo-token fixture should fail");
  }
  console.log("ui ratchet self-test: PASS");
}

function check() {
  const files = productionFiles();
  const sources = files.map((filePath) => fs.readFileSync(filePath, "utf8"));
  const tailwindCss = fs.readFileSync(tailwindPath, "utf8");
  const problems = [];

  if (!tailwindCss.includes("@theme inline")) {
    problems.push("tailwind.css is missing the canonical @theme inline mapping block");
  }
  if (!fs.readFileSync(path.join(srcDir, "styles", "themes.css"), "utf8").includes('[data-theme="inventory-dark"]')) {
    problems.push("inventory-dark is not represented as an explicit supported theme selector");
  }

  for (const missing of semanticMappingProblems(tailwindCss, sources)) {
    problems.push(`semantic Tailwind colour has no @theme mapping: ${missing}`);
  }

  for (const filePath of files) {
    const relativePath = relativeSourcePath(filePath);
    const source = fs.readFileSync(filePath, "utf8");
    for (const finding of fixedColorFindings(relativePath, source)) {
      problems.push(`${relativePath}: unsafe fixed colour utility ${finding.token}`);
    }
    for (const finding of nestedFallbackFindings(relativePath, source)) {
      problems.push(`${finding.file}: nested pseudo-token fallback at offset ${finding.index}`);
    }
  }

  const pseudoFallbackCounts = Object.fromEntries(pseudoFallbackAuditPaths.map((relativePath) => {
    const source = fs.readFileSync(path.join(srcDir, relativePath), "utf8");
    return [relativePath, [...source.matchAll(/var\(\s*--theme-color-[\w-]+\s*,/g)].length];
  }));

  if (problems.length > 0) {
    for (const problem of problems) console.error(`FAIL: ${problem}`);
    process.exitCode = 2;
    return;
  }

  console.log(`ui ratchets: PASS (${files.length} production files; ${fixedColorAllowlist.size} documented fixed-colour exceptions)`);
  console.log(`pseudo-token fallback inventory: ${JSON.stringify(pseudoFallbackCounts)}`);
}

if (process.argv.includes("--self-test")) runSelfTest();
else check();
