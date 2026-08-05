#!/usr/bin/env node
/**
 * Prompt-queue governance validator (STAB02).
 *
 * Validates active docs/ai queue files for:
 * - unsupported statuses (OPEN, TODO, …)
 * - duplicate task IDs inside one file
 * - more than one exclusive READY task in the same feature family
 * - Current READY prompt that is missing or not READY
 * - GenAI marked READY while earlier P0 STAB gates remain unresolved
 *
 * Usage:
 *   node scripts/check-prompt-queues.mjs
 *   node scripts/check-prompt-queues.mjs --self-test
 *   node scripts/check-prompt-queues.mjs --root <dir>
 */

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ALLOWED_STATUSES = new Set([
  "READY",
  "WAITING",
  "IN_PROGRESS",
  "BLOCKED",
  "PARTIAL",
  "DONE",
  "OBSOLETE",
]);

const UNSUPPORTED_STATUSES = new Set(["OPEN", "TODO", "IN PROGRESS", "COMPLETE", "COMPLETED"]);

const ACTIVE_QUEUE_FILES = [
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ACTION_OUTCOME_ADDENDUM.md",
  "docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md",
  "docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md",
  "docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md",
  "docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md",
  "docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md",
  "docs/ai/NEXT_PROMPT_QUEUE.md",
];

const TASK_HEADER_RE = /^##\s+((?:STAB|RQ|Q|GAI|P-UI)[\w.-]+)\b[^\n]*$/m;
const STATUS_RE = /^Status:\s*`?([A-Za-z0-9 _-]+)`?\s*$/gm;
const CURRENT_READY_RE = /^Current READY prompt:\s*(.+)$/im;
const FEATURE_FAMILY_RE = /^Feature family:\s*(.+)$/im;
const PARALLEL_SAFE_RE = /^Parallel-safe:\s*(.+)$/im;
const PRIORITY_RE = /^Priority:\s*(.+)$/im;

function normalizeStatus(raw) {
  return String(raw ?? "")
    .trim()
    .replace(/^`+|`+$/g, "")
    .toUpperCase()
    .replace(/\s+/g, "_");
}

