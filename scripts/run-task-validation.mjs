#!/usr/bin/env node

import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawn, spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const OUTPUT_LIMIT = 8_000;
const SUMMARY_LINES = 20;

function parseArgs(argv) {
  const args = {};
  for (let index = 0; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) continue;
    const key = token.slice(2);
    if (key === "verify-main") {
      args[key] = true;
      continue;
    }
    args[key] = argv[index + 1];
    index += 1;
  }
  return args;
}

function truncate(value) {
  const text = String(value ?? "");
  if (text.length <= OUTPUT_LIMIT) return text;
  return `${text.slice(0, OUTPUT_LIMIT)}\n...[truncated]`;
}

function summarize(value) {
  return truncate(String(value ?? "")
    .split(/\r?\n/)
    .filter((line) => line.trim().length > 0)
    .slice(-SUMMARY_LINES)
    .join("\n"));
}

function aggregateStatus(results) {
  if (results.some((result) => result.status === "fail")) return "fail";
  if (results.some((result) => result.status === "timeout")) return "timeout";
  if (results.some((result) => result.status === "environment-blocked")) return "environment-blocked";
  if (results.length > 0 && results.every((result) => result.status === "skipped")) return "skipped";
  if (results.some((result) => result.status === "pass")) return "pass";
  return "not-run";
}

export function runCommand(command, options = {}) {
  const {
    cwd = process.cwd(),
    timeoutMs = 120_000,
    env = process.env,
  } = options;

  return new Promise((resolve) => {
    const startedAt = new Date().toISOString();
    const child = spawn(command, {
      cwd,
      env,
      shell: true,
      windowsHide: true,
    });
    let stdout = "";
    let stderr = "";
    let timedOut = false;
    const timeout = setTimeout(() => {
      timedOut = true;
      child.kill("SIGTERM");
    }, timeoutMs);

    child.stdout.on("data", (chunk) => { stdout += chunk; });
    child.stderr.on("data", (chunk) => { stderr += chunk; });
    child.on("error", (error) => {
      clearTimeout(timeout);
      resolve({
        status: "environment-blocked",
        exitCode: null,
        signal: null,
        startedAt,
        finishedAt: new Date().toISOString(),
        durationMs: Date.now() - Date.parse(startedAt),
        summary: error.message,
        output: "",
      });
    });
    child.on("close", (exitCode, signal) => {
      clearTimeout(timeout);
      const output = truncate(`${stdout}${stderr ? `\n${stderr}` : ""}`);
      resolve({
        status: timedOut ? "timeout" : exitCode === 0 ? "pass" : "fail",
        exitCode: timedOut ? null : exitCode,
        signal: signal ?? null,
        startedAt,
        finishedAt: new Date().toISOString(),
        durationMs: Date.now() - Date.parse(startedAt),
        summary: summarize(output),
        output,
      });
    });
  });
}

export async function runValidationPlan(plan, options = {}) {
  const commands = Array.isArray(plan) ? plan : plan.commands ?? [];
  const results = [];
  for (const item of commands) {
    const category = item.category ?? "tests";
    let result;
    if (item.skipReason) {
      result = {
        status: "skipped",
        exitCode: null,
        signal: null,
        summary: item.skipReason,
        output: "",
      };
    } else if (item.environmentBlockedReason) {
      result = {
        status: "environment-blocked",
        exitCode: null,
        signal: null,
        summary: item.environmentBlockedReason,
        output: "",
      };
    } else if (!item.command) {
      result = {
        status: "environment-blocked",
        exitCode: null,
        signal: null,
        summary: "Validation command is missing.",
        output: "",
      };
    } else {
      result = await runCommand(item.command, {
        cwd: item.cwd ?? options.cwd,
        timeoutMs: item.timeoutMs ?? options.timeoutMs ?? 120_000,
        env: item.env ?? process.env,
      });
    }
    results.push({
      name: item.name ?? item.command ?? category,
      category,
      command: item.command ?? null,
      ...result,
    });
  }

  const grouped = {};
  for (const result of results) {
    grouped[result.category] ??= [];
    grouped[result.category].push(result);
  }
  const exitCodes = Object.fromEntries(results.map((result) => [result.name, result.exitCode]));
  const skipped = results
    .filter((result) => result.status === "skipped")
    .map(({ name, summary }) => ({ name, reason: summary }));

  return {
    schemaVersion: 1,
    taskId: options.taskId ?? plan.taskId ?? null,
    commit: options.commit ?? readGitValue(["rev-parse", "HEAD"], options.cwd),
    timestamp: new Date().toISOString(),
    environment: {
      cwd: options.cwd ?? process.cwd(),
      node: process.version,
      platform: process.platform,
      arch: process.arch,
      hostname: os.hostname(),
    },
    tests: { status: aggregateStatus(grouped.tests ?? []), results: grouped.tests ?? [] },
    guardrails: { status: aggregateStatus(grouped.guardrails ?? []), results: grouped.guardrails ?? [] },
    build: { status: aggregateStatus(grouped.build ?? []), results: grouped.build ?? [] },
    results,
    exitCodes,
    skipped,
  };
}

export function verifyMain(commit, options = {}) {
  const cwd = options.cwd ?? process.cwd();
  const mainRef = options.mainRef ?? "origin/main";
  const mainSha = readGitValue(["rev-parse", mainRef], cwd);
  if (!commit || !mainSha) {
    return {
      status: "unavailable",
      commit: commit ?? null,
      mainRef,
      mainSha: mainSha ?? null,
      containsCommit: false,
      exactTip: false,
    };
  }
  const ancestor = spawnSync("git", ["merge-base", "--is-ancestor", commit, mainRef], {
    cwd,
    encoding: "utf8",
  });
  const containsCommit = ancestor.status === 0;
  return {
    status: containsCommit ? (commit === mainSha ? "exact-tip" : "ancestor") : "not-contained",
    commit,
    mainRef,
    mainSha,
    containsCommit,
    exactTip: commit === mainSha,
  };
}

function readGitValue(args, cwd = process.cwd()) {
  const result = spawnSync("git", args, { cwd, encoding: "utf8" });
  return result.status === 0 ? result.stdout.trim() : null;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args.plan || !args.output) {
    throw new Error("Usage: node scripts/run-task-validation.mjs --plan plan.json --output evidence.json [--verify-main]");
  }
  const cwd = path.resolve(args.cwd ?? process.cwd());
  const plan = JSON.parse(await fs.readFile(path.resolve(cwd, args.plan), "utf8"));
  const evidence = await runValidationPlan(plan, {
    cwd,
    taskId: args.task ?? plan.taskId,
    timeoutMs: Number(args.timeoutMs ?? 120_000),
  });
  if (args["verify-main"]) {
    evidence.mainVerification = verifyMain(evidence.commit, { cwd, mainRef: args["main-ref"] });
  }
  const outputPath = path.resolve(cwd, args.output);
  await fs.mkdir(path.dirname(outputPath), { recursive: true });
  await fs.writeFile(outputPath, `${JSON.stringify(evidence, null, 2)}\n`, "utf8");
  process.stdout.write(`Validation evidence written to ${path.relative(cwd, outputPath)}\n`);
  if (evidence.results.some((result) => ["fail", "timeout", "environment-blocked"].includes(result.status))) {
    process.exitCode = 1;
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    process.stderr.write(`${error.stack ?? error}\n`);
    process.exitCode = 1;
  });
}
