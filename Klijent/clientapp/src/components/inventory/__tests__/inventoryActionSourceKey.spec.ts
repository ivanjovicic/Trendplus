import { describe, expect, it } from "vitest";
import { buildInventoryActionSourceKey, buildInventoryWorkflowCentralQueueMetadata, inventoryActionSourceKeySchemaVersion } from "../inventoryUtils";

const baseContext = {
  dataScope: "all",
  periodFrom: "2026-09-01T00:00:00.000Z",
  periodTo: "2026-10-01T00:00:00.000Z",
  snapshotGeneration: "snapshot-17",
  storeId: 12,
  sizeCode: "42",
};

describe("Inventory action source-key contract", () => {
  it("is stable for repeated clicks in the same dataset context", () => {
    const first = buildInventoryActionSourceKey("dopuna", 501, baseContext);
    const second = buildInventoryActionSourceKey("dopuna", 501, { ...baseContext });

    expect(first).toBe(second);
    expect(first).toBe(
      "inventory|v2|kind=dopuna|article=501|store=12|size=42|fromStore=all|toStore=all|scope=all|periodFrom=2026-09-01t00%3A00%3A00.000z|periodTo=2026-10-01t00%3A00%3A00.000z|snapshot=snapshot-17",
    );
  });

  it.each([
    ["data scope", { dataScope: "existing" }],
    ["period", { periodFrom: "2026-09-02T00:00:00.000Z" }],
    ["snapshot generation", { snapshotGeneration: "snapshot-18" }],
  ])("does not deduplicate a changed %s", (_label, change) => {
    const changed = buildInventoryActionSourceKey("dopuna", 501, { ...baseContext, ...change });

    expect(changed).not.toBe(buildInventoryActionSourceKey("dopuna", 501, baseContext));
  });

  it("keeps unavailable provenance explicit instead of collapsing it into a known context", () => {
    const unknown = buildInventoryActionSourceKey("dopuna", 501, {
      dataScope: "all",
      periodFrom: "rolling-30d",
      periodTo: "rolling-30d",
      snapshotGeneration: null,
      storeId: 12,
    });

    expect(unknown).toContain("snapshot=unknown");
    expect(unknown).not.toContain("snapshot=latest");
  });

  it("keeps legacy workflow keys readable without labeling them as v2", () => {
    const legacyKey = "dopuna|SKU-501|12|0";
    expect(inventoryActionSourceKeySchemaVersion(legacyKey)).toBe("legacy");
    expect(buildInventoryWorkflowCentralQueueMetadata({
      suggestionKey: legacyKey,
      actionType: "dopuna",
      priority: "high",
      label: "Legacy",
      reason: "Legacy action",
      status: "pending",
      artikalId: 501,
      plu: "SKU-501",
      naziv: "Test",
      fromStoreName: null,
      toStoreName: null,
      suggestedQty: 1,
      estimatedValue: null,
      daysSinceMovement: 1,
    })).toMatchObject({
      sourceKeySchemaVersion: "legacy",
      legacySourceKey: legacyKey,
    });
  });
});
