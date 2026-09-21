import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { renderEvidence } from "./render-task-validation-evidence.mjs";
import { runValidationPlan, verifyMain } from "./run-task-validation.mjs";

const node = process.execPath;

test("validation plan preserves pass, fail, timeout, skipped and environment-blocked outcomes", async () => {
  const evidence = await runValidationPlan([
    { name: "pass", category: "tests", command: `${node} -e "process.exit(0)"` },
    { name: "fail", category: "tests", command: `${node} -e "process.exit(3)"` },
    { name: "timeout", category: "build", command: "sleep 1", timeoutMs: 25 },
    { name: "skipped", category: "guardrails", skipReason: "not required in this fixture" },
    { name: "blocked", category: "build", environmentBlockedReason: "dotnet unavailable" },
  ], { cwd: process.cwd(), taskId: "fixture" });

  assert.equal(evidence.tests.results[0].status, "pass");
  assert.equal(evidence.tests.results[1].status, "fail");
  assert.equal(evidence.tests.results[1].exitCode, 3);
  assert.equal(evidence.build.results[0].status, "timeout");
  assert.equal(evidence.guardrails.results[0].status, "skipped");
  assert.equal(evidence.build.results[1].status, "environment-blocked");
  assert.equal(evidence.exitCodes.fail, 3);
  assert.equal(evidence.exitCodes.skipped, null);
  assert.deepEqual(evidence.skipped, [{ name: "skipped", reason: "not required in this fixture" }]);
});

test("renderer never turns a non-zero command into PASS", () => {
  const markdown = renderEvidence({
    schemaVersion: 1,
    taskId: "fixture",
    commit: "abc",
    timestamp: "2026-09-21T00:00:00.000Z",
    tests: {
      status: "fail",
      results: [{
        name: "failing test",
        command: "node failing-test.mjs",
        status: "fail",
        exitCode: 2,
        summary: "assertion failed",
      }],
    },
    guardrails: { status: "not-run", results: [] },
    build: { status: "not-run", results: [] },
    exitCodes: { "failing test": 2 },
    skipped: [],
  });

  assert.match(markdown, /failing test: \*\*FAIL\*\*; exitCode=2/);
  assert.doesNotMatch(markdown, /failing test: \*\*PASS\*\*/);
});

test("main verification distinguishes exact tip and ancestor commits", async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), "trendplus-validation-git-"));
  try {
    execFileSync("git", ["init", "-q", "-b", "main"], { cwd });
    execFileSync("git", ["config", "user.email", "test@example.invalid"], { cwd });
    execFileSync("git", ["config", "user.name", "Validation Test"], { cwd });
    await writeFile(path.join(cwd, "fixture.txt"), "one\n");
    execFileSync("git", ["add", "fixture.txt"], { cwd });
    execFileSync("git", ["commit", "-qm", "one"], { cwd });
    const firstCommit = execFileSync("git", ["rev-parse", "HEAD"], { cwd, encoding: "utf8" }).trim();
    execFileSync("git", ["update-ref", "refs/remotes/origin/main", firstCommit], { cwd });

    const exact = verifyMain(firstCommit, { cwd, mainRef: "refs/remotes/origin/main" });
    assert.equal(exact.status, "exact-tip");
    assert.equal(exact.containsCommit, true);

    await writeFile(path.join(cwd, "fixture.txt"), "two\n");
    execFileSync("git", ["add", "fixture.txt"], { cwd });
    execFileSync("git", ["commit", "-qm", "two"], { cwd });
    const secondCommit = execFileSync("git", ["rev-parse", "HEAD"], { cwd, encoding: "utf8" }).trim();
    execFileSync("git", ["update-ref", "refs/remotes/origin/main", secondCommit], { cwd });
    const ancestor = verifyMain(firstCommit, { cwd, mainRef: "refs/remotes/origin/main" });
    assert.equal(ancestor.status, "ancestor");
    assert.equal(ancestor.containsCommit, true);
  } finally {
    await rm(cwd, { recursive: true, force: true });
  }
});

test("CLI writes JSON evidence and exits non-zero for a failed command", async () => {
  const cwd = await mkdtemp(path.join(os.tmpdir(), "trendplus-validation-cli-"));
  try {
    const planPath = path.join(cwd, "plan.json");
    const outputPath = path.join(cwd, "evidence.json");
    await writeFile(planPath, JSON.stringify({
      taskId: "cli-fixture",
      commands: [{ name: "failure", category: "tests", command: `${node} -e "process.exit(4)"` }],
    }));
    const result = spawnSync(node, [
      "scripts/run-task-validation.mjs",
      "--plan", planPath,
      "--output", outputPath,
      "--cwd", process.cwd(),
    ], { cwd: process.cwd(), encoding: "utf8" });
    assert.equal(result.status, 1);
    const evidence = JSON.parse(await (await import("node:fs/promises")).readFile(outputPath, "utf8"));
    assert.equal(evidence.exitCodes.failure, 4);
    assert.equal(evidence.tests.status, "fail");
  } finally {
    await rm(cwd, { recursive: true, force: true });
  }
});
