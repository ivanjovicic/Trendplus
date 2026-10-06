import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const srcDir = path.resolve(scriptDir, "../src");
const chartTag = /<(?:Area|Bar|Line|Pie|Composed|Scatter|Radar|RadialBar|Treemap|Funnel|Sankey)Chart\b/g;
const chartOpenTag = /<(?:Area|Bar|Line|Pie|Composed|Scatter|Radar|RadialBar|Treemap|Funnel|Sankey)Chart\b[^>]*>/gs;
const containerTag = /<ResponsiveContainer\b/g;
const frameTag = /<AnalyticsChartAccessibility\b/g;

const excludedWithRationale = new Map([
  ["pages/InsightStudioPage.tsx", "RQ582 keeps Insight Studio experimental and hidden; MetricCard sparklines are decorative, with the metric label and value adjacent."],
]);

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) return walk(fullPath);
    return fullPath.endsWith(".tsx") && !fullPath.includes(`${path.sep}__tests__${path.sep}`) ? [fullPath] : [];
  });
}

function inspectSource(source) {
  const charts = [...source.matchAll(chartOpenTag)].map((match) => match[0]);
  const containers = [...source.matchAll(containerTag)].length;
  const frames = [...source.matchAll(frameTag)].length;
  const problems = [];
  if (charts.length !== containers) problems.push(`chart/container count differs (${charts.length}/${containers})`);
  if (frames !== charts.length) problems.push(`accessible frame count differs (${frames}/${charts.length})`);
  if (charts.some((tag) => !/\baccessibilityLayer\b/.test(tag))) problems.push("a Recharts chart is missing accessibilityLayer");
  for (const [, targetId] of source.matchAll(/tableTargetId="([^"]+)"/g)) {
    const idCount = [...source.matchAll(new RegExp(`\\bid="${targetId}"`, "g"))].length;
    if (idCount !== 1) problems.push(`table alternative target #${targetId} resolves ${idCount} times`);
  }
  return problems;
}

function check() {
  const problems = [];
  let checkedCharts = 0;
  let excludedCharts = 0;
  for (const fullPath of walk(srcDir)) {
    const relativePath = path.relative(srcDir, fullPath).split(path.sep).join("/");
    const source = fs.readFileSync(fullPath, "utf8");
    if (!chartTag.test(source)) continue;
    chartTag.lastIndex = 0;

    if (excludedWithRationale.has(relativePath)) {
      excludedCharts += [...source.matchAll(chartOpenTag)].length;
      if (relativePath === "pages/InsightStudioPage.tsx") {
        const label = source.indexOf("{label}");
        const value = source.indexOf("{value}</div>");
        const sparkline = source.indexOf("{sparkline &&");
        if (label < 0 || value < label || sparkline < value) {
          problems.push(`${relativePath}: decorative sparkline no longer has its metric label and value immediately before it`);
        }
      }
      continue;
    }

    const sourceProblems = inspectSource(source);
    if (sourceProblems.length) problems.push(`${relativePath}: ${sourceProblems.join("; ")}`);
    checkedCharts += [...source.matchAll(chartOpenTag)].length;
  }

  return { problems, checkedCharts, excludedCharts };
}

if (process.argv.includes("--self-test")) {
  const bad = inspectSource("<ResponsiveContainer><BarChart data={[]} /></ResponsiveContainer>");
  if (bad.length === 0) {
    console.error("chart accessibility checker self-test failed to reject an unlabeled chart");
    process.exit(1);
  }
  console.log("chart accessibility checker self-test: PASS");
} else {
  const result = check();
  if (result.problems.length) {
    for (const problem of result.problems) console.error(`FAIL: ${problem}`);
    process.exit(1);
  }
  console.log(`analytics chart accessibility: PASS (${result.checkedCharts} charts checked; ${result.excludedCharts} quarantined Insight Studio charts documented)`);
}
