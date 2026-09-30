#!/usr/bin/env node

import fs from "node:fs/promises";
import { spawnSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const READINESS_STATES = Object.freeze([
  "code-ready",
  "runtime-unproven",
  "verified-current",
  "stale-evidence",
  "blocked",
  "failed",
]);

const FAILURE_STATUSES = new Set(["fail", "failed", "timeout"]);
const BLOCKED_STATUSES = new Set(["environment-blocked", "blocked", "skipped"]);

function readGitValue(args, cwd) {
  const result = spawnSync("git", args, { cwd, encoding: "utf8" });
  return result.status === 0 ? result.stdout.trim() : null;
}

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

function asDate(value) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? null : date;
}

function flattenStatuses(evidence) {
  if (evidence.status) return [evidence.status];
  return ["tests", "guardrails", "build"]
    .flatMap((category) => evidence[category]?.results ?? [])
    .map((result) => result.status)
    .filter(Boolean);
}

export function summarizeEvidenceStatus(evidence) {
  const statuses = flattenStatuses(evidence);
  if (statuses.some((status) => FAILURE_STATUSES.has(status))) return "failed";
  if (statuses.some((status) => BLOCKED_STATUSES.has(status))) return "blocked";
  if (statuses.length > 0 && statuses.every((status) => status === "pass")) return "pass";
  if (statuses.length > 0 && statuses.every((status) => status === "skipped")) return "skipped";
  return "blocked";
}

function generationsMatch(record, currentGenerations) {
  const references = record.generationRefs ?? {};
  return Object.entries(references).every(([name, value]) => currentGenerations[name] === value);
}

function expiration(record, checkedAt, now) {
  const expiresAt = asDate(record.expiresAtUtc)
    ?? (checkedAt && Number.isFinite(record.expiresInHours)
      ? new Date(checkedAt.valueOf() + record.expiresInHours * 60 * 60 * 1000)
      : null);
  return {
    expiresAtUtc: expiresAt?.toISOString() ?? null,
    expired: expiresAt ? expiresAt <= now : true,
  };
}

export function evaluateEvidence(record, context) {
  const now = asDate(context.nowUtc) ?? new Date();
  const source = record.loadedEvidence ?? null;
  const rawStatus = record.status ?? (source ? summarizeEvidenceStatus(source) : "blocked");
  const checkedSha = record.checkedSha ?? source?.commit ?? null;
  const checkedAt = asDate(record.checkedAtUtc ?? source?.timestamp);
  const expiry = expiration(record, checkedAt, now);
  const generationMatch = generationsMatch(record, context.currentGenerations ?? {});
  const shaMatch = Boolean(checkedSha && context.repoSha && checkedSha === context.repoSha);
  const deploymentKnown = Boolean(context.deployedSha);
  const deploymentMatch = Boolean(record.deployedSha && context.deployedSha
    && record.deployedSha === context.deployedSha);

  let state;
  let limitation = record.limitation ?? null;
  if (FAILURE_STATUSES.has(rawStatus)) {
    state = "failed";
  } else if (BLOCKED_STATUSES.has(rawStatus) || record.integrityStatus &&
      ["unverified", "degraded", "drift_detected"].includes(record.integrityStatus)) {
    state = "blocked";
  } else if (!checkedAt || expiry.expired || !shaMatch || !generationMatch ||
      (record.scope === "runtime" && deploymentKnown && !deploymentMatch)) {
    state = "stale-evidence";
    limitation ??= !checkedAt ? "Evidence has no checked timestamp." :
      expiry.expired ? "Evidence expiry policy has elapsed." :
        !shaMatch ? "Evidence was checked against a different repository SHA." :
          !generationMatch ? "Schema, contract or context generation changed." :
            "Evidence deployment SHA does not match the known deployment.";
  } else if (record.scope === "runtime") {
    state = deploymentKnown && deploymentMatch ? "verified-current" : "runtime-unproven";
    limitation ??= state === "runtime-unproven"
      ? "Repository evidence is current, but no matching deployed SHA is known."
      : null;
  } else {
    state = "code-ready";
  }

  return {
    id: record.id,
    family: record.family,
    scope: record.scope,
    state,
    source: record.source ?? record.evidenceFile ?? null,
    status: rawStatus,
    checkedSha,
    deployedSha: record.deployedSha ?? null,
    checkedAtUtc: checkedAt?.toISOString() ?? null,
    expiresAtUtc: expiry.expiresAtUtc,
    generationRefs: record.generationRefs ?? {},
    limitation,
    required: record.required !== false,
  };
}

function aggregateFamily(records) {
  const priority = ["failed", "blocked", "stale-evidence", "runtime-unproven", "code-ready", "verified-current"];
  return priority.find((state) => records.some((record) => record.state === state)) ?? "blocked";
}

