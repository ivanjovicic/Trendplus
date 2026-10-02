import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getStores, getSupplierFilters } from "../services/analyticsApi";
import { getSezone } from "../services/sezoneApi";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import type { StoreOption, SupplierFilterOption } from "../types/analytics";
import type { Sezona } from "../types/Sezona";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { getAnalyticsMetaMessage } from "../utils/analyticsResponseMeta";
import { buildSupplierTabBasisRows } from "../utils/supplierTabBasisLabels";
import {
  isSupplierTrustPayloadPending,
  normalizeSupplierChildDataQualityStatus,
  resolveSupplierConsolidatedDatasetLabel,
  resolveSupplierConsolidatedContextToneClass,
  resolveSupplierConsolidatedRecommendationNote,
  resolveSupplierConsolidatedTrustDescription,
  resolveSupplierConsolidatedTrustHeadline,
  resolveSupplierConsolidatedTrustMode,
  resolveSupplierConsolidatedTrustStatusLabel,
  resolveSupplierTabFallbackDataSource,
} from "../utils/supplierConsolidatedTrustState";
import {
  resolveSupplierFilterFallbackState,
  SUPPLIER_FILTER_LOAD_FAILED_MESSAGE,
  SUPPLIER_FILTER_STALE_LIST_MESSAGE,
} from "../utils/supplierFilterFallbackState";
import SupplierSalesStatsPage from "./SupplierSalesStatsPage";
import SupplierDecisionHubPage from "./SupplierDecisionHubPage";
import SupplierFootwearAnalyticsPage from "./SupplierFootwearAnalyticsPage";
import type { SupplierPeriodPreset, SupplierTab } from "./supplierSharedState";
import { SUPPLIER_TABS, type SupplierTrustHeaderPayload } from "./supplierSharedState";
import { SUPPLIER_TAB_ROLE_CUE } from "../utils/supplierTabInformationHierarchy";
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
  assortment: "Struktura i detaljna razrada",
};

const tabDescriptions: Record<SupplierTab, string> = {
  overview: "Pregled: glavna preporuka za dobavljača i centralni ekran za poslovnu odluku.",
  scorecard: "Skorkarta dobavljača — pomoćni signal. Koristi se za poređenje i objašnjenje, dok je konačna poslovna preporuka u tabu Pregled.",
  assortment: "Asortiman: detaljna razrada strukture prometa po tipu obuće, bez posebne konačne preporuke.",
};

const dataScopeLabels: Record<string, string> = {
  all: "Svi podaci",
  existing: "Postojeći artikli",
  imported: "Uvezeni podaci",
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
    description: "Ovde gledaš koji tipovi obuće nose promet i gde je potrebna dodatna razrada bez konačne preporuke.",
  },
};

const legacyContextMessages: Record<string, string> = {
  "operations-supplier-sales": "Kompatibilna veza iz Operacija otvorila je glavni Pregled dobavljača, tab Pregled. Aktivna navigacija prati ovaj glavni ekran.",
  "operations-supplier-footwear": "Kompatibilna veza iz Operacija otvorila je glavni Pregled dobavljača, tab Asortiman. Aktivna navigacija prati ovaj glavni ekran.",
};

function buildStoreLabel(store: StoreOption, duplicateNames: ReadonlySet<string> = new Set()): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  const baseLabel = extras ? `${store.storeName} (${extras})` : store.storeName;
  return duplicateNames.has(store.storeName) ? `${baseLabel} [ID ${store.storeId}]` : baseLabel;
}

