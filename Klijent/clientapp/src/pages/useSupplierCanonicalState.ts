import { useEffect, useMemo } from "react";
import { useSearchParams } from "react-router-dom";
import { getPresetRange } from "../utils/analyticsFormatters";
import { getDataScope, normalizeDataScope, setDataScope as persistDataScope } from "../utils/dataScope";
import type { SupplierCanonicalFilters, SupplierPeriodPreset, SupplierTab } from "./supplierSharedState";
import { SUPPLIER_TABS } from "./supplierSharedState";

function parseNullableInt(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : null;
}

function parseNullableNumber(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

function parseNullableText(value: string | null): string | null {
  const normalized = value?.trim() ?? "";
  return normalized || null;
}

function parseGender(value: string | null): string | null {
  const normalized = parseNullableText(value);
  return normalized && ["Žensko", "Muško", "Unisex", "Dečije"].includes(normalized)
    ? normalized
    : null;
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
    category: parseNullableText(searchParams.get("category")),
    gender: parseGender(searchParams.get("gender")),
    seasonId: parseNullableInt(searchParams.get("seasonId")),
    minRevenue: parseNullableNumber(searchParams.get("minRevenue")),
    onlyHighConfidence: searchParams.get("onlyHighConfidence") === "true",
    excludeOosBeforeMarkdown: searchParams.get("excludeOosBeforeMarkdown") === "true",
  }), [defaultRange.fromDate, defaultRange.toDate, hasDateQuery, searchParams]);

  const invalidRange = useMemo(
    () => new Date(canonicalFilters.fromDate) > new Date(canonicalFilters.toDate),
    [canonicalFilters.fromDate, canonicalFilters.toDate]
  );

  useEffect(() => {
    persistDataScope(normalizeDataScope(canonicalFilters.dataScope));
  }, [canonicalFilters.dataScope]);

  useEffect(() => {
    const handleScopeChange = () => {
      const nextScope = getDataScope();
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        if (normalizeDataScope(next.get("dataScope")) === nextScope) {
          return current;
        }
        next.set("dataScope", nextScope);
        next.delete("supplierId");
        return next;
      }, { replace: true });
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
  }, [setSearchParams]);

  useEffect(() => {
    const next = new URLSearchParams(searchParams);
    let changed = false;
    const removeIfInvalid = (key: string, valid: boolean) => {
      if (next.has(key) && !valid) {
        next.delete(key);
        changed = true;
      }
    };

    removeIfInvalid("category", canonicalFilters.category !== null);
    removeIfInvalid("gender", canonicalFilters.gender !== null);
    removeIfInvalid("seasonId", canonicalFilters.seasonId !== null);
    removeIfInvalid("minRevenue", canonicalFilters.minRevenue !== null);
    removeIfInvalid("onlyHighConfidence", searchParams.get("onlyHighConfidence") === "true");
    removeIfInvalid("excludeOosBeforeMarkdown", searchParams.get("excludeOosBeforeMarkdown") === "true");

    if (changed) setSearchParams(next, { replace: true });
  }, [canonicalFilters.category, canonicalFilters.gender, canonicalFilters.minRevenue, canonicalFilters.seasonId, searchParams, setSearchParams]);

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

  const setCategory = (value: string) => {
    updateParams((next) => {
      const normalized = value.trim();
      if (normalized) next.set("category", normalized);
      else next.delete("category");
    });
  };

  const setGender = (value: string) => {
    updateParams((next) => {
      if (parseGender(value)) next.set("gender", value);
      else next.delete("gender");
    });
  };

  const setSeason = (value: string) => {
    updateParams((next) => {
      const parsed = parseNullableInt(value);
      if (parsed != null) next.set("seasonId", String(parsed));
      else next.delete("seasonId");
    });
  };

  const setMinRevenue = (value: string) => {
    updateParams((next) => {
      const parsed = parseNullableNumber(value);
      if (parsed != null) next.set("minRevenue", String(parsed));
      else next.delete("minRevenue");
    });
  };

  const setOnlyHighConfidence = (value: boolean) => {
    updateParams((next) => {
      if (value) next.set("onlyHighConfidence", "true");
      else next.delete("onlyHighConfidence");
    });
  };

  const setExcludeOosBeforeMarkdown = (value: boolean) => {
    updateParams((next) => {
      if (value) next.set("excludeOosBeforeMarkdown", "true");
      else next.delete("excludeOosBeforeMarkdown");
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
      next.delete("seasonId");
      next.delete("category");
      next.delete("gender");
      next.delete("minRevenue");
      next.delete("onlyHighConfidence");
      next.delete("excludeOosBeforeMarkdown");
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
    setCategory,
    setGender,
    setSeason,
    setMinRevenue,
    setOnlyHighConfidence,
    setExcludeOosBeforeMarkdown,
    resetFilters,
  };
}
