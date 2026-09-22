#!/usr/bin/env node

import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const STATUS_LABELS = {
  pass: "PASS",
  fail: "FAIL",
  timeout: "TIMEOUT",
  skipped: "SKIPPED",
  "environment-blocked": "ENVIRONMENT-BLOCKED",
  "not-run": "NOT-RUN",
};

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) continue;
    args[token.slice(2)] = argv[index + 1];
    index += 1;
  }
  return args;
}

function statusLabel(status) {
  return STATUS_LABELS[status] ?? String(status ?? "not-run").toUpperCase();
}

function renderResult(result) {
  const command = result.command ? `\`${result.command}\`` : "not executed";
  const exitCode = result.exitCode == null ? "null" : result.exitCode;
  const summary = result.summary ? ` — ${result.summary.replace(/\r?\n/g, " ")}` : "";
  return `- ${result.name}: **${statusLabel(result.status)}**; exitCode=${exitCode}; command=${command}${summary}`;
}

export function renderEvidence(evidence, options = {}) {
  const title = options.title ?? `Validation evidence: ${evidence.taskId ?? "task"}`;
  const lines = [
    `# ${title}`,
    "",
    `- Schema version: ${evidence.schemaVersion ?? "unknown"}`,
    `- Task: ${evidence.taskId ?? "unknown"}`,
    `- Commit: ${evidence.commit ?? "unknown"}`,
    `- Generated: ${evidence.timestamp ?? "unknown"}`,
    "",
    "## Validation results",
  ];

  for (const category of ["tests", "guardrails", "build"]) {
    const group = evidence[category] ?? { status: "not-run", results: [] };
    lines.push("", `### ${category} — **${statusLabel(group.status)}**`);
    if (group.results?.length) {
      lines.push(...group.results.map(renderResult));
    } else {
      lines.push("- No commands recorded.");
    }
  }

  lines.push("", "## Exit codes");
  const exitCodes = evidence.exitCodes ?? {};
  for (const [name, code] of Object.entries(exitCodes)) {
    lines.push(`- ${name}: ${code == null ? "null" : code}`);
  }
  if (Object.keys(exitCodes).length === 0) lines.push("- No commands recorded.");

  lines.push("", "## Skipped");
  if (evidence.skipped?.length) {
    lines.push(...evidence.skipped.map((item) => `- ${item.name}: ${item.reason}`));
  } else {
    lines.push("- none");
  }

  lines.push("", "## Main verification");
  const main = evidence.mainVerification;
  if (main) {
    lines.push(
      `- Status: **${statusLabel(main.status)}**`,
      `- Commit: ${main.commit ?? "null"}`,
      `- Ref: ${main.mainRef ?? "null"}`,
      `- Ref SHA: ${main.mainSha ?? "null"}`,
      `- Contains commit: ${main.containsCommit ? "true" : "false"}`,
    );
  } else {
    lines.push("- not run");
  }

  lines.push("", "## Environment", `- ${JSON.stringify(evidence.environment ?? {})}`);
  return `${lines.join("\n")}\n`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.input || !args.output) {
    throw new Error("Usage: node scripts/render-task-validation-evidence.mjs --input evidence.json --output evidence.md");
  }
  const cwd = path.resolve(args.cwd ?? process.cwd());
  const evidence = JSON.parse(await fs.readFile(path.resolve(cwd, args.input), "utf8"));
  const markdown = renderEvidence(evidence, { title: args.title });
  const outputPath = path.resolve(cwd, args.output);
  await fs.mkdir(path.dirname(outputPath), { recursive: true });
  await fs.writeFile(outputPath, markdown, "utf8");
  process.stdout.write(`Rendered validation evidence to ${path.relative(cwd, outputPath)}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    process.stderr.write(`${error.stack ?? error}\n`);
    process.exitCode = 1;
  });
}
