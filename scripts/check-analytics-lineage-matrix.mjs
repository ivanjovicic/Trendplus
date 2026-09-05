import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const matrixPath = resolve("docs/qa/ANALYTICS_ROUTE_LINEAGE_MATRIX_2026-09-05.md");
const matrix = readFileSync(matrixPath, "utf8");

const requiredMarkers = [
  "/analytics",
  "/analytics/products",
  "/analytics/supplier",
  "/analytics/inventory",
  "/analytics/actions",
  "/analytics/decision-board",
  "/analytics/data-quality",
  "/analytics/reports",
  "Sales summaries and dimensions",
  "Trend / momentum / index",
  "Forecast / stock depletion",
  "Pre/post nivelacija vendor",
  "Pre/post nivelacija by shoe type/color",
  "Insight Studio advanced composite",
  "Traženi period",
  "Efektivni period",
  "Posmatrani period",
  "Data scope",
  "Vreme generisanja",
  "Poslednji uspešan refresh",
  "Freshness status",
  "Data quality status",
  "Empty / partial / error",
  "Recommendation allowed",
  "Razlog ograničenja",
  "UNPROVEN-RUNTIME",
  "STAB16",
];

const missing = requiredMarkers.filter((marker) => !matrix.includes(marker));
if (missing.length > 0) {
  console.error(`FAIL: analytics lineage matrix is missing: ${missing.join(", ")}`);
  process.exit(1);
}

const routeRows = matrix
  .split("\n")
  .filter((line) => line.startsWith("| `/analytics"));
if (routeRows.length < 8) {
  console.error(`FAIL: expected at least 8 top-level analytics route rows, found ${routeRows.length}.`);
  process.exit(1);
}

console.log(`OK: analytics lineage matrix covers ${routeRows.length} analytics route/family rows and all required trust fields.`);