export function buildReadinessSnapshot(manifest, context = {}) {
  const nowUtc = context.nowUtc ?? new Date().toISOString();
  const evidence = (manifest.evidence ?? []).map((record) => ({
    ...record,
    loadedEvidence: record.loadedEvidence,
  }));
  const evaluated = evidence.map((record) => evaluateEvidence(record, {
    nowUtc,
    repoSha: context.repoSha,
    deployedSha: context.deployedSha ?? manifest.deployedSha ?? null,
    currentGenerations: manifest.currentGenerations ?? {},
  }));
  const families = [...new Set(evaluated.map((record) => record.family))].map((family) => {
    const records = evaluated.filter((record) => record.family === family);
    return { family, state: aggregateFamily(records), evidence: records };
  });
  const overallState = aggregateFamily(evaluated);
  return {
    schemaVersion: 1,
    kind: "analytics-production-readiness-snapshot",
    generatedAtUtc: nowUtc,
    repoSha: context.repoSha ?? null,
    deployedSha: context.deployedSha ?? manifest.deployedSha ?? null,
    currentGenerations: manifest.currentGenerations ?? {},
    expiryPolicy: manifest.expiryPolicy ?? null,
    overallState,
    families,
    limitations: manifest.limitations ?? [],
    sourceManifest: manifest.sourceManifest ?? null,
  };
}

export function renderReadinessMarkdown(snapshot, options = {}) {
  const title = options.title ?? "Analytics Production Readiness Status";
  const lines = [
    `# ${title}`,
    "",
    "> This document is generated from the machine-readable snapshot. It is not a hand-maintained global release verdict.",
    "> A missing, expired, SHA-mismatched or externally blocked proof is intentionally not rendered as PASS.",
    "",
    `- Snapshot generated: ${snapshot.generatedAtUtc ?? "unknown"}`,
    `- Repository SHA: \`${snapshot.repoSha ?? "unknown"}\``,
    `- Deployed SHA: \`${snapshot.deployedSha ?? "unknown / not supplied"}\``,
    `- Overall state: **${snapshot.overallState ?? "blocked"}**`,
    `- Schema/contract/context generations: \`${JSON.stringify(snapshot.currentGenerations ?? {})}\``,
    `- Expiry policy: ${snapshot.expiryPolicy ?? "not supplied"}`,
    "",
    "## Evidence by family",
    "",
    "| Family | State | Evidence | Checked SHA | Checked at | Expires | Limitation |",
    "| --- | --- | --- | --- | --- | --- | --- |",
  ];
  for (const family of snapshot.families ?? []) {
    for (const evidence of family.evidence ?? []) {
      lines.push(`| ${family.family} | **${evidence.state}** | ${evidence.source ?? evidence.id} | \`${evidence.checkedSha ?? "unknown"}\` | ${evidence.checkedAtUtc ?? "unknown"} | ${evidence.expiresAtUtc ?? "unknown"} | ${(evidence.limitation ?? "none").replaceAll("|", "\\|")} |`);
    }
  }
  lines.push("", "## Limitations", "");
  if (snapshot.limitations?.length) lines.push(...snapshot.limitations.map((item) => `- ${item}`));
  else lines.push("- none recorded");
  lines.push("", "## State semantics", "", "- `code-ready`: current repository evidence is fresh and SHA-bound.", "- `runtime-unproven`: code evidence is current, but matching deployed/runtime proof is absent.", "- `verified-current`: required runtime proof is fresh and matches repository, deployment and generations.", "- `stale-evidence`: evidence expired or no longer matches SHA/generation/deployment.", "- `blocked`: proof was skipped, unavailable, integrity-unverified or externally gated.", "- `failed`: executed evidence failed.");
  return `${lines.join("\n")}\n`;
}

async function loadManifest(cwd, inputPath) {
  const manifest = JSON.parse(await fs.readFile(inputPath, "utf8"));
  for (const record of manifest.evidence ?? []) {
    if (!record.evidenceFile) continue;
    const evidencePath = path.resolve(cwd, record.evidenceFile);
    try {
      record.loadedEvidence = JSON.parse(await fs.readFile(evidencePath, "utf8"));
    } catch (error) {
      record.status = "blocked";
      record.limitation ??= `Evidence file unavailable: ${record.evidenceFile}`;
      record.loadError = error.message;
    }
  }
  return manifest;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.input || !args.json || !args.markdown) {
    throw new Error("Usage: node scripts/generate-analytics-readiness.mjs --input manifest.json --json snapshot.json --markdown status.md [--cwd repo]");
  }
  const cwd = path.resolve(args.cwd ?? process.cwd());
  const manifest = await loadManifest(cwd, path.resolve(cwd, args.input));
  const snapshot = buildReadinessSnapshot(manifest, {
    nowUtc: args.now ?? new Date().toISOString(),
    repoSha: readGitValue(["rev-parse", "HEAD"], cwd),
    deployedSha: args["deployed-sha"] ?? manifest.deployedSha ?? null,
  });
  const jsonPath = path.resolve(cwd, args.json);
  const markdownPath = path.resolve(cwd, args.markdown);
  await fs.mkdir(path.dirname(jsonPath), { recursive: true });
  await fs.mkdir(path.dirname(markdownPath), { recursive: true });
  await fs.writeFile(jsonPath, `${JSON.stringify(snapshot, null, 2)}\n`, "utf8");
  await fs.writeFile(markdownPath, renderReadinessMarkdown(snapshot), "utf8");
  process.stdout.write(`Readiness snapshot written to ${path.relative(cwd, jsonPath)}\n`);
  process.stdout.write(`Readiness Markdown written to ${path.relative(cwd, markdownPath)}\n`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    process.stderr.write(`${error.stack ?? error}\n`);
    process.exitCode = 1;
  });
}
