import { useEffect, useRef, useState } from "react";
import { getStores, getSupplierFilters } from "../services/analyticsApi";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import type { StoreOption, SupplierFilterOption } from "../types/analytics";
import SupplierSalesStatsPage from "./SupplierSalesStatsPage";
import SupplierDecisionHubPage from "./SupplierDecisionHubPage";
import SupplierFootwearAnalyticsPage from "./SupplierFootwearAnalyticsPage";
import type { SupplierPeriodPreset, SupplierTab } from "./supplierSharedState";
import type { SupplierTrustHeaderPayload } from "./supplierSharedState";
import { SUPPLIER_TABS } from "./supplierSharedState";
import { useSupplierCanonicalState } from "./useSupplierCanonicalState";
import "./SupplierConsolidatedPage.css";

const tabLabels: Record<SupplierTab, string> = {
  overview: "Pregled",
  scorecard: "Skorkarta",
  assortment: "Asortiman",
};

const tabDescriptions: Record<SupplierTab, string> = {
  overview: "Pregled: glavna preporuka za dobavljaca i centralni ekran za poslovnu odluku.",
  scorecard: "Skorkarta: dodatni scorecard signal (nije paralelna finalna preporuka).",
  assortment: "Asortiman: drilldown strukture prometa po tipu obuce, bez posebne finalne preporuke.",
};

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}
export default function SupplierConsolidatedPage() {
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [trustPayload, setTrustPayload] = useState<SupplierTrustHeaderPayload | null>(null);
  const didInitTrustResetRef = useRef(false);
  const {
    currentTab,
    canonicalFilters,
    invalidRange,
    setTab,
    setPreset,
    setDate,
    setDataScope,
    setStore,
    setSupplier,
    resetFilters,
  } = useSupplierCanonicalState();

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

  useEffect(() => {
    // Avoid stale trust metadata while switching tabs/filters.
    if (!didInitTrustResetRef.current)
    {
      didInitTrustResetRef.current = true;
      return;
    }

    setTrustPayload(null);
  }, [currentTab, canonicalFilters.fromDate, canonicalFilters.toDate, canonicalFilters.storeId, canonicalFilters.supplierId, canonicalFilters.dataScope]);

  return (
    <div className="supplier-consolidated-page">
      <AnalyticsTrustHeader
        title="Dobavljaci"
        description="Jedinstveni ekran za overview preporuku, scorecard signal i analizu asortimana dobavljaca."
        periodFrom={trustPayload?.periodFrom ?? canonicalFilters.fromDate}
        periodTo={trustPayload?.periodTo ?? canonicalFilters.toDate}
        lastRefreshAt={trustPayload?.lastRefreshAt ?? null}
        dataFreshnessStatus={trustPayload?.dataFreshnessStatus ?? "unknown"}
        refreshIsRunning={trustPayload?.refreshIsRunning ?? false}
        refreshCurrentStep={trustPayload?.refreshCurrentStep ?? null}
        dataSource={trustPayload?.dataSource ?? "Supplier decision materialized view"}
        dataQualityStatus={trustPayload?.dataQualityStatus ?? null}
        dataQualitySummary={trustPayload?.dataQualitySummary}
        requestedDataset={trustPayload?.requestedDataset ?? null}
        effectiveDataset={trustPayload?.effectiveDataset ?? null}
        effectivePeriodLabel={trustPayload?.effectivePeriodLabel ?? null}
        usedFallback={trustPayload?.usedFallback ?? false}
        fallbackReason={trustPayload?.fallbackReason ?? null}
        fallbackReasonCode={trustPayload?.fallbackReasonCode ?? null}
        recommendationAllowed={trustPayload?.recommendationAllowed ?? null}
        mode={currentTab === "assortment" ? "signal" : "recommendation"}
        recommendationNote={trustPayload?.recommendationNote ?? (currentTab !== "assortment" ? "Pregled i skorkarta su recommendation surface; asortiman je signalni drilldown." : undefined)}
        emptyStateReason={trustPayload?.emptyStateReason ?? null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <header className="supplier-consolidated-header">
        <div className="supplier-consolidated-header-content">
          <div>
            <div className="supplier-consolidated-overline">Centralna analitika dobavljača</div>
            <h1>Dobavljači</h1>
            <p className="supplier-consolidated-header-desc">{tabDescriptions[currentTab]}</p>
          </div>
        </div>
      </header>

      <section className="supplier-consolidated-filters" aria-label="Supplier filteri">
        <label className="supplier-consolidated-field">
          <span>Period</span>
          <select value={canonicalFilters.periodPreset} onChange={(event) => setPreset(event.target.value as SupplierPeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Od</span>
          <input type="date" value={canonicalFilters.fromDate} onChange={(event) => setDate("fromDate", event.target.value)} />
        </label>

        <label className="supplier-consolidated-field">
          <span>Do</span>
          <input type="date" value={canonicalFilters.toDate} onChange={(event) => setDate("toDate", event.target.value)} />
        </label>

        <label className="supplier-consolidated-field">
          <span>Opseg</span>
          <select value={canonicalFilters.dataScope} onChange={(event) => setDataScope(event.target.value)}>
            <option value="all">Svi podaci</option>
            <option value="existing">Postojeći artikli</option>
            <option value="imported">Uvezeni podaci</option>
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Objekat</span>
          <select value={canonicalFilters.storeId ?? ""} onChange={(event) => setStore(event.target.value)}>
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>{buildStoreLabel(store)}</option>
            ))}
          </select>
        </label>

        <label className="supplier-consolidated-field">
          <span>Dobavljač</span>
          <select value={canonicalFilters.supplierId ?? ""} onChange={(event) => setSupplier(event.target.value)}>
            <option value="">Svi dobavljači</option>
            {suppliers.map((supplier) => (
              <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>
            ))}
          </select>
        </label>

        <div className="supplier-consolidated-actions">
          <button type="button" className="secondary" onClick={resetFilters}>Reset</button>
        </div>
      </section>

      {invalidRange ? <div className="supplier-consolidated-message error" role="alert">Datum od ne može biti posle datuma do.</div> : null}

      <nav className="supplier-consolidated-tabs" aria-label="Supplier analytics tabovi">
        {SUPPLIER_TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            className={`supplier-consolidated-tab ${currentTab === tab ? "active" : ""} ${tab === "overview" ? "primary" : ""}`}
            onClick={() => setTab(tab)}
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
            <SupplierSalesStatsPage embedded sharedFilters={canonicalFilters} onTrustMetadataChange={setTrustPayload} />
          </div>
        )}
        {currentTab === "scorecard" && (
          <div className="supplier-embedded-container supplier-embedded-scorecard">
            <SupplierDecisionHubPage embedded sharedFilters={canonicalFilters} onTrustMetadataChange={setTrustPayload} />
          </div>
        )}
        {currentTab === "assortment" && (
          <div className="supplier-embedded-container supplier-embedded-assortment">
            <SupplierFootwearAnalyticsPage embedded sharedFilters={canonicalFilters} onTrustMetadataChange={setTrustPayload} />
          </div>
        )}
      </div>
    </div>
  );
}

