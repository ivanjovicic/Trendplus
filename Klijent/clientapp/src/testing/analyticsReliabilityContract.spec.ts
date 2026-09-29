import {
  createFixtureAdapter,
  registerAnalyticsReliabilityContractSuite,
} from "./analyticsReliabilityContract";
import { TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE } from "../validation/analyticsRuntimeSchemaCoverage";
import { TIER1_ANALYTICS_RELIABILITY_CONTRACT_COVERAGE } from "./analyticsReliabilityContractCoverage";
import { describe, expect, it } from "vitest";

for (const entry of TIER1_ANALYTICS_RELIABILITY_CONTRACT_COVERAGE) {
  if (entry.kind === "adapter" && entry.adapterName) {
    registerAnalyticsReliabilityContractSuite(createFixtureAdapter(entry.adapterName));
  }
}

describe("Tier-1 analytics reliability contract coverage", () => {
  it("maps every runtime-schema surface to a shared adapter", () => {
    const runtimeSurfaces = new Set(TIER1_ANALYTICS_RUNTIME_SCHEMA_COVERAGE.map((entry) => entry.surface));
    const contractEntries = TIER1_ANALYTICS_RELIABILITY_CONTRACT_COVERAGE;
    const contractSurfaces = new Set(contractEntries.map((entry) => entry.surface));

    expect(new Set(contractEntries.map((entry) => entry.surface)).size).toBe(contractEntries.length);
    expect(contractSurfaces).toEqual(runtimeSurfaces);
    expect(contractEntries.every((entry) => entry.kind === "adapter" ? Boolean(entry.adapterName) : Boolean(entry.reason?.trim()))).toBe(true);
  });
});
