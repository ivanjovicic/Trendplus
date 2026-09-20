#!/usr/bin/env node
/*
Analytics guardrail scanner with an explicit, non-growing debt baseline.

Usage:
  node ./scripts/check-analytics-guardrails.mjs
  node ./scripts/check-analytics-guardrails.mjs --self-test

The scanner exits successfully when current findings are a subset of the
reviewed baseline. New findings fail the command; removed baseline findings
are reported so the baseline can shrink through a reviewed diff.
*/

import fs from "fs/promises";
import path from "path";

const base = process.cwd(); // expected to run from Klijent/clientapp
const baselinePath = path.join(base, "scripts", "known-guardrail-baseline.json");
const targets = ["src/pages", "src/components", "src/services", "src/utils"];
const allowedRelative = [
  "src/utils/analyticsFormatters.ts",
  "src/utils/analyticsConstants.ts",
  "src/utils/analyticsMetricDescriptions.ts",
];

const rules = [
  { name: "BOOST_SCORE_THRESHOLD", re: /\bconst\s+BOOST_SCORE_THRESHOLD\b/, scopes: ["pages", "components"] },
  { name: "KEEP_SCORE_THRESHOLD", re: /\bconst\s+KEEP_SCORE_THRESHOLD\b/, scopes: ["pages", "components"] },
  { name: "fmtRsd", re: /\bfunction\s+fmtRsd\b|\bconst\s+fmtRsd\b/, scopes: ["pages", "components"] },
  { name: "fmtPct", re: /\bfunction\s+fmtPct\b|\bconst\s+fmtPct\b/, scopes: ["pages", "components"] },
  { name: "formatCurrency", re: /\bfunction\s+formatCurrency\b|\bconst\s+formatCurrency\b|\bexport\s+function\s+formatCurrency\b/, scopes: ["pages"] },
  { name: "formatPercent", re: /\bfunction\s+formatPercent\b|\bconst\s+formatPercent\b|\bexport\s+function\s+formatPercent\b/, scopes: ["pages"] },
  { name: "decisionScore_assign", re: /\bdecisionScore\b\s*=/, scopes: ["pages", "components"] },
  { name: "qualityIndex_mul", re: /qualityIndex\s*\*/, scopes: ["pages", "components"] },
  { name: "score_mul_zero", re: /score\s*\*\s*0\./, scopes: ["pages", "components", "services", "utils"] },
  { name: "marginCoverage_mul_zero", re: /marginCoveragePct\s*\*\s*0\./, scopes: ["pages", "components"] },
  { name: "trendNorm", re: /\btrendNorm\b/, scopes: ["pages", "components"] },
  { name: "shareNorm", re: /\bshareNorm\b/, scopes: ["pages", "components"] },
  { name: "confidencePct_assign", re: /\bconfidencePct\b\s*=/, scopes: ["pages", "components"] },
  { name: "reliabilityPct_assign", re: /\breliabilityPct\b\s*=/, scopes: ["pages", "components"] },
  { name: "recommendationStatus_assign", re: /\brecommendationStatus\b\s*=/, scopes: ["pages", "components"] },
];

function isTestFile(rel) {
  return /\.test\.|\.spec\.|\/__tests__\//.test(rel);
}

async function walk(dir) {
  const entries = await fs.readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) files.push(...(await walk(full)));
    else files.push(full);
  }
  return files;
}

function relPath(file) {
  return path.relative(base, file).replaceAll("\\", "/");
}

function violationKey(violation) {
  return `${violation.file}::${violation.rule}::${violation.line}`;
}

function validateBaseline(baseline) {
  if (!baseline || baseline.version !== 1 || !Array.isArray(baseline.entries)) {
    throw new Error("Invalid guardrail baseline: expected version 1 with entries.");
  }
  if (!Number.isInteger(baseline.maxEntries) || baseline.maxEntries < baseline.entries.length) {
    throw new Error("Invalid guardrail baseline: entries exceed maxEntries.");
  }

  const keys = new Set();
  for (const entry of baseline.entries) {
    if (
      typeof entry?.file !== "string"
      || typeof entry?.rule !== "string"
      || !Number.isInteger(entry?.line)
      || typeof entry?.reason !== "string"
      || entry.reason.trim().length === 0
    ) {
      throw new Error("Invalid guardrail baseline entry: file, rule, line and reviewed reason are required.");
    }
    if (/[*?[\]{}]/.test(entry.file) || /[*?[\]{}]/.test(entry.rule)) {
      throw new Error("Wildcard guardrail baseline entries are forbidden.");
    }
    const key = violationKey(entry);
    if (keys.has(key)) throw new Error(`Duplicate guardrail baseline entry: ${key}`);
    keys.add(key);
  }
}

