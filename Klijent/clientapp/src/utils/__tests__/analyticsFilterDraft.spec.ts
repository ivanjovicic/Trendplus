import { describe, expect, it } from "vitest";
import { getUnappliedFilterDraftChip } from "../analyticsFilterDraft";

describe("getUnappliedFilterDraftChip", () => {
  it("returns no chip when every applied filter matches the draft", () => {
    const filters = { fromDate: "2026-06-01", toDate: "2026-06-30", storeId: null };

    expect(getUnappliedFilterDraftChip(filters, filters)).toBeNull();
  });

  it("detects a changed date or dimension while keeping applied filters separate", () => {
    const active = { fromDate: "2026-06-01", toDate: "2026-06-30", storeId: null };
    const chip = { key: "draft-status", label: "Filteri", value: "Nije primenjeno", tone: "warning" };

    expect(getUnappliedFilterDraftChip({ ...active, fromDate: "2026-06-02" }, active)).toEqual(chip);
    expect(getUnappliedFilterDraftChip({ ...active, storeId: 3 }, active)).toEqual(chip);
  });

  it("detects unapplied Pre/Post vendor, category, and store selections", () => {
    const active = {
      fromDate: "2026-06-01",
      toDate: "2026-06-30",
      vendorId: null as number | null,
      category: "",
      storeId: null as number | null,
    };

    const chip = { key: "draft-status", label: "Filteri", value: "Nije primenjeno", tone: "warning" };
    expect(getUnappliedFilterDraftChip({ ...active, vendorId: 7 }, active)).toEqual(chip);
    expect(getUnappliedFilterDraftChip({ ...active, category: "Obuća" }, active)).toEqual(chip);
    expect(getUnappliedFilterDraftChip({ ...active, storeId: 3 }, active)).toEqual(chip);
  });
});
