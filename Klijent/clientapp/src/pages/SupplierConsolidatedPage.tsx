import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getStores, getSupplierFilters } from "../services/analyticsApi";
import type { StoreOption, SupplierFilterOption } from "../types/analytics";
import { getPresetRange } from "../utils/analyticsFormatters";
import { getDataScope, normalizeDataScope, setDataScope } from "../utils/dataScope";
import SupplierSalesStatsPage from "./SupplierSalesStatsPage";
import SupplierDecisionHubPage from "./SupplierDecisionHubPage";
import SupplierFootwearAnalyticsPage from "./SupplierFootwearAnalyticsPage";
import type { SupplierCanonicalFilters, SupplierPeriodPreset } from "./supplierSharedState";
import "./SupplierConsolidatedPage.css";

type SupplierTab = "overview" | "scorecard" | "assortment";

const SUPPLIER_TABS: SupplierTab[] = ["overview", "scorecard", "assortment"];

const tabLabels: Record<SupplierTab, string> = {
  overview: "Pregled",
  scorecard: "Scorecard",
  assortment: "Asortiman",
};

const tabDescriptions: Record<SupplierTab, string> = {
  overview: "Canonical supplier decision surface: promet, marzni doprinos, trend i prioritetna akcija.",
  scorecard: "Sekundarna evaluacija kvaliteta dobavljaca, markdown rizika i pouzdanosti signala.",
  assortment: "Supporting drilldown kroz asortiman i tipove obuce za izabranog dobavljaca ili ceo portfolio.",
};

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

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

export default function SupplierConsolidatedPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);

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
    setDataScope(normalizeDataScope(canonicalFilters.dataScope));
  }, [canonicalFilters.dataScope]);

  useEffect(() => {
    let cancelled = false;
    getStores(true)
      .then((items) => { if (!cancelled) setStores(items); })
      .catch(() => { if (!cancelled) setStores([]); });
    return () => { cancelled = true; };
  }, [canonicalFilters.dataScope]);

  useEffect(() => {
    let cancelled = false;
    getSupplierFilters(canonicalFilters.fromDate, canonicalFilters.toDate, true, canonicalFilters.storeId)
      .then((items) => { if (!cancelled) setSuppliers(items); })
      .catch(() => { if (!cancelled) setSuppliers([]); });
    return () => { cancelled = true; };
  }, [canonicalFilters.fromDate, canonicalFilters.storeId, canonicalFilters.toDate, canonicalFilters.dataScope]);

  const updateParams = (recipe: (next: URLSearchParams) => void) => {
    const next = new URLSearchParams(searchParams);
    recipe(next);
    if (!next.get("tab")) next.set("tab", currentTab);
    setSearchParams(next);
  };

  const handleTabChange = (tab: SupplierTab) => {
    updateParams((next) => { next.set("tab", tab); });
  };

  const handlePresetChange = (preset: SupplierPeriodPreset) => {
    updateParams((next) => {
      next.set("periodPreset", preset);
      if (preset !== "custom") {
        const range = getPresetRange(preset);
        next.set("fromDate", range.fromDate);
        next.set("toDate", range.toDate);
      }
    });
  };

  const handleDateChange = (key: "fromDate" | "toDate", value: string) => {
    updateParams((next) => {
      next.set(key, value);
      next.set("periodPreset", "custom");
    });
  };

  const handleDataScopeChange = (value: string) => {
    updateParams((next) => {
      next.set("dataScope", normalizeDataScope(value));
      next.delete("supplierId");
    });
  };

  const handleStoreChange = (value: string) => {
    updateParams((next) => {
      if (value) next.set("storeId", value);
      else next.delete("storeId");
      next.delete("supplierId");
    });
  };

  const handleSupplierChange = (value: string) => {
    updateParams((next) => {
      if (value) next.set("supplierId", value);
      else next.delete("supplierId");
    });
  };

  const handleResetFilters = () => {
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

  return (
    <div className="supplier-consolidated-page">
      <header className="supplier-consolidated-header">
        <div className="supplier-consolidated-header-content">
          <div>
            <div className="supplier-consolidated-overline">Canonical supplier analytics</div>
            <h1>Dobavljaci</h1>
            <p className="supplier-consolidated-header-desc">{tabDescriptions[currentTab]}</p>
          </div>
        </div>
      </header>

      <section className="supplier-consolidated-filters" aria-label="Supplier filteri">
        <label className="supplier-consolidated-field">
          <span>Period</span>
          <select value={canonicalFilters.periodPreset} onChange={(event) => handlePresetChange(event.target.value as SupplierPeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagodjeno</option>
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Od</span>
          <input type="date" value={canonicalFilters.fromDate} onChange={(event) => handleDateChange("fromDate", event.target.value)} />
        </label>

        <label className="supplier-consolidated-field">
          <span>Do</span>
          <input type="date" value={canonicalFilters.toDate} onChange={(event) => handleDateChange("toDate", event.target.value)} />
        </label>

        <label className="supplier-consolidated-field">
          <span>Opseg</span>
          <select value={canonicalFilters.dataScope} onChange={(event) => handleDataScopeChange(event.target.value)}>
            <option value="all">Svi podaci</option>
            <option value="existing">Postojeci artikli</option>
            <option value="imported">Uvezeni podaci</option>
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Objekat</span>
          <select value={canonicalFilters.storeId ?? ""} onChange={(event) => handleStoreChange(event.target.value)}>
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>{buildStoreLabel(store)}</option>
            ))}
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Dobavljac</span>
          <select value={canonicalFilters.supplierId ?? ""} onChange={(event) => handleSupplierChange(event.target.value)}>
            <option value="">Svi dobavljaci</option>
            {suppliers.map((supplier) => (
              <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>
            ))}
          </select>
        </label>

        <div className="supplier-consolidated-actions">
          <button type="button" className="secondary" onClick={handleResetFilters}>Reset</button>
        </div>
      </section>

      {invalidRange ? <div className="supplier-consolidated-message error">Datum od ne moze biti posle datuma do.</div> : null}

      <nav className="supplier-consolidated-tabs" aria-label="Supplier analytics tabovi">
        {SUPPLIER_TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            className={`supplier-consolidated-tab ${currentTab === tab ? "active" : ""} ${tab === "overview" ? "primary" : ""}`}
            onClick={() => handleTabChange(tab)}
            aria-selected={currentTab === tab}
            aria-current={currentTab === tab ? "page" : undefined}
          >
            <span className="supplier-tab-label">{tabLabels[tab]}</span>
            {tab === "overview" && <span className="supplier-tab-badge">Glavni</span>}
          </button>
        ))}
      </nav>

      <div className="supplier-consolidated-content">
        {currentTab === "overview" && (
          <div className="supplier-embedded-container supplier-embedded-overview">
            <SupplierSalesStatsPage embedded sharedFilters={canonicalFilters} />
          </div>
        )}
        {currentTab === "scorecard" && (
          <div className="supplier-embedded-container supplier-embedded-scorecard">
            <SupplierDecisionHubPage embedded sharedFilters={canonicalFilters} />
          </div>
        )}
        {currentTab === "assortment" && (
          <div className="supplier-embedded-container supplier-embedded-assortment">
            <SupplierFootwearAnalyticsPage embedded sharedFilters={canonicalFilters} />
          </div>
        )}
      </div>
    </div>
  );
}
