#!/usr/bin/env node
/**
 * Trendplus planning architecture validator.
 *
 * Complements scripts/check-prompt-queues.mjs by validating the consolidated
 * master roadmap, roadmap/queue ownership, and the DEX/RL/DT/PERF/OBS/SEC
 * planning families.
 *
 * Usage:
 *   node scripts/check-planning-architecture.mjs
 *   node scripts/check-planning-architecture.mjs --self-test
 *   node scripts/check-planning-architecture.mjs --root <dir>
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const REQUIRED_CANONICAL_PATHS = [
  "MASTER_ROADMAP.md",
  "docs/product/PRODUCT_VISION.md",
  "docs/planning/FEATURE_LIFECYCLE.md",
  "docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md",
  "docs/roadmaps/PERFORMANCE_ROADMAP.md",
  "docs/roadmaps/OBSERVABILITY_ROADMAP.md",
  "docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md",
  "docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md",
  "docs/roadmaps/BUSINESS_ROADMAP.md",
  "docs/architecture/ADRS.md",
  "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md",
  "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md",
  "docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md",
  "docs/ai/AGENT_START_HERE.md",
  "docs/ai/PROMPT_QUEUE_PROTOCOL.md",
  "docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md",
  "docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md",
  "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md",
  "docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md",
  "docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md",
  "docs/ai/MULTITENANCY_PROMPT_QUEUE.md",
  "docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md",
  "docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md",
  "docs/ai/GENAI_COPILOT_ROADMAP.md",
];

const PROGRAM_OWNERSHIP = [
  { program: "BCI", roadmap: "MASTER_ROADMAP.md", queue: "docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md" },
  { program: "STAB", roadmap: "MASTER_ROADMAP.md", queue: "docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md" },
  { program: "RQ", roadmap: "docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md", queue: "docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md" },
  { program: "P-UI", roadmap: "docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md", queue: "docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md" },
  { program: "QDB", roadmap: "docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md", queue: "docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md" },
  { program: "MT", roadmap: "docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md", queue: "docs/ai/MULTITENANCY_PROMPT_QUEUE.md" },
  { program: "GAI", roadmap: "docs/ai/GENAI_COPILOT_ROADMAP.md", queue: "docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md" },
  { program: "DEX", roadmap: "docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md", queue: "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md" },
  { program: "RL", roadmap: "docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md", queue: "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md" },
  { program: "DT", roadmap: "docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md", queue: "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md" },
  { program: "PERF", roadmap: "docs/roadmaps/PERFORMANCE_ROADMAP.md", queue: "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md" },
  { program: "OBS", roadmap: "docs/roadmaps/OBSERVABILITY_ROADMAP.md", queue: "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md" },
  { program: "SEC", roadmap: "docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md", queue: "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md" },
];

const NEW_PROGRAMS = new Set(["DEX", "RL", "DT", "PERF", "OBS", "SEC"]);
const REQUIRED_PROMPT_SECTIONS = [
  "Problem",
  "Evidence",
  "Scope",
  "Read first",
  "Do",
  "Tests",
  "Acceptance",
  "Dependencies",
];
const ALLOWED_STATUSES = new Set(["READY", "WAITING", "IN_PROGRESS", "BLOCKED", "PARTIAL", "DONE", "OBSOLETE"]);
const TASK_HEADER = /^##\s+((DEX|RL|DT|PERF|OBS|SEC)\d+)\b.*$/;

function exists(root, relative) {
  return fs.existsSync(path.join(root, relative));
}

function read(root, relative) {
  return fs.readFileSync(path.join(root, relative), "utf8");
}

function parseNewTasks(content, file) {
  const lines = content.split(/\r?\n/);
  const tasks = [];
  let current = null;

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    const match = line.match(TASK_HEADER);
    if (match) {
      if (current) tasks.push(current);
      current = {
        id: match[1],
        program: match[2],
        file,
        line: index + 1,
        status: null,
        owner: null,
        featureFamily: null,
        parallelSafe: false,
        parallelSafeDeclared: false,
        sections: new Set(),
      };
      continue;
    }
    if (!current) continue;

    const status = line.match(/^Status:\s*`?([A-Za-z_]+)`?\s*$/i);
    if (status && current.status == null) current.status = status[1].toUpperCase();

    const owner = line.match(/^Owner:\s*(.+)$/i);
    if (owner && current.owner == null) current.owner = owner[1].trim();

    const family = line.match(/^Feature family:\s*(.+)$/i);
    if (family && current.featureFamily == null) current.featureFamily = family[1].trim();

    const parallelSafe = line.match(/^Parallel-safe:\s*(.+)$/i);
    if (parallelSafe) {
      current.parallelSafeDeclared = true;
      current.parallelSafe = /^yes\b/i.test(parallelSafe[1].trim());
    }

    const section = line.match(/^###\s+(.+?)\s*$/);
    if (section) current.sections.add(section[1].trim());
  }
  if (current) tasks.push(current);
  return tasks;
}

function tableCells(line) {
  const trimmed = line.trim();
  if (!trimmed.startsWith("|") || !trimmed.endsWith("|")) return null;
  return trimmed.split("|").slice(1, -1).map((cell) => cell.trim());
}

function normalizePointer(value) {
  return value == null ? null : value.replaceAll("`", "").trim();
}

function isNonePointer(value) {
  const normalized = normalizePointer(value);
  return normalized != null && /^none(?:\s*\(.*\))?$/i.test(normalized);
}

function ownerQueueCurrentReady(content, program) {
  for (const line of content.split(/\r?\n/)) {
    const cells = tableCells(line);
    if (!cells || cells.length < 2) continue;
    const label = cells[0];
    if (label === program || label.startsWith(`${program} -`)) return normalizePointer(cells[1]);
  }
  return null;
}

function masterCurrentReady(content, program) {
  for (const line of content.split(/\r?\n/)) {
    const cells = tableCells(line);
    if (!cells || cells.length < 3 || cells[0] !== program) continue;
    return normalizePointer(cells[2]);
  }
  return null;
}

function validate(root) {
  const errors = [];

  for (const relative of REQUIRED_CANONICAL_PATHS) {
    if (!exists(root, relative)) errors.push(`${relative}: missing canonical planning path`);
  }

  for (const mapping of PROGRAM_OWNERSHIP) {
    if (!exists(root, mapping.roadmap)) errors.push(`${mapping.program}: owner roadmap missing: ${mapping.roadmap}`);
    if (!exists(root, mapping.queue)) errors.push(`${mapping.program}: owner queue missing: ${mapping.queue}`);
  }

  const newQueueFiles = [
    "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md",
    "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md",
  ];

  const tasks = [];
  for (const relative of newQueueFiles) {
    if (!exists(root, relative)) continue;
    tasks.push(...parseNewTasks(read(root, relative), relative));
  }

  for (const task of tasks) {
    if (!task.status || !ALLOWED_STATUSES.has(task.status)) {
      errors.push(`${task.file}:${task.line}: ${task.id} missing/invalid Status`);
    }
    if (!task.owner) errors.push(`${task.file}:${task.line}: ${task.id} missing Owner`);
    for (const section of REQUIRED_PROMPT_SECTIONS) {
      if (!task.sections.has(section)) {
        errors.push(`${task.file}:${task.line}: ${task.id} missing required section '### ${section}'`);
      }
    }
  }

  const master = exists(root, "MASTER_ROADMAP.md") ? read(root, "MASTER_ROADMAP.md") : null;

  for (const program of NEW_PROGRAMS) {
    const programTasks = tasks.filter((task) => task.program === program);
    if (programTasks.length === 0) {
      errors.push(`${program}: no prompt found in new planning queues`);
      continue;
    }

    const active = programTasks.filter((task) => task.status === "READY" || task.status === "IN_PROGRESS");

    if (active.length > 1) {
      for (const task of active) {
        if (!task.featureFamily) {
          errors.push(`${task.file}:${task.line}: ${task.id} requires Feature family metadata when multiple tasks are active in ${program}`);
        }
        if (!task.parallelSafeDeclared) {
          errors.push(`${task.file}:${task.line}: ${task.id} requires Parallel-safe metadata when multiple tasks are active in ${program}`);
        }
      }
    }

    const activeByFamily = new Map();
    for (const task of active) {
      const family = task.featureFamily || `unspecified:${task.id}`;
      if (!activeByFamily.has(family)) activeByFamily.set(family, []);
      activeByFamily.get(family).push(task);
    }
    for (const [family, familyTasks] of activeByFamily) {
      if (familyTasks.length <= 1) continue;
      if (familyTasks.some((task) => !task.parallelSafe)) {
        errors.push(
          `${program}: multiple READY/IN_PROGRESS tasks in feature family '${family}' require Parallel-safe: yes on every active task (${familyTasks.map((task) => `${task.id}:${task.status}`).join(", ")})`,
        );
      }
    }

    if (active.length === 0) {
      const mapping = PROGRAM_OWNERSHIP.find((entry) => entry.program === program);
      const queuePointer = mapping && exists(root, mapping.queue)
        ? ownerQueueCurrentReady(read(root, mapping.queue), program)
        : null;
      const masterPointer = master == null ? null : masterCurrentReady(master, program);

      if (!isNonePointer(queuePointer) || !isNonePointer(masterPointer)) {
        errors.push(
          `${program}: no READY/IN_PROGRESS prompt requires explicit Current READY 'none' in both owner queue and MASTER_ROADMAP.md ` +
          `(queue=${queuePointer ?? "missing"}, master=${masterPointer ?? "missing"})`,
        );
      }
    }
  }

  if (master != null) {
    for (const mapping of PROGRAM_OWNERSHIP) {
      if (!master.includes(`| ${mapping.program} |`)) {
        errors.push(`MASTER_ROADMAP.md: missing routing row for ${mapping.program}`);
      }
      if (!master.includes(mapping.queue)) {
        errors.push(`MASTER_ROADMAP.md: missing queue link for ${mapping.program}: ${mapping.queue}`);
      }
      if (!master.includes(mapping.roadmap)) {
        errors.push(`MASTER_ROADMAP.md: missing roadmap/planning-owner link for ${mapping.program}: ${mapping.roadmap}`);
      }
    }
  }

  if (exists(root, "docs/ai/AGENT_START_HERE.md")) {
    const agent = read(root, "docs/ai/AGENT_START_HERE.md");
    if (!agent.includes("MASTER_ROADMAP.md")) errors.push("docs/ai/AGENT_START_HERE.md: missing master roadmap routing");
    if (!agent.includes("ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md")) errors.push("docs/ai/AGENT_START_HERE.md: missing Premium UI owner queue");
    if (!agent.includes("DECISION_INTELLIGENCE_PROMPT_QUEUE.md")) errors.push("docs/ai/AGENT_START_HERE.md: missing Decision Intelligence owner queue");
    if (!agent.includes("PLATFORM_EVOLUTION_PROMPT_QUEUE.md")) errors.push("docs/ai/AGENT_START_HERE.md: missing Platform Evolution owner queue");
  }

  return { errors, taskCount: tasks.length };
}

function write(root, relative, content = "# fixture\n") {
  const target = path.join(root, relative);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content, "utf8");
}

function fixtureQueue(programs) {
  return programs.map((program) => `## ${program}01 - First\n\nStatus: READY\nOwner: unassigned\nFeature family: ${program.toLowerCase()}-primary\nParallel-safe: no\n\n### Problem\nX\n\n### Evidence\nX\n\n### Scope\nX\n\n### Read first\nX\n\n### Do\nX\n\n### Tests\nX\n\n### Acceptance\nX\n\n### Dependencies\nX\n\n## ${program}02 - Later\n\nStatus: WAITING\nOwner: unassigned\nFeature family: ${program.toLowerCase()}-secondary\nParallel-safe: no\n\n### Problem\nX\n\n### Evidence\nX\n\n### Scope\nX\n\n### Read first\nX\n\n### Do\nX\n\n### Tests\nX\n\n### Acceptance\nX\n\n### Dependencies\nX\n`).join("\n");
}

function runSelfTest() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "trendplus-planning-validator-"));
  try {
    for (const relative of REQUIRED_CANONICAL_PATHS) write(root, relative);
    const decisionFixture = fixtureQueue(["DEX", "RL", "DT"]);
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", decisionFixture);
    write(root, "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md", fixtureQueue(["PERF", "OBS", "SEC"]));

    const masterRows = PROGRAM_OWNERSHIP.map((mapping) => `| ${mapping.program} | ${mapping.queue} | ${mapping.roadmap} |`).join("\n");
    const validMaster = `# Master\n${masterRows}\n${PROGRAM_OWNERSHIP.map((mapping) => `${mapping.queue}\n${mapping.roadmap}`).join("\n")}\n`;
    write(root, "MASTER_ROADMAP.md", validMaster);
    write(root, "docs/ai/AGENT_START_HERE.md", "MASTER_ROADMAP.md\nANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md\nDECISION_INTELLIGENCE_PROMPT_QUEUE.md\nPLATFORM_EVOLUTION_PROMPT_QUEUE.md\n");

    const valid = validate(root);
    if (valid.errors.length > 0) throw new Error(`valid fixture failed:\n${valid.errors.join("\n")}`);

    const zeroReadyQueue = `## Current READY by program\n\n| Program | Current READY | Execution class |\n|---|---|---|\n| DEX - Decision Explainability | none | planning |\n\n${decisionFixture.replace("## DEX01 - First\n\nStatus: READY", "## DEX01 - First\n\nStatus: DONE")}`;
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", zeroReadyQueue);
    write(root, "MASTER_ROADMAP.md", validMaster.replace(
      `| DEX | docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md | docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md |`,
      `| DEX | docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md | none | docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md |`,
    ));
    const explicitZero = validate(root);
    if (explicitZero.errors.length > 0) {
      throw new Error(`explicit zero-READY fixture failed:\n${explicitZero.errors.join("\n")}`);
    }

    write(root, "MASTER_ROADMAP.md", validMaster);
    const missingZeroDeclaration = validate(root);
    if (!missingZeroDeclaration.errors.some((error) => error.includes("no READY/IN_PROGRESS prompt requires explicit Current READY 'none'"))) {
      throw new Error("expected missing explicit zero-READY declaration failure");
    }

    write(root, "MASTER_ROADMAP.md", validMaster);
    const independentReadyQueue = fixtureQueue(["DEX", "RL", "DT"]).replace("Status: WAITING", "Status: READY");
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", independentReadyQueue);
    const independentReady = validate(root);
    if (independentReady.errors.length > 0) {
      throw new Error(`independent multi-READY fixture failed:\n${independentReady.errors.join("\n")}`);
    }

    const conflictingReadyQueue = independentReadyQueue.replace("Feature family: dex-secondary", "Feature family: dex-primary");
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", conflictingReadyQueue);
    const conflictingReady = validate(root);
    if (!conflictingReady.errors.some((error) => error.includes("DEX: multiple READY/IN_PROGRESS tasks in feature family 'dex-primary' require Parallel-safe: yes"))) {
      throw new Error("expected same-family multi-READY collision failure");
    }

    const parallelSameFamilyQueue = conflictingReadyQueue.replaceAll("Parallel-safe: no", "Parallel-safe: yes");
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", parallelSameFamilyQueue);
    const parallelSameFamily = validate(root);
    if (parallelSameFamily.errors.some((error) => error.includes("multiple READY/IN_PROGRESS tasks in feature family"))) {
      throw new Error(`parallel-safe same-family fixture failed:\n${parallelSameFamily.errors.join("\n")}`);
    }

    console.log("planning architecture validator self-test: PASS");
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
const result = validate(root);
if (result.errors.length > 0) {
  console.error(`planning architecture validation: FAIL (${result.errors.length} issue(s))`);
  for (const error of result.errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`planning architecture validation: PASS (${result.taskCount} new planning tasks checked)`);