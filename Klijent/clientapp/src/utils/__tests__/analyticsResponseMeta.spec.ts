import { describe, expect, it } from "vitest";
import type { AnalyticsResponseMeta } from "../../types/analytics";
import {
  AnalyticsMetaError,
  assertAnalyticsMetaSuccess,
  getAnalyticsMetaMessage,
  hasAnalyticsMetaEmptyReason,
  isAnalyticsMetaEmpty,
  isAnalyticsMetaError,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../analyticsResponseMeta";

describe("analyticsResponseMeta", () => {
  it("success meta does not throw", () => {
    const payload = { meta: { success: true } as AnalyticsResponseMeta, value: 123 };
    const result = assertAnalyticsMetaSuccess(payload, (response) => response.meta, "context");
    expect(result.value).toBe(123);
  });

  it("success=false throws AnalyticsMetaError with details", () => {
    const payload = {
      meta: {
        success: false,
        errorCode: "sql_timeout",
        correlationId: "cid-1",
        errorMessage: "Timeout",
      } as AnalyticsResponseMeta,
    };

    expect(() =>
      assertAnalyticsMetaSuccess(payload, (response) => response.meta, "test-context")
    ).toThrow(AnalyticsMetaError);

    try {
      assertAnalyticsMetaSuccess(payload, (response) => response.meta, "test-context");
    } catch (reason) {
      const err = reason as AnalyticsMetaError;
      expect(err.errorCode).toBe("sql_timeout");
      expect(err.correlationId).toBe("cid-1");
      expect(err.context).toBe("test-context");
    }
  });

  it("warning meta is classified as warning", () => {
    expect(isAnalyticsMetaWarning({ success: true, warningCode: "PARTIAL" })).toBe(true);
    expect(isAnalyticsMetaWarning({ success: true, isPartial: true })).toBe(true);
  });

  it("emptyReason response is classified as empty", () => {
    expect(isAnalyticsMetaEmpty({ success: true, emptyReason: "no_data_in_period" })).toBe(true);
  });

  it("insufficient_data is not treated as empty by default", () => {
    const meta: AnalyticsResponseMeta = { success: true, dataQualityStatus: "insufficient_data" };
    expect(isAnalyticsMetaInsufficient(meta)).toBe(true);
    expect(isAnalyticsMetaEmpty(meta)).toBe(false);
  });

  it("empty state helper separates insufficient signal from true empty data", () => {
    const insufficientMeta: AnalyticsResponseMeta = {
      success: true,
      dataQualityStatus: "insufficient_data",
    };
    const emptyMeta: AnalyticsResponseMeta = { success: true, emptyReason: "no_data_in_period" };

    expect(shouldShowAnalyticsEmptyState(insufficientMeta, 0)).toBe(true);
    expect(shouldShowAnalyticsEmptyState(insufficientMeta, 5)).toBe(false);
    expect(shouldShowAnalyticsEmptyState(emptyMeta, 5)).toBe(false);
    expect(shouldShowAnalyticsEmptyState(emptyMeta, 5, { allowEmptyReasonWithRows: true })).toBe(true);
    expect(shouldShowAnalyticsEmptyState(emptyMeta, 0)).toBe(true);
  });

  it("Product Decision regression: insufficient_data with rows>0 does not hide table", () => {
    const meta: AnalyticsResponseMeta = {
      success: true,
      dataQualityStatus: "insufficient_data",
      emptyReason: "no_rows_for_period",
    };

    expect(shouldShowAnalyticsEmptyState(meta, 3)).toBe(false);
  });

  it("has empty reason helper returns true only for success + emptyReason", () => {
    expect(hasAnalyticsMetaEmptyReason({ success: true, emptyReason: "no_data_in_period" })).toBe(true);
    expect(hasAnalyticsMetaEmptyReason({ success: false, emptyReason: "no_data_in_period" })).toBe(false);
    expect(hasAnalyticsMetaEmptyReason({ success: true })).toBe(false);
  });

  it("missing meta is neutral and does not throw", () => {
    const payload = { value: "ok" };
    const result = assertAnalyticsMetaSuccess(payload, () => null, "neutral-context");
    expect(result.value).toBe("ok");
    expect(isAnalyticsMetaError(undefined)).toBe(false);
    expect(isAnalyticsMetaEmpty(undefined)).toBe(false);
    expect(isAnalyticsMetaWarning(undefined)).toBe(false);
  });

  it("message priority uses error, warning, message, then empty reason mapping", () => {
    expect(getAnalyticsMetaMessage({ success: false, errorMessage: "err" })).toBe("err");
    expect(getAnalyticsMetaMessage({ success: true, warningMessage: "warn" })).toBe("warn");
    expect(getAnalyticsMetaMessage({ success: true, message: "msg" })).toBe("msg");
    expect(getAnalyticsMetaMessage({ success: true, emptyReason: "no_data_in_period" })).toBe(
      "Nema podataka za izabrani period."
    );
  });
});
