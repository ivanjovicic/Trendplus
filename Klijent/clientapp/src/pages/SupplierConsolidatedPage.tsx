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

const tabHints: Record<SupplierTab, string> = {
  overview: "Finalna preporuka",
  scorecard: "Poređenje dobavljača",
  assortment: "Struktura i drilldown",
};

const tabDescriptions: Record<SupplierTab, string> = {
  overview: "Pregled: glavna preporuka za dobavljača i centralni ekran za poslovnu odluku.",
  scorecard: "Skorkarta dobavljača — pomoćni signal. Koristi se za poređenje i objašnjenje, dok je finalna poslovna preporuka u tabu Pregled.",
  assortment: "Asortiman: drilldown strukture prometa po tipu obuće, bez posebne finalne preporuke.",
};

const dataScopeLabels: Record<string, string> = {
  all: "Svi podaci",
  existing: "Postojeći artikli",
  imported: "Uvezeni podaci",
};

const dataQualityLabels: Record<string, string> = {
  good: "Pouzdani podaci",
  warning: "Potreban oprez",
  insufficient_data: "Nedovoljno podataka",
  error: "Problem u podacima",
  unknown: "Pouzdanost nije potvrđena",
};

const tabTakeaways: Record<SupplierTab, { title: string; description: string }> = {
  overview: {
    title: "Pregled vodi finalnu odluku",
    description: "Ovde prvo proveravaš da li dobavljač zaslužuje fokus. Ostali tabovi služe da objasne zašto.",
  },
  scorecard: {
    title: "Skorkarta služi za poređenje",
    description: "Koristi je da uporediš dobavljače i proveriš signal, ali finalnu odluku potvrdi u tabu Pregled.",
  },
  assortment: {
    title: "Asortiman objašnjava strukturu",
    description: "Ovde gledaš koji tipovi obuće nose promet i gde treba dodatni drilldown bez finalne preporuke.",
  },
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

  const selectedStoreLabel = canonicalFilters.storeId
    ? stores.find((store) => String(store.storeId) === String(canonicalFilters.storeId))?.storeName ?? "Izabrani objekat"
    : "Svi objekti";
  const selectedSupplierLabel = canonicalFilters.supplierId
    ? suppliers.find((supplier) => String(supplier.supplierId) === String(canonicalFilters.supplierId))?.supplierName ?? "Izabrani dobavljač"
    : "Svi dobavljači";
  const activeScopeLabel = dataScopeLabels[canonicalFilters.dataScope] ?? canonicalFilters.dataScope;
  const activePeriodLabel = trustPayload?.effectivePeriodLabel?.trim()
    ? trustPayload.effectivePeriodLabel
    : `${canonicalFilters.fromDate} — ${canonicalFilters.toDate}`;
  const datasetLabel = trustPayload?.effectiveDataset?.trim()
    || trustPayload?.requestedDataset?.trim()
    || "Aktivni dataset nije posebno označen";
  const trustHeadline = trustPayload?.usedFallback
    ? "Fallback ili sužen dataset je aktivan"
    : trustPayload?.recommendationAllowed === false
      ? "Signal je informativan i traži proveru"
      : currentTab === "overview"
        ? "Pregled je glavni izvor preporuke"
        : currentTab === "scorecard"
          ? "Skorkarta je pomoćni signal"
          : "Asortiman je objašnjenje i drilldown";
  const trustDescription = trustPayload?.usedFallback
    ? (trustPayload.fallbackReason ?? "Pre konačnog zaključka proveri effective period i dataset u trust headeru.")
    : trustPayload?.recommendationNote
      ?? (currentTab === "scorecard"
        ? "Poređenje dobavljača čitaj uz finalnu preporuku iz taba Pregled."
        : currentTab === "assortment"
          ? "Koristi ovaj prikaz da razumeš uzrok rezultata, ne kao samostalnu finalnu preporuku."
          : "Skorkarta i asortiman služe da potvrde ili objasne ono što vidiš u pregledu.");
  const trustToneClass = trustPayload?.dataQualityStatus === "error"
    ? "critical"
    : (trustPayload?.usedFallback
      || trustPayload?.recommendationAllowed === false
      || trustPayload?.dataQualityStatus === "warning"
      || trustPayload?.dataQualityStatus === "insufficient_data")
      ? "warning"
      : "info";
  const trustStatusLabel = trustPayload?.dataQualityStatus
    ? (dataQualityLabels[trustPayload.dataQualityStatus] ?? trustPayload.dataQualityStatus)
    : "Pouzdanost nije potvrđena";

  useEffect(() => {
    let cancelled = false;
    getStores(true)
      .then((items) => { if (!cancelled) setStores(items); })
      .catch(() => {
        if (!cancelled) {
          // Preserve the last known store list on transient failures instead of faking an empty filter set.
        }
      });
    return () => { cancelled = true; };
  }, [canonicalFilters.dataScope]);

  useEffect(() => {
    let cancelled = false;
    getSupplierFilters(canonicalFilters.fromDate, canonicalFilters.toDate, true, canonicalFilters.storeId)
      .then((items) => { if (!cancelled) setSuppliers(items); })
      .catch(() => {
        if (!cancelled) {
          // Preserve the last known supplier list on transient failures instead of faking an empty filter set.
        }
      });
    return () => { cancelled = true; };
  }, [canonicalFilters.fromDate, canonicalFilters.storeId, canonicalFilters.toDate, canonicalFilters.dataScope]);

  useEffect(() => {
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
        title="Dobavljači"
        description="Jedinstveni ekran za glavnu preporuku, poređenje dobavljača i analizu asortimana."
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
        mode={
          currentTab === "assortment"
            ? "signal"
            : currentTab === "scorecard"
              ? (trustPayload?.recommendationAllowed === true ? "recommendation" : "signal")
              : "recommendation"
        }
        recommendationNote={trustPayload?.recommendationNote ?? (
          currentTab === "scorecard"
            ? (trustPayload?.recommendationAllowed === true
              ? "Skorkarta je signalni sloj uz aktivnu finalnu preporuku."
              : "Ovo je analitički signal. Finalna preporuka je u tabu Pregled.")
            : (currentTab !== "assortment" ? "Pregled je finalna preporuka; asortiman i skorkarta su signalni slojevi." : undefined)
        )}
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
            <span className="supplier-tab-copy">
              <span className="supplier-tab-label">{tabLabels[tab]}</span>
              <span className="supplier-tab-hint">{tabHints[tab]}</span>
            </span>
            {tab === "overview" && <span className="supplier-tab-badge">Glavni</span>}
          </button>
        ))}
      </nav>

      <section className="supplier-consolidated-context" aria-label="Kako čitati ekran dobavljača">
        <article className="supplier-consolidated-context-card supplier-consolidated-context-card--primary">
          <span className="supplier-context-kicker">Aktivni prikaz</span>
          <strong>{tabTakeaways[currentTab].title}</strong>
          <p>{tabTakeaways[currentTab].description}</p>
        </article>
        <article className="supplier-consolidated-context-card">
          <span className="supplier-context-kicker">Period i filteri</span>
          <strong>{activePeriodLabel}</strong>
          <p>{`${activeScopeLabel} • ${selectedStoreLabel} • ${selectedSupplierLabel}`}</p>
        </article>
        <article className={`supplier-consolidated-context-card supplier-consolidated-context-card--${trustToneClass}`}>
          <span className="supplier-context-kicker">Trust i poređenje</span>
          <strong>{trustHeadline}</strong>
          <p>{`${trustDescription} Kvalitet: ${trustStatusLabel}. Dataset: ${datasetLabel}.`}</p>
        </article>
      </section>

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
