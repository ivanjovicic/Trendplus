#!/usr/bin/env node
/**
 * Guard the current-truth section of the analytics execution plan.
 * Historical audit text is allowed, but it must not be the only contract.
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const PLAN = "docs/ANALYTICS_EXECUTION_PLAN.md";
const REQUIRED_REFERENCES = [
  "Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts",
  "Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx",
  "Klijent/clientapp/src/pages/SupplierRedirects.tsx",
  "Api/Services/SalesDataScopePolicy.cs",
  "Application/Analytics/AnalyticsMarginPolicy.cs",
  "Api/Endpoints/SupplierDecisionHubEndpoints.cs",
];
const REQUIRED_MARKERS = [
  "## 0. Current Contract",
  "Verified against main",
  "historical",
  "/analytics/supplier",
  "ProdajaZaglavlje.DataOrigin",
  "BatchId + ProdajaStavkaId",
  "RQ494",
  "RQ495",
  "RQ505",
  "RQ507",
];

function validate(root) {
  const errors = [];
  const planPath = path.join(root, PLAN);
  if (!fs.existsSync(planPath)) return [`${PLAN}: missing`];

  const content = fs.readFileSync(planPath, "utf8");
  const normalizedContent = content.toLowerCase();
  for (const marker of REQUIRED_MARKERS) {
    if (!normalizedContent.includes(marker.toLowerCase())) errors.push(`${PLAN}: missing current-truth marker '${marker}'`);
  }

  if (!/Verified against main[^\n]*`[0-9a-f]{40}`/i.test(content)) {
    errors.push(`${PLAN}: current-truth section must name a full inspected main SHA`);
  }

  if (content.includes("decisionScore` je označen kao deprecated user-visible metric")) {
    errors.push(`${PLAN}: stale deprecated decisionScore claim remains unqualified`);
  }

  for (const relative of REQUIRED_REFERENCES) {
    if (!fs.existsSync(path.join(root, relative))) errors.push(`${relative}: referenced source is missing`);
  }

  return errors;
}

function write(root, relative, content) {
  const target = path.join(root, relative);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content, "utf8");
}

function runSelfTest() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "trendplus-execution-plan-"));
  try {
    const plan = `## 0. Current Contract\n\n> Verified against main: \`0123456789abcdef0123456789abcdef01234567\`\n\nHistorical audit/proposal.\n/analytics/supplier\nProdajaZaglavlje.DataOrigin\nBatchId + ProdajaStavkaId\nRQ494 RQ495 RQ505 RQ507`;
    write(root, PLAN, plan);
    for (const relative of REQUIRED_REFERENCES) write(root, relative, "source");
    const errors = validate(root);
    if (errors.length > 0) throw new Error(errors.join("\n"));
    console.log("analytics execution plan validator self-test: PASS");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

const args = process.argv.slice(2);
if (args.includes("--self-test")) {
  runSelfTest();
  process.exit(0);
}

const rootIndex = args.indexOf("--root");
const root = rootIndex >= 0 ? path.resolve(args[rootIndex + 1]) : process.cwd();
const errors = validate(root);
if (errors.length > 0) {
  console.error(`analytics execution plan validation: FAIL (${errors.length} issue(s))`);
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log("analytics execution plan validation: PASS");
