#!/usr/bin/env node
/**
 * Lightweight consistency checks for Trendplus agent/development instructions.
 *
 * Usage:
 *   node scripts/check-agent-instructions.mjs
 *   node scripts/check-agent-instructions.mjs --self-test
 *   node scripts/check-agent-instructions.mjs --root <dir>
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const REQUIRED_SNIPPETS = new Map([
  ["AGENTS.md", ["direct repository request", "MASTER_ROADMAP.md", "historical ledger", "VALIDATION_SELECTOR.md"]],
  [".github/copilot-instructions.md", ["AGENT_START_HERE.md", "VALIDATION_SELECTOR.md", "najužu proveru"]],
  ["docs/ai/REPO_AI_README.md", ["Authority order when docs conflict", "Canonical owners by topic", "VALIDATION_SELECTOR.md"]],
  ["docs/ai/AGENT_START_HERE.md", ["Direct task workflow", "Queue task workflow", "VALIDATION_SELECTOR.md", "historical ledger"]],
  ["docs/ai/PROMPT_QUEUE_PROTOCOL.md", ["Mechanical prompt conflicts", "same-owner", "VALIDATION_SELECTOR.md"]],
  ["docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md", ["exact delivered SHA", "Main commit SHA", "Main verification", "RUN_LOG_TEMPLATE.md"]],
  [".ai/RUN_LOG_TEMPLATE.md", ["What was done", "What was missed", "Risks", "Next"]],
  ["docs/ai/VALIDATION_SELECTOR.md", ["React and analytics UI", ".NET API, application and infrastructure", "Workers, refresh and scheduled jobs", "Queue and planning changes"]],
]);

const INVALID_LIVE_STATUS_RE = /^Status:\s*`?(TODO|OPEN|COMPLETE|COMPLETED)`?\s*$/gim;
const MARKDOWN_LINK_RE = /\[[^\]]*\]\(([^)]+)\)/g;

function normalize(value) {
  return value.replace(/\\/g, "/");
}

function read(root, relative) {
  return fs.readFileSync(path.join(root, relative), "utf8");
}

function validateLinks(root, relative, content) {
  const errors = [];
  for (const match of content.matchAll(MARKDOWN_LINK_RE)) {
    let target = match[1].trim();
    if (!target || target.startsWith("#") || /^(?:https?:|mailto:)/i.test(target)) continue;
    target = target.split("#", 1)[0].split("?", 1)[0];
    if (target.startsWith("<") && target.endsWith(">")) target = target.slice(1, -1);
    try {
      target = decodeURIComponent(target);
    } catch {
      errors.push(`${relative}: malformed encoded link '${match[1]}'`);
      continue;
    }
    const absolute = path.resolve(root, path.dirname(relative), target);
    if (!fs.existsSync(absolute)) errors.push(`${relative}: broken relative link '${match[1]}'`);
  }
  return errors;
}

function validate(root) {
  const errors = [];

  for (const [relative, snippets] of REQUIRED_SNIPPETS) {
    const absolute = path.join(root, relative);
    if (!fs.existsSync(absolute)) {
      errors.push(`${relative}: missing canonical instruction file`);
      continue;
    }

    const content = read(root, relative);
    for (const snippet of snippets) {
      if (!content.toLowerCase().includes(snippet.toLowerCase())) {
        errors.push(`${relative}: missing consistency marker '${snippet}'`);
      }
    }

    if (INVALID_LIVE_STATUS_RE.test(content)) {
      errors.push(`${relative}: contains unsupported live status (TODO|OPEN|COMPLETE|COMPLETED)`);
    }
    INVALID_LIVE_STATUS_RE.lastIndex = 0;

    if (content.includes("NEXT_PROMPT_QUEUE.md") && !content.toLowerCase().includes("historical ledger")) {
      errors.push(`${relative}: references NEXT_PROMPT_QUEUE.md without declaring it a historical ledger`);
    }

    errors.push(...validateLinks(root, relative, content));
  }

  return errors;
}

function write(root, relative, content) {
  const absolute = path.join(root, relative);
  fs.mkdirSync(path.dirname(absolute), { recursive: true });
  fs.writeFileSync(absolute, content, "utf8");
}

function runSelfTest() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "trendplus-agent-instructions-"));
  try {
    for (const [relative, snippets] of REQUIRED_SNIPPETS) {
      write(root, relative, `${snippets.join("\n")}\n`);
    }
    write(root, "docs/ai/example.md", "# Example\n");
    fs.appendFileSync(path.join(root, "AGENTS.md"), "\n[Example](docs/ai/example.md)\n", "utf8");

    const valid = validate(root);
    if (valid.length > 0) throw new Error(`valid fixture failed:\n${valid.join("\n")}`);

    fs.appendFileSync(path.join(root, "AGENTS.md"), "\nStatus: TODO\n[Missing](docs/ai/missing.md)\n", "utf8");
    const invalid = validate(root);
    if (!invalid.some((error) => error.includes("unsupported live status"))) {
      throw new Error("expected stale live-status failure");
    }
    if (!invalid.some((error) => error.includes("broken relative link"))) {
      throw new Error("expected broken-link failure");
    }

    console.log("agent instruction validator self-test: PASS");
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
  console.error(`agent instruction validation: FAIL (${errors.length} issue(s))`);
  for (const error of errors) console.error(`- ${normalize(error)}`);
  process.exit(1);
}

console.log(`agent instruction validation: PASS (${REQUIRED_SNIPPETS.size} canonical files checked)`);
