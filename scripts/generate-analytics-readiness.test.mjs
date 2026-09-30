import assert from "node:assert/strict";
import test from "node:test";
import { buildReadinessSnapshot, renderReadinessMarkdown } from "./generate-analytics-readiness.mjs";

const sha = "a".repeat(40);
const generations = { schema: "schema-v1", contract: "contract-v1", context: "context-v1" };

function record(overrides = {}) {
  return {
    id: "fixture-runtime",
    family: "fixture",
    scope: "runtime",
    status: "pass",
    checkedSha: sha,
    checkedAtUtc: "2026-09-30T10:00:00.000Z",
    expiresInHours: 24,
    deployedSha: sha,
    generationRefs: generations,
    ...overrides,
  };
}

test("fresh exact-SHA runtime evidence is verified-current", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record()] }, {
    repoSha: sha,
    deployedSha: sha,
    nowUtc: "2026-09-30T12:00:00.000Z",
  });
  assert.equal(snapshot.families[0].state, "verified-current");
  assert.equal(snapshot.families[0].evidence[0].state, "verified-current");
});

test("a new repository SHA downgrades old evidence to stale-evidence", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record()] }, {
    repoSha: "b".repeat(40),
    deployedSha: sha,
    nowUtc: "2026-09-30T12:00:00.000Z",
  });
  assert.equal(snapshot.families[0].state, "stale-evidence");
});

test("expired browser or schema evidence is stale", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record({ expiresInHours: 1 })] }, {
    repoSha: sha,
    deployedSha: sha,
    nowUtc: "2026-09-30T12:00:01.000Z",
  });
  assert.equal(snapshot.families[0].state, "stale-evidence");
});

test("skipped or environment-blocked proof is blocked", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record({ status: "skipped" })] }, {
    repoSha: sha,
    deployedSha: sha,
    nowUtc: "2026-09-30T12:00:00.000Z",
  });
  assert.equal(snapshot.families[0].state, "blocked");
});

test("integrity drift blocks a verified claim and renderer matches JSON state", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record({ integrityStatus: "unverified" })] }, {
    repoSha: sha,
    deployedSha: sha,
    nowUtc: "2026-09-30T12:00:00.000Z",
  });
  assert.equal(snapshot.families[0].state, "blocked");
  const markdown = renderReadinessMarkdown(snapshot);
  assert.match(markdown, /Overall state: \*\*blocked\*\*/);
  assert.match(markdown, /\| fixture \| \*\*blocked\*\*/);
});

test("current code evidence is code-ready while runtime remains unproven", () => {
  const snapshot = buildReadinessSnapshot({ currentGenerations: generations, evidence: [record({
    id: "fixture-code",
    scope: "code",
    deployedSha: null,
  })] }, {
    repoSha: sha,
    deployedSha: null,
    nowUtc: "2026-09-30T12:00:00.000Z",
  });
  assert.equal(snapshot.families[0].state, "code-ready");
});
