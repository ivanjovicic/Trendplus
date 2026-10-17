#!/usr/bin/env node
/**
 * Guard relative Markdown links in current (non-archived) documentation.
 *
 * Scope: tracked *.md files outside historical locations (.ai/runs/**, docs/archive/**).
 * A link target must be a tracked file or a directory that contains tracked files
 * (case-sensitive, so Windows-only casing mistakes fail the same way they fail on GitHub).
 * Existing historical breakage is frozen in scripts/doc-links-baseline.txt; only NEW broken
 * links fail. Baseline entries that are fixed are reported so the baseline can shrink.
 */

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const BASELINE = "scripts/doc-links-baseline.txt";
const EXCLUDED_SOURCE_PREFIXES = [".ai/runs/", "docs/archive/"];
// Intentionally deleted / ephemeral targets that are referenced by protocol text.
const IGNORED_TARGET_PATTERNS = [/\.lock(\.md)?$/i, /(^|\/)\.ai\/(task-)?locks(\/|$)/i, /(^|\/)node_modules(\/|$)/];

function isExcludedSource(file) {
  return EXCLUDED_SOURCE_PREFIXES.some((prefix) => file.startsWith(prefix));
}

function stripCode(content) {
  // Drop fenced code blocks and inline code so examples are not treated as links.
  return content
    .replace(/^(```|~~~)[^\n]*\n[\s\S]*?^\1[^\n]*$/gm, "")
    .replace(/`[^`\n]*`/g, "");
}

export function extractLinks(content) {
  const links = [];
  const text = stripCode(content);
  const re = /!?\[[^\]\n]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
  let match;
  while ((match = re.exec(text)) !== null) links.push(match[1]);
  return links;
}

function isExternal(target) {
  return /^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith("//") || target.startsWith("#");
}

function normalizeTarget(sourceFile, target) {
  let clean = target.split("#")[0].split("?")[0];
  if (!clean) return null;
  try {
    clean = decodeURIComponent(clean);
  } catch {
    // keep raw
  }
  const base = clean.startsWith("/") ? "" : path.posix.dirname(sourceFile);
  const joined = path.posix.normalize(path.posix.join(base, clean.replace(/^\/+/, "")));
  return joined.replace(/\/$/, "");
}

export function validate(root, trackedFiles, baselineLines) {
  const tracked = new Set(trackedFiles);
  const dirs = new Set();
  for (const file of trackedFiles) {
    let dir = path.posix.dirname(file);
    while (dir && dir !== "." && !dirs.has(dir)) {
      dirs.add(dir);
      dir = path.posix.dirname(dir);
    }
  }
  const baseline = new Set(baselineLines.map((line) => line.trim()).filter((line) => line && !line.startsWith("#")));
  const broken = [];
  for (const file of trackedFiles) {
    if (!file.toLowerCase().endsWith(".md") || isExcludedSource(file)) continue;
    const full = path.join(root, file);
    if (!fs.existsSync(full)) continue;
    const content = fs.readFileSync(full, "utf8");
    for (const target of extractLinks(content)) {
      if (isExternal(target)) continue;
      const resolved = normalizeTarget(file, target);
      if (resolved === null || resolved === "" || resolved === ".") continue;
      if (resolved.startsWith("..")) {
        broken.push(`${file} -> ${target}`);
        continue;
      }
      if (IGNORED_TARGET_PATTERNS.some((pattern) => pattern.test(resolved))) continue;
      if (tracked.has(resolved) || dirs.has(resolved)) continue;
      broken.push(`${file} -> ${target}`);
    }
  }
  const unique = [...new Set(broken)];
  const errors = unique.filter((entry) => !baseline.has(entry));
  const fixed = [...baseline].filter((entry) => !unique.includes(entry));
  return { errors, fixed, total: unique.length };
}

function listTracked(root) {
  const output = execFileSync("git", ["ls-files", "-z"], { cwd: root, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  return output.split("\0").filter(Boolean);
}

function readBaseline(root) {
  const full = path.join(root, BASELINE);
  return fs.existsSync(full) ? fs.readFileSync(full, "utf8").split(/\r?\n/) : [];
}

function write(root, file, content) {
  const full = path.join(root, file);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content);
}

function selfTest() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "doc-links-"));
  try {
    const files = ["README.md", "docs/a.md", "docs/sub/b.md", "docs/archive/old.md", ".ai/runs/r.md", "Api/Program.cs"];
    write(root, "Api/Program.cs", "");
    write(root, "docs/sub/b.md", "[up](../a.md) [dir](../../Api) [anchor](#x) [ext](https://example.com)\n");
    write(root, "docs/archive/old.md", "[gone](missing.md)\n");
    write(root, ".ai/runs/r.md", "[gone](missing.md)\n");
    write(root, "docs/a.md", "[ok](sub/b.md#top) [lock](../.ai/task-locks/RQ1.lock.md)\n```\n[code](nope.md)\n```\n`[inline](nope.md)`\n");
    write(root, "README.md", "[ok](docs/a.md) [abs](/docs/sub/b.md)\n");

    let result = validate(root, files, []);
    if (result.errors.length !== 0) throw new Error(`valid tree failed: ${result.errors.join("; ")}`);

    write(root, "README.md", "[bad](docs/A.md) [missing](docs/nope.md)\n");
    result = validate(root, files, []);
    if (result.errors.length !== 2) throw new Error(`expected 2 broken links (case + missing), got ${result.errors.length}`);

    result = validate(root, files, ["README.md -> docs/A.md", "README.md -> docs/nope.md", "docs/x.md -> gone.md"]);
    if (result.errors.length !== 0) throw new Error("baseline entries must not fail");
    if (result.fixed.length !== 1) throw new Error("fixed baseline entry must be reported");

    write(root, "README.md", "[escape](../outside.md)\n");
    result = validate(root, files, []);
    if (result.errors.length !== 1) throw new Error("link escaping repository must fail");

    console.log("doc link validator self-test: PASS");
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
}

const args = process.argv.slice(2);
if (args.includes("--self-test")) {
  selfTest();
  process.exit(0);
}

const root = process.cwd();
const tracked = listTracked(root);
if (args.includes("--write-baseline")) {
  const { errors } = validate(root, tracked, []);
  const header = "# Frozen pre-existing broken relative Markdown links (source -> target).\n# Shrink this list when fixing; never add new entries to silence a fresh break.\n";
  fs.writeFileSync(path.join(root, BASELINE), header + errors.sort().join("\n") + (errors.length ? "\n" : ""));
  console.log(`doc link baseline written: ${errors.length} entries`);
  process.exit(0);
}
const { errors, fixed, total } = validate(root, tracked, readBaseline(root));
for (const entry of fixed) console.log(`baseline entry no longer broken (remove it): ${entry}`);
if (errors.length) {
  console.error("Doc link validation failed (new broken relative links):");
  for (const error of errors) console.error(`- ${error}`);
  process.exit(1);
}
console.log(`doc link validation passed (${total} baselined historical breaks, ${fixed.length} fixable baseline entries)`);
