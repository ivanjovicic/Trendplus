import { describe, expect, it } from "vitest";
import {
  ANALYTICS_STATE_TAXONOMY,
  getAnalyticsFailureCode,
  isKnownAnalyticsStateCode,
  resolveAnalyticsState,
} from "../analyticsStateTaxonomy";

describe("analytics state taxonomy", () => {
  it("defines each design-system state with copy, tone, kind, and an action when one applies", () => {
    for (const [code, definition] of Object.entries(ANALYTICS_STATE_TAXONOMY)) {
      expect(definition.kind, code).toMatch(/^(empty|error|loading)$/);
      expect(definition.tone, code).toMatch(/^(neutral|warning|info|error)$/);
      expect(definition.title.trim(), code).not.toBe("");
      expect(definition.message.trim(), code).not.toBe("");
    }
  });

  it.each([
    ["no_data_in_period", "empty_no_data"],
    ["no_sales_in_period", "empty_no_data"],
    ["no_supplier_sales", "empty_no_data"],
    ["filtered_out", "empty_filtered_out"],
    ["insufficient_data", "insufficient_data"],
    ["source_dimension_not_populated", "source_dimension_not_populated"],
    ["STALE_CACHE", "partial"],
    ["MISSING_OBJECT", "schema_mismatch"],
    ["vendor_sales_nivelacija_contract_missing", "schema_mismatch"],
  ])("maps backend code %s to %s", (source, expected) => {
    expect(resolveAnalyticsState(source).code).toBe(expected);
    expect(isKnownAnalyticsStateCode(source)).toBe(true);
  });

  it("reserves horizon and dimension codes but does not infer them from metadata without a code", () => {
    expect(resolveAnalyticsState("beyond_source_horizon").code).toBe("beyond_source_horizon");
    expect(resolveAnalyticsState("source_dimension_not_populated").code).toBe("source_dimension_not_populated");
    expect(resolveAnalyticsState(undefined, { success: true, dataQualityStatus: "warning" }).definition).toBeNull();
  });

  it("uses successful backend metadata before compatibility variants and preserves unknown codes for details", () => {
    expect(resolveAnalyticsState(undefined, { success: true, emptyReason: "no_data_in_period" }, "filtered_out").code)
      .toBe("empty_no_data");
    expect(resolveAnalyticsState("future_reason_v4")).toMatchObject({
      code: "unknown_code",
      rawCode: "future_reason_v4",
    });
    expect(resolveAnalyticsState(undefined, { success: true, isPartial: true, warningCode: "future_partial_v4" }))
      .toMatchObject({ code: "partial", rawCode: "future_partial_v4" });
    expect(resolveAnalyticsState(undefined, { success: false, isPartial: true }))
      .toMatchObject({ code: "error_retryable" });
  });

  it("classifies validation and fetch failures without exposing their technical messages", () => {
    expect(getAnalyticsFailureCode({ name: "AnalyticsResponseValidationError" })).toBe("schema_mismatch");
    expect(getAnalyticsFailureCode(new TypeError("Failed to fetch"))).toBe("backend_unreachable");
    expect(getAnalyticsFailureCode(new Error("internal exception"))).toBe("error_retryable");
  });
});
