import { describe, expect, it } from "vitest";
import { buildSupplierSalesStatsTrustProjection } from "../supplierSalesStatsTrust";

describe("supplierSalesStatsTrust", () => {
  it("projects fresh status from a valid refresh timestamp", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: true,
      lastRefreshAtUtc: "2026-07-01T07:55:00Z",
      isPartial: false,
    })).toEqual({
      lastRefreshAt: "2026-07-01T07:55:00Z",
      dataFreshnessStatus: "fresh",
      isPartial: false,
    });
  });

  it("keeps partial responses visibly stale while preserving refresh timestamp", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: true,
      lastRefreshAtUtc: "2026-07-01T07:55:00Z",
      isPartial: true,
    })).toEqual({
      lastRefreshAt: "2026-07-01T07:55:00Z",
      dataFreshnessStatus: "stale",
      isPartial: true,
    });
  });

  it("does not promote response generation time to freshness when refresh timestamp is missing", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: true,
      lastRefreshAtUtc: null,
      isPartial: false,
    })).toEqual({
      lastRefreshAt: null,
      dataFreshnessStatus: "unknown",
      isPartial: false,
    });
  });

  it("keeps malformed refresh timestamps unknown and omitted", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: true,
      lastRefreshAtUtc: "not-a-timestamp",
      isPartial: false,
    })).toEqual({
      lastRefreshAt: null,
      dataFreshnessStatus: "unknown",
      isPartial: false,
    });
  });

  it("keeps failed payloads unknown without treating their timestamp as freshness", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: false,
      lastRefreshAtUtc: "2026-07-01T07:55:00Z",
      isPartial: false,
    })).toEqual({
      lastRefreshAt: null,
      dataFreshnessStatus: "unknown",
      isPartial: false,
    });
  });

  it("keeps empty successful payloads unknown even when a timestamp exists", () => {
    expect(buildSupplierSalesStatsTrustProjection({
      success: true,
      emptyReason: "no_data_in_period",
      lastRefreshAtUtc: "2026-07-01T07:55:00Z",
      isPartial: false,
    })).toEqual({
      lastRefreshAt: "2026-07-01T07:55:00Z",
      dataFreshnessStatus: "unknown",
      isPartial: false,
    });
  });
});