export default function SupplierConsolidatedPage() {
  const [searchParams] = useSearchParams();
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [suppliers, setSuppliers] = useState<SupplierFilterOption[]>([]);
  const [seasons, setSeasons] = useState<Sezona[]>([]);
  const [supplierFiltersWarning, setSupplierFiltersWarning] = useState<string | null>(null);
  const [supplierFiltersStale, setSupplierFiltersStale] = useState(false);
  const suppliersRef = useRef(suppliers);
  const [trustState, setTrustState] = useState<{ key: string; payload: SupplierTrustHeaderPayload | null }>({ key: "", payload: null });
  const [supplierFiltersLoaded, setSupplierFiltersLoaded] = useState(false);
  suppliersRef.current = suppliers;
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
    setCategory,
    setGender,
    setSeason,
    setMinRevenue,
    setOnlyHighConfidence,
    setExcludeOosBeforeMarkdown,
    resetFilters,
  } = useSupplierCanonicalState();

  const trustRequestKey = useMemo(
    () => {
      const key = [
        currentTab,
        canonicalFilters.fromDate,
        canonicalFilters.toDate,
        canonicalFilters.dataScope,
        canonicalFilters.storeId ?? "",
      ];
      if (currentTab === "scorecard") {
        key.push(
          canonicalFilters.supplierId ?? "",
          canonicalFilters.category ?? "",
          canonicalFilters.gender ?? "",
          canonicalFilters.seasonId ?? "",
          canonicalFilters.minRevenue ?? "",
          canonicalFilters.onlyHighConfidence ? "1" : "0",
          canonicalFilters.excludeOosBeforeMarkdown ? "1" : "0",
        );
      } else if (currentTab === "assortment") {
        key.push(canonicalFilters.supplierId ?? "");
      }
      return key.join("|");
    },
    [canonicalFilters, currentTab],
  );
  const trustRequestKeyRef = useRef("");
  trustRequestKeyRef.current = trustRequestKey;
  const trustPayload = trustState.key === trustRequestKey ? trustState.payload : null;
  const trustPending = isSupplierTrustPayloadPending(trustPayload);
  const countingBasisRows = useMemo(
    () => buildSupplierTabBasisRows(trustPayload?.basis ?? null),
    [trustPayload?.basis],
  );
  const handleTrustMetadataChange = useCallback(
    (payload: SupplierTrustHeaderPayload | null) => {
      if (payload && payload.requestKey !== trustRequestKeyRef.current) return;
      setTrustState({ key: trustRequestKeyRef.current, payload });
    },
    [],
  );
  const duplicateStoreNames = useMemo(() => {
    const counts = new Map<string, number>();
    stores.forEach((store) => counts.set(store.storeName, (counts.get(store.storeName) ?? 0) + 1));
    return new Set([...counts.entries()].filter(([, count]) => count > 1).map(([name]) => name));
  }, [stores]);
  const supplierSelectionMissing = supplierFiltersLoaded
    && !supplierFiltersStale
    && canonicalFilters.supplierId != null
    && !suppliers.some((supplier) => String(supplier.supplierId) === String(canonicalFilters.supplierId));

  const selectedStoreLabel = canonicalFilters.storeId
    ? (() => {
      const selected = stores.find((store) => String(store.storeId) === String(canonicalFilters.storeId));
      return selected ? buildStoreLabel(selected, duplicateStoreNames) : "Izabrani objekat";
    })()
    : "Svi objekti";
  const selectedSupplierLabel = canonicalFilters.supplierId
    ? suppliers.find((supplier) => String(supplier.supplierId) === String(canonicalFilters.supplierId))?.supplierName ?? "Izabrani dobavljač"
    : "Svi dobavljači";
  const activeScopeLabel = dataScopeLabels[canonicalFilters.dataScope] ?? canonicalFilters.dataScope;
  const legacyContextMessage = legacyContextMessages[searchParams.get("legacySource") ?? ""] ?? null;
  const effectivePeriodLabel = typeof trustPayload?.effectivePeriodLabel === "string"
    ? trustPayload.effectivePeriodLabel.trim()
    : null;
  const activePeriodLabel = effectivePeriodLabel
    ? effectivePeriodLabel
    : `${canonicalFilters.fromDate} — ${canonicalFilters.toDate}`;
  const datasetLabel = resolveSupplierConsolidatedDatasetLabel(trustPayload);
  const trustHeadline = resolveSupplierConsolidatedTrustHeadline(currentTab, trustPayload);
  const fallbackReasonText = trustPayload?.usedFallback
    ? getSafeAnalyticsErrorMessage(
      trustPayload.fallbackReason,
      trustPayload.fallbackReasonCode,
      "Pre konačnog zaključka proveri efektivni period i skup podataka u zaglavlju pouzdanosti.",
    )
    : null;
  const recommendationNoteText = typeof trustPayload?.recommendationNote === "string"
    ? getSafeAnalyticsErrorMessage(trustPayload.recommendationNote)
    : null;
  const trustDescription = resolveSupplierConsolidatedTrustDescription(
    currentTab,
    trustPayload,
    recommendationNoteText,
    fallbackReasonText,
  );
  const trustToneClass = resolveSupplierConsolidatedContextToneClass(trustPayload);
  const trustStatusLabel = resolveSupplierConsolidatedTrustStatusLabel(trustPayload);
  const resolvedDataQualityStatus = normalizeSupplierChildDataQualityStatus(trustPayload?.dataQualityStatus);
  const consolidatedTrustMode = resolveSupplierConsolidatedTrustMode(currentTab);
  const consolidatedDataSource = trustPayload?.dataSource ?? resolveSupplierTabFallbackDataSource(currentTab);
  const consolidatedRecommendationNote = resolveSupplierConsolidatedRecommendationNote(
    currentTab,
    trustPayload,
    recommendationNoteText,
  );

  useEffect(() => {
    if (currentTab !== "scorecard") return;
    let cancelled = false;
    getSezone()
      .then((items) => {
        if (!cancelled) setSeasons(items);
      })
      .catch(() => {
        if (!cancelled) setSeasons([]);
      });
    return () => { cancelled = true; };
  }, [currentTab]);

  useEffect(() => {
    let cancelled = false;
    getStores(true, canonicalFilters.dataScope)
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
    getSupplierFilters(
      canonicalFilters.fromDate,
      canonicalFilters.toDate,
      true,
      canonicalFilters.storeId,
      canonicalFilters.dataScope,
    )
      .then((items) => {
        if (cancelled) return;

        const resolved = resolveSupplierFilterFallbackState(items, suppliersRef.current);
        setSupplierFiltersLoaded(true);
        setSupplierFiltersWarning(resolved.warning);
        setSupplierFiltersStale(resolved.isStale);
        setSuppliers(resolved.suppliers);

      })
      .catch(() => {
        if (!cancelled) {
          // Preserve prior options without claiming that they match the active period and scope.
          setSupplierFiltersWarning(SUPPLIER_FILTER_LOAD_FAILED_MESSAGE);
          setSupplierFiltersStale(true);
          setSupplierFiltersLoaded(true);
        }
      });
    return () => { cancelled = true; };
  }, [canonicalFilters.fromDate, canonicalFilters.storeId, canonicalFilters.toDate, canonicalFilters.dataScope]);

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
        dataSource={consolidatedDataSource}
        provenanceBasis={trustPayload?.provenanceBasis ?? null}
        dataQualityStatus={resolvedDataQualityStatus}
        dataQualitySummary={trustPayload?.dataQualitySummary}
        requestedDataset={trustPayload?.requestedDataset ?? null}
        effectiveDataset={trustPayload?.effectiveDataset ?? null}
        effectivePeriodLabel={trustPayload?.effectivePeriodLabel ?? null}
        usedFallback={trustPayload?.usedFallback ?? false}
        fallbackReason={trustPayload?.fallbackReason ?? null}
        fallbackReasonCode={trustPayload?.fallbackReasonCode ?? null}
        recommendationAllowed={trustPending ? null : (trustPayload?.recommendationAllowed ?? null)}
        trustPending={trustPending}
        mode={consolidatedTrustMode}
        recommendationNote={consolidatedRecommendationNote}
        emptyStateReason={trustPayload?.emptyStateReason ?? null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      {legacyContextMessage ? (
        <div className="supplier-consolidated-message supplier-consolidated-message--compatibility" role="status" data-testid="supplier-legacy-context">
          {legacyContextMessage}
        </div>
      ) : null}
      <header className="supplier-consolidated-header">
        <div className="supplier-consolidated-header-content">
          <div>
            <div className="supplier-consolidated-overline">Centralna analitika dobavljača</div>
            <h2>Dobavljači</h2>
            <p className="supplier-consolidated-header-desc">{tabDescriptions[currentTab]}</p>
          </div>
        </div>
      </header>

      <section className="supplier-consolidated-filters" aria-label="Filteri dobavljača">
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
              <option key={store.storeId} value={store.storeId}>{buildStoreLabel(store, duplicateStoreNames)}</option>
            ))}
          </select>
        </label>

        <label className={`supplier-consolidated-field${supplierFiltersStale ? " supplier-consolidated-field--stale" : ""}`}>
          <span className="supplier-consolidated-field-label">
            Dobavljač
            {supplierFiltersStale ? <span className="supplier-consolidated-stale-badge">Zastarela lista</span> : null}
          </span>
          <select
            aria-label="Dobavljač"
            value={canonicalFilters.supplierId ?? ""}
            onChange={(event) => setSupplier(event.target.value)}
            disabled={supplierFiltersStale}
            aria-invalid={supplierFiltersStale || undefined}
            aria-describedby={supplierFiltersStale ? "supplier-filter-stale-warning" : undefined}
          >
            <option value="">{supplierFiltersStale ? "Izbor je privremeno blokiran" : "Svi dobavljači"}</option>
            {supplierSelectionMissing ? (
              <option value={canonicalFilters.supplierId ?? ""} disabled>
                {selectedSupplierLabel} (nije u aktivnom skupu)
              </option>
            ) : null}
            {suppliers.map((supplier) => (
              <option key={supplier.supplierId} value={supplier.supplierId} disabled={supplierFiltersStale}>
                {supplier.supplierName}
              </option>
            ))}
          </select>
          {supplierFiltersWarning ? (
            <span
              id="supplier-filter-stale-warning"
              className={`supplier-consolidated-filter-note${supplierFiltersStale ? " supplier-consolidated-filter-note--stale" : ""}`}
              role="status"
            >
              {supplierFiltersWarning}
              {supplierFiltersStale ? ` ${SUPPLIER_FILTER_STALE_LIST_MESSAGE}` : ""}
            </span>
          ) : null}
          {supplierSelectionMissing ? (
            <span className="supplier-consolidated-filter-note supplier-consolidated-filter-note--missing" role="status">
              Dobavljač nije u izabranom periodu/opsegu.
            </span>
          ) : null}
        </label>

        {currentTab === "scorecard" ? (
          <>
            <label className="supplier-consolidated-field">
              <span>Kategorija skorkarte</span>
              <input
                type="text"
                value={canonicalFilters.category ?? ""}
                placeholder="npr. Patike"
                onChange={(event) => setCategory(event.target.value)}
              />
            </label>

            <label className="supplier-consolidated-field">
              <span>Pol skorkarte</span>
              <select value={canonicalFilters.gender ?? ""} onChange={(event) => setGender(event.target.value)}>
                <option value="">Svi polovi</option>
                <option value="Žensko">Žensko</option>
                <option value="Muško">Muško</option>
                <option value="Unisex">Unisex</option>
                <option value="Dečije">Dečije</option>
              </select>
            </label>

            <label className="supplier-consolidated-field">
              <span>Sezona skorkarte</span>
              <select value={canonicalFilters.seasonId ?? ""} onChange={(event) => setSeason(event.target.value)}>
                <option value="">Sve sezone</option>
                {seasons.map((season) => (
                  <option key={season.id} value={season.id}>{season.naziv}</option>
                ))}
              </select>
            </label>

            <label className="supplier-consolidated-field">
              <span>Minimalni prihod skorkarte</span>
              <input
                type="number"
                min="0"
                step="1000"
                value={canonicalFilters.minRevenue ?? ""}
                onChange={(event) => setMinRevenue(event.target.value)}
              />
            </label>

            <label className="supplier-consolidated-check">
              <input
                type="checkbox"
                checked={canonicalFilters.onlyHighConfidence === true}
                onChange={(event) => setOnlyHighConfidence(event.target.checked)}
              />
              <span>Samo visoka pouzdanost skorkarte</span>
            </label>

            <label className="supplier-consolidated-check">
              <input
                type="checkbox"
                checked={canonicalFilters.excludeOosBeforeMarkdown === true}
                onChange={(event) => setExcludeOosBeforeMarkdown(event.target.checked)}
              />
              <span>Isključi artikle bez zaliha pre sniženja iz skorkarte</span>
            </label>
          </>
        ) : null}

        <div className="supplier-consolidated-actions">
          <button type="button" className="secondary" onClick={resetFilters}>Reset</button>
        </div>
      </section>

      {invalidRange ? <div className="supplier-consolidated-message error" role="alert">Datum od ne može biti posle datuma do.</div> : null}

      <nav className="supplier-consolidated-tabs" aria-label="Kartice analitike dobavljača">
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

      <p className="supplier-tab-role-cue" data-testid="supplier-tab-role-cue">
        {SUPPLIER_TAB_ROLE_CUE[currentTab]}
      </p>

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
          <p>{`${trustDescription} Kvalitet: ${trustStatusLabel}. Skup podataka: ${datasetLabel}.`}</p>
        </article>
      </section>

      <details className="supplier-counting-basis" data-testid="supplier-counting-basis">
        <summary>Kako se broji</summary>
        {countingBasisRows ? (
          <dl className="supplier-counting-basis-list">
            {countingBasisRows.map((row) => (
              <div key={row.key} className="supplier-counting-basis-row">
                <dt>{row.label}</dt>
                <dd>{row.value}</dd>
              </div>
            ))}
          </dl>
        ) : (
          <p className="supplier-counting-basis-empty">
            Način brojanja za ovaj prikaz još nije dostupan. Brojke između tabova ne porediti dok se ne učita.
          </p>
        )}
        <p className="supplier-counting-basis-note">
          Tabovi namerno broje različito; ista oznaka u dva taba ne mora dati isti iznos.
        </p>
      </details>

      <div className="supplier-consolidated-content">
        {currentTab === "overview" && (
          <div className="supplier-embedded-container supplier-embedded-overview">
            <SupplierSalesStatsPage embedded sharedFilters={canonicalFilters} trustRequestKey={trustRequestKey} onTrustMetadataChange={handleTrustMetadataChange} />
          </div>
        )}
        {currentTab === "scorecard" && (
          <div className="supplier-embedded-container supplier-embedded-scorecard">
            <SupplierDecisionHubPage embedded sharedFilters={canonicalFilters} trustRequestKey={trustRequestKey} onTrustMetadataChange={handleTrustMetadataChange} />
          </div>
        )}
        {currentTab === "assortment" && (
          <div className="supplier-embedded-container supplier-embedded-assortment">
            <SupplierFootwearAnalyticsPage embedded sharedFilters={canonicalFilters} trustRequestKey={trustRequestKey} onTrustMetadataChange={handleTrustMetadataChange} />
          </div>
        )}
      </div>
    </div>
  );
}