function normalizeReadyToken(raw) {
  const text = String(raw ?? "").trim();
  if (!text || /^none\b/i.test(text)) return null;
  const cleaned = text.replace(/^[`"'[]+|[`"'\]]+$/g, "").trim();
  const match = cleaned.match(/\b((?:STAB|RQ|Q|GAI|P-UI)[\w.-]+)\b/i);
  return match ? match[1] : null;
}

function isParallelSafe(value) {
  const text = String(value ?? "").trim().toLowerCase();
  if (!text) return false;
  if (text === "no" || text.startsWith("no,")) return false;
  return text.startsWith("yes");
}

function parseTasks(content, filePath) {
  const lines = content.split(/\r?\n/);
  const tasks = [];
  let current = null;

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    const header = line.match(/^##\s+((?:STAB|RQ|Q|GAI|P-UI)[\w.-]+)\b/);
    if (header) {
      if (current) tasks.push(current);
      current = {
        id: header[1],
        file: filePath,
        headerLine: i + 1,
        status: null,
        statusLine: null,
        featureFamily: null,
        parallelSafe: false,
        priority: null,
      };
      continue;
    }

    if (!current) continue;

    const statusMatch = line.match(/^Status:\s*`?([A-Za-z0-9 _-]+)`?\s*$/i);
    if (statusMatch && current.status == null) {
      current.status = normalizeStatus(statusMatch[1]);
      current.statusLine = i + 1;
      continue;
    }

    const familyMatch = line.match(/^Feature family:\s*(.+)$/i);
    if (familyMatch && current.featureFamily == null) {
      current.featureFamily = familyMatch[1].trim();
      continue;
    }

    const parallelMatch = line.match(/^Parallel-safe:\s*(.+)$/i);
    if (parallelMatch) {
      current.parallelSafe = isParallelSafe(parallelMatch[1]);
      continue;
    }

    const priorityMatch = line.match(/^Priority:\s*(.+)$/i);
    if (priorityMatch && current.priority == null) {
      current.priority = priorityMatch[1].trim();
    }
  }

  if (current) tasks.push(current);
  return tasks;
}

function collectStatusLines(content, filePath) {
  const findings = [];
  const lines = content.split(/\r?\n/);
  for (let i = 0; i < lines.length; i += 1) {
    const match = lines[i].match(/^Status:\s*`?([A-Za-z0-9 _-]+)`?\s*$/i);
    if (!match) continue;
    const status = normalizeStatus(match[1]);
    findings.push({ file: filePath, line: i + 1, status, raw: match[1] });
  }
  return findings;
}

function validateQueueFile(filePath, content) {
  const errors = [];
  const tasks = parseTasks(content, filePath);
  const statuses = collectStatusLines(content, filePath);

  for (const entry of statuses) {
    if (UNSUPPORTED_STATUSES.has(entry.status) || !ALLOWED_STATUSES.has(entry.status)) {
      errors.push(`${filePath}:${entry.line}: unsupported status '${entry.raw}' (allowed: ${[...ALLOWED_STATUSES].join("|")})`);
    }
  }

  const byId = new Map();
  for (const task of tasks) {
    if (!byId.has(task.id)) byId.set(task.id, []);
    byId.get(task.id).push(task);
  }
  for (const [id, list] of byId) {
    if (list.length > 1) {
      for (const task of list.slice(1)) {
        errors.push(`${filePath}:${task.headerLine}: duplicate task id '${id}'`);
      }
    }
  }

  const readyByFamily = new Map();
  for (const task of tasks) {
    if (task.status !== "READY") continue;
    const family = task.featureFamily || `unspecified:${task.id}`;
    if (!readyByFamily.has(family)) readyByFamily.set(family, []);
    readyByFamily.get(family).push(task);
  }
  for (const [family, list] of readyByFamily) {
    if (list.length <= 1) continue;
    const exclusive = list.filter((task) => !task.parallelSafe);
    if (exclusive.length > 1) {
      for (const task of exclusive) {
        errors.push(
          `${filePath}:${task.statusLine ?? task.headerLine}: multiple exclusive READY tasks in feature family '${family}' (${exclusive.map((t) => t.id).join(", ")})`,
        );
      }
    }
  }

  const currentReadyMatch = content.match(CURRENT_READY_RE);
  if (currentReadyMatch) {
    const token = normalizeReadyToken(currentReadyMatch[1]);
    if (token) {
      const task = tasks.find((entry) => entry.id === token);
      const line = content.slice(0, currentReadyMatch.index).split(/\r?\n/).length;
      if (!task) {
        errors.push(`${filePath}:${line}: Current READY prompt '${token}' is missing from this queue`);
      } else if (task.status !== "READY" && task.status !== "IN_PROGRESS") {
        errors.push(
          `${filePath}:${line}: Current READY prompt '${token}' has status '${task.status}' (expected READY or IN_PROGRESS)`,
        );
      }
    }
  }

  return { errors, tasks };
}

function validateGenAiGate(allTasks) {
  const errors = [];
  const genAiReady = allTasks.filter(
    (task) => task.file.replace(/\\/g, "/").endsWith("GENAI_PRODUCT_PROMPT_QUEUE.md") && (task.status === "READY" || task.status === "IN_PROGRESS"),
  );
  if (genAiReady.length === 0) return errors;

  const unresolvedStabP0 = allTasks.filter((task) => {
    if (!task.file.replace(/\\/g, "/").endsWith("STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md")) return false;
    if (!/^STAB\d+/i.test(task.id)) return false;
    const priority = String(task.priority ?? "").toUpperCase();
    if (!priority.includes("P0")) return false;
    return task.status === "READY" || task.status === "PARTIAL" || task.status === "BLOCKED" || task.status === "IN_PROGRESS";
  });

  if (unresolvedStabP0.length > 0) {
    for (const task of genAiReady) {
      errors.push(
        `${task.file}:${task.statusLine ?? task.headerLine}: GenAI task '${task.id}' is ${task.status} while unresolved P0 stabilization gate(s) remain: ${unresolvedStabP0.map((t) => `${t.id}:${t.status}`).join(", ")}`,
      );
    }
  }

  return errors;
}

function validateRoot(rootDir) {
  const errors = [];
  const allTasks = [];

  for (const relative of ACTIVE_QUEUE_FILES) {
    const absolute = path.join(rootDir, relative);
    if (!fs.existsSync(absolute)) {
      errors.push(`${relative}:1: active queue file is missing`);
      continue;
    }
    const content = fs.readFileSync(absolute, "utf8");
    const result = validateQueueFile(relative.replace(/\\/g, "/"), content);
    errors.push(...result.errors);
    allTasks.push(...result.tasks);
  }

  errors.push(...validateGenAiGate(allTasks));
  return { errors, taskCount: allTasks.length };
}

function writeFixture(dir, relativePath, content) {
  const absolute = path.join(dir, relativePath);
  fs.mkdirSync(path.dirname(absolute), { recursive: true });
  fs.writeFileSync(absolute, content, "utf8");
}

function runSelfTest() {
  const tmpRoot = fs.mkdtempSync(path.join(path.dirname(fileURLToPath(import.meta.url)), "prompt-queue-selftest-"));
  const failures = [];

  try {
    // Minimal valid set: create all required files with empty/valid content where needed.
    for (const relative of ACTIVE_QUEUE_FILES) {
      writeFixture(
        tmpRoot,
        relative,
        relative.endsWith("STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md")
          ? `# Stab\nCurrent READY prompt: STAB99\n\n## STAB99 - Example\n\nStatus: READY\nPriority: P0\nFeature family: example-gate\nParallel-safe: yes\n`
          : relative.endsWith("GENAI_PRODUCT_PROMPT_QUEUE.md")
            ? `# GenAI\n\n## GAI01 - Gate\n\nStatus: WAITING\nPriority: P0\nFeature family: genai-gate\nParallel-safe: no\n`
            : relative.endsWith("ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md")
              ? `# UI\nCurrent READY prompt: P-UI-05\n\n## P-UI-05 - Visual\n\nStatus: READY\nPriority: P0\nFeature family: analytics-ui-visual-regression\nParallel-safe: yes\n`
              : `# Queue\nCurrent READY prompt: none\n`,
      );
    }

    const valid = validateRoot(tmpRoot);
    if (valid.errors.length > 0) {
      failures.push(`valid sample failed:\n${valid.errors.join("\n")}`);
    }

    writeFixture(
      tmpRoot,
      "docs/ai/NEXT_PROMPT_QUEUE.md",
      `# Next\n\n## Q99 - Bad\n\nStatus: OPEN\n`,
    );
    const openResult = validateRoot(tmpRoot);
    if (!openResult.errors.some((error) => error.includes("unsupported status 'OPEN'"))) {
      failures.push("expected unsupported OPEN failure with file:line");
    }

    writeFixture(
      tmpRoot,
      "docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md",
      `# UI\nCurrent READY prompt: P-UI-05\n\n## P-UI-05 - A\n\nStatus: READY\nFeature family: same-family\nParallel-safe: no\n\n## P-UI-06 - B\n\nStatus: READY\nFeature family: same-family\nParallel-safe: no\n`,
    );
    const dupReady = validateRoot(tmpRoot);
    if (!dupReady.errors.some((error) => error.includes("multiple exclusive READY"))) {
      failures.push("expected duplicate exclusive READY failure");
    }

    writeFixture(
      tmpRoot,
      "docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md",
      `# UI\nCurrent READY prompt: P-UI-05\n\n## P-UI-05 - A\n\nStatus: READY\nFeature family: same-family\nParallel-safe: yes\n\n## P-UI-06 - B\n\nStatus: READY\nFeature family: same-family\nParallel-safe: yes\n`,
    );
    const parallelOk = validateRoot(tmpRoot);
    if (parallelOk.errors.some((error) => error.includes("multiple exclusive READY"))) {
      failures.push("parallel-safe READY pair should pass");
    }

    writeFixture(
      tmpRoot,
      "docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md",
      `# Stab\nCurrent READY prompt: STAB42\n\n## STAB41 - Other\n\nStatus: WAITING\nPriority: P0\nFeature family: missing-ready\nParallel-safe: no\n`,
    );
    const missingReady = validateRoot(tmpRoot);
    if (!missingReady.errors.some((error) => error.includes("Current READY prompt 'STAB42' is missing"))) {
      failures.push("expected missing Current READY failure");
    }

    writeFixture(
      tmpRoot,
      "docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md",
      `# Stab\nCurrent READY prompt: STAB01\n\n## STAB01 - Gate\n\nStatus: READY\nPriority: P0\nFeature family: current-main-release-truth\nParallel-safe: yes\n`,
    );
    writeFixture(
      tmpRoot,
      "docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md",
      `# GenAI\n\n## GAI01 - Gate\n\nStatus: READY\nPriority: P0\nFeature family: genai-gate\nParallel-safe: no\n`,
    );
    const genAiConflict = validateRoot(tmpRoot);
    if (!genAiConflict.errors.some((error) => error.includes("GenAI task 'GAI01'"))) {
      failures.push("expected GenAI gate conflict while STAB P0 READY remains");
    }
  } finally {
    fs.rmSync(tmpRoot, { recursive: true, force: true });
  }

  if (failures.length > 0) {
    console.error("SELF-TEST FAILED");
    for (const failure of failures) console.error(`- ${failure}`);
    process.exit(1);
  }

  console.log("OK: prompt-queue validator self-test passed.");
}

function main() {
  const args = process.argv.slice(2);
  if (args.includes("--self-test")) {
    runSelfTest();
    return;
  }

  const rootIdx = args.indexOf("--root");
  const rootDir = rootIdx >= 0 ? path.resolve(args[rootIdx + 1]) : path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const { errors, taskCount } = validateRoot(rootDir);

  if (errors.length > 0) {
    console.error(`FAIL: ${errors.length} prompt-queue governance issue(s) across ${taskCount} tasks`);
    for (const error of errors) console.error(error);
    process.exit(1);
  }

  console.log(`OK: prompt-queue governance checks passed (${taskCount} tasks).`);
}

main();