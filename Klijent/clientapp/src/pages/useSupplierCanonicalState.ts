import { useEffect, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { getPresetRange } from "../utils/analyticsFormatters";
import { getDataScope, normalizeDataScope, setDataScope as persistDataScope } from "../utils/dataScope";
import type { SupplierCanonicalFilters, SupplierPeriodPreset, SupplierTab } from "./supplierSharedState";
import { SUPPLIER_TABS } from "./supplierSharedState";

function parseNullableInt(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : null;
}

function parseDateOrDefault(value: string | null, fallback: string): string {
  if (!value) return fallback;
  const normalized = value.slice(0, 10);
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : fallback;
}

function parseTab(value: string | null): SupplierTab {
  return value && SUPPLIER_TABS.includes(value as SupplierTab) ? (value as SupplierTab) : "overview";
}

function parsePreset(value: string | null, hasDateQuery: boolean): SupplierPeriodPreset {
  if (value === "30d" || value === "90d" || value === "180d" || value === "365d" || value === "custom") {
    return value;
  }
  return hasDateQuery ? "custom" : "30d";
}

export function useSupplierCanonicalState() {
  const [searchParams, setSearchParams] = useSearchParams();
  const defaultRange = useMemo(() => getPresetRange("30d"), []);
  const currentTab = parseTab(searchParams.get("tab"));
  const hasDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");

  const canonicalFilters = useMemo<SupplierCanonicalFilters>(() => ({
    periodPreset: parsePreset(searchParams.get("periodPreset"), hasDateQuery),
    fromDate: parseDateOrDefault(searchParams.get("fromDate"), defaultRange.fromDate),
    toDate: parseDateOrDefault(searchParams.get("toDate"), defaultRange.toDate),
    dataScope: normalizeDataScope(searchParams.get("dataScope") ?? getDataScope()),
    storeId: parseNullableInt(searchParams.get("storeId")),
    supplierId: parseNullableInt(searchParams.get("supplierId")),
  }), [defaultRange.fromDate, defaultRange.toDate, hasDateQuery, searchParams]);

  const invalidRange = useMemo(
    () => new Date(canonicalFilters.fromDate) > new Date(canonicalFilters.toDate),
    [canonicalFilters.fromDate, canonicalFilters.toDate]
  );

  useEffect(() => {
    persistDataScope(normalizeDataScope(canonicalFilters.dataScope));
  }, [canonicalFilters.dataScope]);

  const updateParams = (recipe: (next: URLSearchParams) => void) => {
    const next = new URLSearchParams(searchParams);
    recipe(next);
    if (!next.get("tab")) next.set("tab", currentTab);
    setSearchParams(next);
  };

  const setTab = (tab: SupplierTab) => {
    updateParams((next) => { next.set("tab", tab); });
  };

  const setPreset = (preset: SupplierPeriodPreset) => {
    updateParams((next) => {
      next.set("periodPreset", preset);
      if (preset !== "custom") {
        const range = getPresetRange(preset);
        next.set("fromDate", range.fromDate);
        next.set("toDate", range.toDate);
      }
    });
  };

  const setDate = (key: "fromDate" | "toDate", value: string) => {
    updateParams((next) => {
      next.set(key, value);
      next.set("periodPreset", "custom");
    });
  };

  const setCanonicalDataScope = (value: string) => {
    updateParams((next) => {
      next.set("dataScope", normalizeDataScope(value));
      next.delete("supplierId");
    });
  };

  const setStore = (value: string) => {
    updateParams((next) => {
      if (value) next.set("storeId", value);
      else next.delete("storeId");
      next.delete("supplierId");
    });
  };

  const setSupplier = (value: string) => {
    updateParams((next) => {
      if (value) next.set("supplierId", value);
      else next.delete("supplierId");
    });
  };

  const resetFilters = () => {
    updateParams((next) => {
      const range = getPresetRange("30d");
      next.set("periodPreset", "30d");
      next.set("fromDate", range.fromDate);
      next.set("toDate", range.toDate);
      next.set("dataScope", getDataScope());
      next.delete("storeId");
      next.delete("supplierId");
      next.delete("sezonaId");
      next.delete("includeUnknown");
      next.delete("focus");
    });
  };

  return {
    currentTab,
    canonicalFilters,
    invalidRange,
    setTab,
    setPreset,
    setDate,
    setDataScope: setCanonicalDataScope,
    setStore,
    setSupplier,
    resetFilters,
  };
}