async function loadBaseline() {
  const content = await fs.readFile(baselinePath, "utf8");
  const baseline = JSON.parse(content);
  validateBaseline(baseline);
  return baseline;
}

async function scanViolations() {
  const violations = [];
  for (const target of targets) {
    const dir = path.join(base, target);
    let allFiles;
    try {
      allFiles = await walk(dir);
    } catch {
      continue;
    }

    for (const file of allFiles) {
      if (!/\.(ts|tsx|js|jsx)$/.test(file)) continue;
      const rel = relPath(file);
      if (isTestFile(rel) || allowedRelative.includes(rel)) continue;
      const content = await fs.readFile(file, "utf8");
      const category = rel.startsWith("src/pages/")
        ? "pages"
        : rel.startsWith("src/components/")
          ? "components"
          : rel.startsWith("src/services/")
            ? "services"
            : "utils";
      const lines = content.split(/\r?\n/);

      for (const rule of rules) {
        if (!rule.scopes.includes(category)) continue;
        const lineIndex = lines.findIndex((line) => rule.re.test(line));
        if (lineIndex < 0) continue;
        violations.push({ file: rel, rule: rule.name, line: lineIndex + 1 });
      }
    }
  }
  return violations;
}

function compareViolations(current, baseline) {
  const currentByKey = new Map(current.map((violation) => [violationKey(violation), violation]));
  const baselineByKey = new Map(baseline.entries.map((violation) => [violationKey(violation), violation]));
  return {
    added: current.filter((violation) => !baselineByKey.has(violationKey(violation))),
    removed: baseline.entries.filter((violation) => !currentByKey.has(violationKey(violation))),
    unchanged: current.filter((violation) => baselineByKey.has(violationKey(violation))),
  };
}

function guardrailExitCode(comparison) {
  return comparison.added.length > 0 ? 2 : 0;
}

function printViolation(prefix, violation) {
  console.error(`${prefix}: ${violation.file}:${violation.line} -> ${violation.rule}`);
}

function assert(condition, message) {
  if (!condition) throw new Error(`Self-test failed: ${message}`);
}

function runSelfTest() {
  const baseline = {
    version: 1,
    maxEntries: 2,
    entries: [
      { file: "src/pages/a.tsx", rule: "rule_a", line: 10, reason: "reviewed" },
      { file: "src/pages/b.tsx", rule: "rule_b", line: 20, reason: "reviewed" },
    ],
  };
  validateBaseline(baseline);

  const unchanged = compareViolations(baseline.entries, baseline);
  assert(unchanged.added.length === 0 && unchanged.removed.length === 0, "unchanged baseline should pass");
  assert(guardrailExitCode(unchanged) === 0, "unchanged baseline should exit successfully");

  const removed = compareViolations([baseline.entries[0]], baseline);
  assert(removed.added.length === 0 && removed.removed.length === 1, "removed debt should be reported");

  const newViolation = { file: "src/pages/c.tsx", rule: "rule_c", line: 30 };
  const grown = compareViolations([...baseline.entries, newViolation], baseline);
  assert(grown.added.length === 1, "new debt should fail");
  assert(guardrailExitCode(grown) === 2, "new debt should use the failing exit code");

  let wildcardRejected = false;
  try {
    validateBaseline({ ...baseline, entries: [{ ...baseline.entries[0], file: "src/pages/*.tsx" }] });
  } catch {
    wildcardRejected = true;
  }
  assert(wildcardRejected, "wildcard suppression should be rejected");
  assert(baseline.entries.length <= baseline.maxEntries, "baseline monotonicity must cap growth");
  console.log("OK: Guardrail baseline self-test passed.");
}

async function main() {
  if (process.argv.includes("--self-test")) {
    runSelfTest();
    return;
  }

  const baseline = await loadBaseline();
  const current = await scanViolations();
  const comparison = compareViolations(current, baseline);

  for (const violation of comparison.added) printViolation("NEW VIOLATION", violation);
  for (const violation of comparison.removed) printViolation("BASELINE REMOVED", violation);

  if (guardrailExitCode(comparison) !== 0) {
    console.error(`\nFAIL: ${comparison.added.length} new guardrail violation(s); ${comparison.unchanged.length} remain in reviewed baseline.`);
    process.exitCode = 2;
    return;
  }

  if (current.length > 0) {
    console.log(`OK: Guardrails baseline-only (${comparison.unchanged.length} known violation(s), ${comparison.removed.length} removed).`);
  } else {
    console.log(`OK: Guardrails pass (baseline debt cleared; ${comparison.removed.length} baseline violation(s) removed).`);
  }
}

main().catch((error) => {
  console.error(`Guardrail baseline error: ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 2;
});
