#!/usr/bin/env node
/**
 * Trendplus planning architecture validator.
 *
 * Complements scripts/check-prompt-queues.mjs by validating the consolidated
 * master roadmap, roadmap/queue ownership, and the new DEX/RL/DT/PERF/OBS/SEC
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
        sections: new Set(),
      };
      continue;
    }
    if (!current) continue;

    const status = line.match(/^Status:\s*`?([A-Za-z_]+)`?\s*$/i);
    if (status && current.status == null) current.status = status[1].toUpperCase();

    const owner = line.match(/^Owner:\s*(.+)$/i);
    if (owner && current.owner == null) current.owner = owner[1].trim();

    const section = line.match(/^###\s+(.+?)\s*$/);
    if (section) current.sections.add(section[1].trim());
  }
  if (current) tasks.push(current);
  return tasks;
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

  for (const program of NEW_PROGRAMS) {
    const programTasks = tasks.filter((task) => task.program === program);
    if (programTasks.length === 0) {
      errors.push(`${program}: no prompt found in new planning queues`);
      continue;
    }
    const ready = programTasks.filter((task) => task.status === "READY");
    if (ready.length !== 1) {
      errors.push(`${program}: expected exactly one READY prompt, found ${ready.length}`);
    }
    const laterReady = programTasks.slice(1).filter((task) => task.status === "READY");
    if (laterReady.length > 0) {
      errors.push(`${program}: later prompt(s) unexpectedly READY: ${laterReady.map((task) => task.id).join(", ")}`);
    }
  }

  if (exists(root, "MASTER_ROADMAP.md")) {
    const master = read(root, "MASTER_ROADMAP.md");
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
  return programs.map((program) => `## ${program}01 - First\n\nStatus: READY\nOwner: unassigned\n\n### Problem\nX\n\n### Evidence\nX\n\n### Scope\nX\n\n### Read first\nX\n\n### Do\nX\n\n### Tests\nX\n\n### Acceptance\nX\n\n### Dependencies\nX\n\n## ${program}02 - Later\n\nStatus: WAITING\nOwner: unassigned\n\n### Problem\nX\n\n### Evidence\nX\n\n### Scope\nX\n\n### Read first\nX\n\n### Do\nX\n\n### Tests\nX\n\n### Acceptance\nX\n\n### Dependencies\nX\n`).join("\n");
}

function runSelfTest() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "trendplus-planning-validator-"));
  try {
    for (const relative of REQUIRED_CANONICAL_PATHS) write(root, relative);
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", fixtureQueue(["DEX", "RL", "DT"]));
    write(root, "docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md", fixtureQueue(["PERF", "OBS", "SEC"]));

    const masterRows = PROGRAM_OWNERSHIP.map((mapping) => `| ${mapping.program} | ${mapping.queue} | ${mapping.roadmap} |`).join("\n");
    write(root, "MASTER_ROADMAP.md", `# Master\n${masterRows}\n${PROGRAM_OWNERSHIP.map((mapping) => `${mapping.queue}\n${mapping.roadmap}`).join("\n")}\n`);
    write(root, "docs/ai/AGENT_START_HERE.md", "MASTER_ROADMAP.md\nANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md\nDECISION_INTELLIGENCE_PROMPT_QUEUE.md\nPLATFORM_EVOLUTION_PROMPT_QUEUE.md\n");

    const valid = validate(root);
    if (valid.errors.length > 0) throw new Error(`valid fixture failed:\n${valid.errors.join("\n")}`);

    const badQueue = fixtureQueue(["DEX", "RL", "DT"]).replace("Status: WAITING", "Status: READY");
    write(root, "docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md", badQueue);
    const duplicateReady = validate(root);
    if (!duplicateReady.errors.some((error) => error.includes("DEX: expected exactly one READY"))) {
      throw new Error("expected duplicate READY failure");
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
