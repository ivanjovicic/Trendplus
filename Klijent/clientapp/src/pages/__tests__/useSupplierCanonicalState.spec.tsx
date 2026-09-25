import { act, renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { useSupplierCanonicalState } from "../useSupplierCanonicalState";

const LEGACY_SEASON_URL = "/analytics/supplier?tab=overview&legacySource=operations-supplier-sales&sezonaId=7&fromDate=2026-01-01&toDate=2026-03-31&storeId=3";

function renderCanonicalState(initialEntry = LEGACY_SEASON_URL) {
  const location = { search: "" };
  function LocationProbe() {
    location.search = useLocation().search;
    return null;
  }
  const wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={[initialEntry]}>
      <LocationProbe />
      {children}
    </MemoryRouter>
  );
  const hook = renderHook(() => useSupplierCanonicalState(), { wrapper });
  return { ...hook, params: () => new URLSearchParams(location.search) };
}

describe("useSupplierCanonicalState legacy season override", () => {
  it("drops the legacy sezonaId when a period preset is chosen", () => {
    const { result, params } = renderCanonicalState();
    expect(params().get("sezonaId")).toBe("7");

    act(() => result.current.setPreset("90d"));

    expect(params().has("sezonaId")).toBe(false);
    expect(params().get("periodPreset")).toBe("90d");
    expect(params().get("storeId")).toBe("3");
    expect(params().get("legacySource")).toBe("operations-supplier-sales");
  });

  it("drops the legacy sezonaId when a custom date is typed", () => {
    const { result, params } = renderCanonicalState();

    act(() => result.current.setDate("toDate", "2026-04-15"));

    expect(params().has("sezonaId")).toBe(false);
    expect(params().get("toDate")).toBe("2026-04-15");
    expect(params().get("fromDate")).toBe("2026-01-01");
    expect(params().get("periodPreset")).toBe("custom");
  });

  it("keeps the legacy sezonaId for non-period filters", () => {
    const { result, params } = renderCanonicalState();

    act(() => result.current.setSupplier("11"));

    expect(params().get("sezonaId")).toBe("7");
    expect(params().get("supplierId")).toBe("11");
  });
});
