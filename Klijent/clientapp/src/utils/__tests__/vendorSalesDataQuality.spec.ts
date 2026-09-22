import { describe, expect, it } from "vitest";
import { projectVendorSalesDataQuality } from "../vendorSalesDataQuality";

const completeQuality = {
  rawRows: 0,
  deduplicatedRows: 0,
  duplicateRowsRemoved: 0,
  inactiveRows: 0,
  unchangedPriceRows: 0,
  analyzedRows: 0,
  analyzedSharePercent: 0,
  lowPostCoverageRows: 0,
  avgCoveragePre30: null,
  avgCoveragePost30: null,
};

describe("projectVendorSalesDataQuality", () => {
  it("preserves genuine measured zero values as a complete snapshot", () => {
    const projection = projectVendorSalesDataQuality(completeQuality);

    expect(projection.isComplete).toBe(true);
    expect(projection.rawRows).toBe(0);
    expect(projection.analyzedRows).toBe(0);
    expect(projection.analyzedSharePercent).toBe(0);
  });

  it.each([
    ["omitted", undefined],
    ["null", null],
  ])("marks a %s snapshot as unknown instead of zero", (_label, value) => {
    const projection = projectVendorSalesDataQuality(value);

    expect(projection.isComplete).toBe(false);
    expect(projection.rawRows).toBeNull();
    expect(projection.analyzedRows).toBeNull();
    expect(projection.analyzedSharePercent).toBeNull();
  });

  it("marks partial and non-finite evidence as incomplete", () => {
    expect(
      projectVendorSalesDataQuality({ ...completeQuality, analyzedRows: null }).isComplete,
    ).toBe(false);
    expect(
      projectVendorSalesDataQuality({ ...completeQuality, avgCoveragePost30: Number.NaN }).isComplete,
    ).toBe(false);
    expect(
      projectVendorSalesDataQuality({ ...completeQuality, analyzedSharePercent: Number.POSITIVE_INFINITY }).isComplete,
    ).toBe(false);
  });

  it("allows absent optional coverage only when the required quality snapshot is complete", () => {
    const projection = projectVendorSalesDataQuality({ ...completeQuality, analyzedRows: 4, rawRows: 4, analyzedSharePercent: 100 });

    expect(projection.isComplete).toBe(true);
    expect(projection.avgCoveragePre30).toBeNull();
    expect(projection.avgCoveragePost30).toBeNull();
  });

  it("keeps cohort and detail truncation provenance separate from duplicate removal", () => {
    const projection = projectVendorSalesDataQuality({
      ...completeQuality,
      cohortRows: 8,
      cohortRowsExcluded: 2,
      returnedRows: 4,
      truncatedRows: 4,
      comparableRows: 6,
      comparableSharePercent: 75,
      isDetailTruncated: true,
      cohortPolicy: "latest_event_per_article",
    });

    expect(projection.duplicateRowsRemoved).toBe(0);
    expect(projection.cohortRows).toBe(8);
    expect(projection.cohortRowsExcluded).toBe(2);
    expect(projection.returnedRows).toBe(4);
    expect(projection.truncatedRows).toBe(4);
    expect(projection.comparableRows).toBe(6);
    expect(projection.comparableSharePercent).toBe(75);
    expect(projection.isDetailTruncated).toBe(true);
    expect(projection.cohortPolicy).toBe("latest_event_per_article");
  });
});
