import { startTransition, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import SupplierDetailDrawer from "../components/supplierDecisionHub/SupplierDetailDrawer";
import SupplierDecisionFilters, {
  type SupplierDecisionFilterFormState,
} from "../components/supplierDecisionHub/SupplierDecisionFilters";
import SupplierDecisionKpis from "../components/supplierDecisionHub/SupplierDecisionKpis";
import SupplierDecisionQuadrant from "../components/supplierDecisionHub/SupplierDecisionQuadrant";
import SupplierDecisionTable from "../components/supplierDecisionHub/SupplierDecisionTable";
import SupplierRecommendationRail from "../components/supplierDecisionHub/SupplierRecommendationRail";
import {
  getSupplierDecisionDetails,
  getSupplierDecisionQuadrant,
  getSupplierDecisionRanking,
  getSupplierDecisionSummary,
  type QuadrantResponse,
  type RankingResponse,
  type SummaryResponse,
  type SupplierDecisionDetailsResponse,
  type SupplierDecisionHubFilters,
  type SupplierDecisionHubSortField,
} from "../services/supplierDecisionHubApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import { getSezone } from "../services/sezoneApi";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { Sezona } from "../types/Sezona";
import { formatDateRange } from "../components/supplierDecisionHub/utils";
import "./SupplierDecisionHubPage.css";

const PAGE_SIZE = 12;

const rankingColumns: AnalyticsTableColumn<RankingResponse["items"][number]>[] = [
  { key: "supplierId", header: "Dobavljac ID", dataType: "number" },
  { key: "supplierName", header: "Dobavljac", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency" },
  { key: "units", header: "Komadi", dataType: "number" },
  { key: "fullPriceRevenueShare", header: "Udeo bez snizenja", dataType: "percent" },
  { key: "fullPriceSellthrough", header: "Sell-through bez snizenja", dataType: "percent" },
  { key: "preMarkdownMarginPct", header: "Marza", dataType: "percent" },
  { key: "markdownRevenueShare", header: "Udeo snizenja", dataType: "percent" },
  { key: "deadStockRate", header: "Dead stock", dataType: "percent" },
  { key: "unsoldStockValue", header: "Unsold stock value", dataType: "currency" },
  { key: "repeatWinnerRate", header: "Repeat winner rate", dataType: "percent" },
  { key: "mlSupplierScore", header: "AI procena", dataType: "number" },
  { key: "supplierQualityIndex", header: "Indeks kvaliteta", dataType: "number" },
  { key: "confidenceScore", header: "Pouzdanost", dataType: "number" },
  { key: "recommendationCode", header: "Preporuka", dataType: "text" },
];

function createDefaultFormState(): SupplierDecisionFilterFormState {
  return {
    fromDate: "",
    toDate: "",
    category: "",
    gender: "",
    seasonId: "",
    minRevenue: "",
    onlyHighConfidence: false,
    excludeOosBeforeMarkdown: false,
  };
}

function normalizeFilters(formState: SupplierDecisionFilterFormState): SupplierDecisionHubFilters {
  return {
    fromDate: formState.fromDate || undefined,
    toDate: formState.toDate || undefined,
    category: formState.category.trim() || undefined,
    gender: formState.gender || undefined,
    seasonId: formState.seasonId ? Number(formState.seasonId) : undefined,
    minRevenue: formState.minRevenue ? Number(formState.minRevenue) : undefined,
    onlyHighConfidence: formState.onlyHighConfidence,
    excludeOosBeforeMarkdown: formState.excludeOosBeforeMarkdown,
  };
}

export default function SupplierDecisionHubPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [formState, setFormState] = useState<SupplierDecisionFilterFormState>(() =>
    createDefaultFormState()
  );
  const [appliedFilters, setAppliedFilters] = useState<SupplierDecisionHubFilters>(() =>
    normalizeFilters(createDefaultFormState())
  );
  const [seasons, setSeasons] = useState<Sezona[]>([]);
  const [summary, setSummary] = useState<SummaryResponse | null>(null);
  const [quadrant, setQuadrant] = useState<QuadrantResponse | null>(null);
  const [ranking, setRanking] = useState<RankingResponse | null>(null);
  const [details, setDetails] = useState<SupplierDecisionDetailsResponse | null>(null);
  const [loadingOverview, setLoadingOverview] = useState(true);
  const [loadingRanking, setLoadingRanking] = useState(true);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [overviewError, setOverviewError] = useState<string | null>(null);
  const [rankingError, setRankingError] = useState<string | null>(null);
  const [detailsError, setDetailsError] = useState<string | null>(null);
  const [dateError, setDateError] = useState<string | null>(null);
  const [selectedSupplierId, setSelectedSupplierId] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  const [sortBy, setSortBy] =
    useState<SupplierDecisionHubSortField>("supplierQualityIndex");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");

  useEffect(() => {
    let cancelled = false;

    const loadSeasons = async () => {
      try {
        const result = await getSezone();
        if (!cancelled) {
          setSeasons(result);
        }
      } catch {
        if (!cancelled) {
          setSeasons([]);
        }
      }
    };

    void loadSeasons();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadOverview = async () => {
      setLoadingOverview(true);
      setOverviewError(null);
      try {
        const [summaryResponse, quadrantResponse] = await Promise.all([
          getSupplierDecisionSummary(appliedFilters),
          getSupplierDecisionQuadrant(appliedFilters),
        ]);
        if (!cancelled) {
          setSummary(summaryResponse);
          setQuadrant(quadrantResponse);
        }
      } catch {
        if (!cancelled) {
          setSummary(null);
          setQuadrant(null);
          setOverviewError("Nije moguće učitati zbirnu analitiku dobavljača.");
        }
      } finally {
        if (!cancelled) {
          setLoadingOverview(false);
        }
      }
    };

    void loadOverview();
    return () => {
      cancelled = true;
    };
  }, [appliedFilters]);

  useEffect(() => {
    let cancelled = false;

    const loadRanking = async () => {
      setLoadingRanking(true);
      setRankingError(null);
      try {
        const response = await getSupplierDecisionRanking(appliedFilters, {
          page,
          pageSize: PAGE_SIZE,
          sortBy,
          sortDir,
        });
        if (!cancelled) {
          setRanking(response);
        }
      } catch {
        if (!cancelled) {
          setRanking(null);
          setRankingError("Nije moguće učitati rang listu dobavljača.");
        }
      } finally {
        if (!cancelled) {
          setLoadingRanking(false);
        }
      }
    };

    void loadRanking();
    return () => {
      cancelled = true;
    };
  }, [appliedFilters, page, sortBy, sortDir]);

  useEffect(() => {
    if (selectedSupplierId == null) {
      setDetails(null);
      setDetailsError(null);
      setLoadingDetails(false);
      return;
    }

    let cancelled = false;

    const loadDetails = async () => {
      setLoadingDetails(true);
      setDetailsError(null);
      try {
        const response = await getSupplierDecisionDetails(selectedSupplierId, appliedFilters);
        if (!cancelled) {
          setDetails(response);
        }
      } catch {
        if (!cancelled) {
          setDetails(null);
          setDetailsError("Nije moguće učitati detalje izabranog dobavljača.");
        }
      } finally {
        if (!cancelled) {
          setLoadingDetails(false);
        }
      }
    };

    void loadDetails();
    return () => {
      cancelled = true;
    };
  }, [selectedSupplierId, appliedFilters]);

  const heroPeriod = useMemo(
    () =>
      formatDateRange(
        summary?.from ?? appliedFilters.fromDate,
        summary?.to ?? appliedFilters.toDate
      ),
    [appliedFilters.fromDate, appliedFilters.toDate, summary?.from, summary?.to]
  );

  const handleApplyFilters = () => {
    if (formState.fromDate && formState.toDate && formState.fromDate > formState.toDate) {
      setDateError("Datum 'od' mora biti pre ili jednak datumu 'do'.");
      return;
    }
    setDateError(null);
    setPage(1);
    setAppliedFilters(normalizeFilters(formState));
  };

  const handleResetFilters = () => {
    const defaults = createDefaultFormState();
    setFormState(defaults);
    setPage(1);
    setSortBy("supplierQualityIndex");
    setSortDir("desc");
    setDateError(null);
    setAppliedFilters(normalizeFilters(defaults));
  };

  const handleSortChange = (nextSortBy: SupplierDecisionHubSortField) => {
    setPage(1);
    if (sortBy === nextSortBy) {
      setSortDir((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }
    setSortBy(nextSortBy);
    setSortDir("desc");
  };

  const handleSelectSupplier = (supplierId: number) => {
    startTransition(() => {
      setSelectedSupplierId(supplierId);
    });
  };

  const analyticsFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od datuma", value: appliedFilters.fromDate ?? "" },
      { key: "toDate", label: "Do datuma", value: appliedFilters.toDate ?? "" },
      { key: "category", label: "Kategorija", value: appliedFilters.category ?? "" },
      { key: "gender", label: "Pol", value: appliedFilters.gender ?? "" },
      { key: "seasonId", label: "Sezona", value: appliedFilters.seasonId ?? "" },
      { key: "minRevenue", label: "Min prihod", value: appliedFilters.minRevenue ?? "" },
      { key: "onlyHighConfidence", label: "Samo visoka pouzdanost", value: appliedFilters.onlyHighConfidence ?? false },
      { key: "excludeOosBeforeMarkdown", label: "Iskljuci OOS pre markdown", value: appliedFilters.excludeOosBeforeMarkdown ?? false },
      { key: "page", label: "Strana", value: page },
      { key: "sortBy", label: "Sort", value: sortBy },
      { key: "sortDir", label: "Sort smer", value: sortDir },
    ],
    [appliedFilters.category, appliedFilters.excludeOosBeforeMarkdown, appliedFilters.fromDate, appliedFilters.gender, appliedFilters.minRevenue, appliedFilters.onlyHighConfidence, appliedFilters.seasonId, appliedFilters.toDate, page, sortBy, sortDir]
  );

  const analyticsMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "periodFrom", label: "Period od", value: summary?.from ?? "" },
      { key: "periodTo", label: "Period do", value: summary?.to ?? "" },
      { key: "supplierCount", label: "Broj dobavljaca", value: summary?.supplierCount ?? 0 },
      { key: "fullPriceRevenueShare", label: "Udeo bez snizenja", value: summary?.fullPriceRevenueShare ?? "" },
      { key: "fullPriceSellthrough", label: "Sell-through bez snizenja", value: summary?.fullPriceSellthrough ?? "" },
      { key: "markdownRevenueShare", label: "Udeo snizenja", value: summary?.markdownRevenueShare ?? "" },
      { key: "preMarkdownMarginPct", label: "Marza pre markdown", value: summary?.preMarkdownMarginPct ?? "" },
    ],
    [summary?.from, summary?.fullPriceRevenueShare, summary?.fullPriceSellthrough, summary?.markdownRevenueShare, summary?.preMarkdownMarginPct, summary?.supplierCount, summary?.to]
  );

  const openSupplierDetail = (item: RankingResponse["items"][number]) => {
    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "supplier-decision-hub",
        recordId: String(item.supplierId),
        title: item.supplierName,
        subtitle: "Rangiranje dobavljaca",
        columns: rankingColumns,
        row: item,
        metadata: [...analyticsFilters, ...analyticsMetadata],
      })
    );

    navigate(`/analitika/supplier-decision-hub/${item.supplierId}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <div className="supplier-decision-page">
      <section className="supplier-decision-hero">
        <div className="supplier-decision-hero-copy">
          <div className="supplier-decision-overline">Analitika nabavke</div>
          <h1>Centar odluka o dobavljačima</h1>
          <p>
            Stranica razdvaja prodaju pre sniženja od prodaje koja zavisi od spuštanja
            cene, otkriva dobavljače za širenje saradnje i izdvaja lažno loše rezultate
            nastale zbog nedostatka zaliha.
          </p>
          <div className="supplier-decision-hero-meta">
            <span>Obuhvaćen period: {heroPeriod}</span>
            <span>Dobavljača u uzorku: {summary?.supplierCount ?? 0}</span>
          </div>
        </div>
        <SupplierDecisionFilters
          value={formState}
          seasons={seasons}
          pending={loadingOverview || loadingRanking}
          onChange={setFormState}
          onApply={handleApplyFilters}
          onReset={handleResetFilters}
        />
      </section>

      {dateError ? <div className="supplier-decision-error">{dateError}</div> : null}
      {overviewError ? <div className="supplier-decision-error">{overviewError}</div> : null}
      {rankingError ? <div className="supplier-decision-error">{rankingError}</div> : null}

      <section className="supplier-decision-section">
        <div className="supplier-decision-section-title">KPI pregled</div>
        <SupplierDecisionKpis summary={summary} loading={loadingOverview} />
      </section>

      <section className="supplier-decision-section">
        <div className="supplier-decision-section-title">Kvadrant odluka</div>
        <SupplierDecisionQuadrant
          items={quadrant?.items ?? []}
          loading={loadingOverview}
          onSelectSupplier={handleSelectSupplier}
        />
      </section>

      <section className="supplier-decision-section">
        <div className="supplier-decision-section-title">Direktne preporuke</div>
        <SupplierRecommendationRail
          topGrowSuppliers={summary?.topGrowSuppliers ?? []}
          topRiskSuppliers={summary?.topRiskSuppliers ?? []}
          onSelectSupplier={handleSelectSupplier}
        />
      </section>

      <section className="supplier-decision-section">
        <div className="supplier-decision-section-title">Rangiranje dobavljača</div>
        <SupplierDecisionTable
          items={ranking?.items ?? []}
          columns={rankingColumns}
          analyticsFilters={analyticsFilters}
          analyticsMetadata={analyticsMetadata}
          loading={loadingRanking}
          page={ranking?.page ?? page}
          pageSize={ranking?.pageSize ?? PAGE_SIZE}
          totalCount={ranking?.totalCount ?? 0}
          sortBy={sortBy}
          sortDir={sortDir}
          onPageChange={setPage}
          onSortChange={handleSortChange}
          onSelectSupplier={handleSelectSupplier}
          onOpenDetail={openSupplierDetail}
        />
      </section>

      <SupplierDetailDrawer
        open={selectedSupplierId != null}
        loading={loadingDetails}
        error={detailsError}
        details={details}
        onClose={() => setSelectedSupplierId(null)}
      />
    </div>
  );
}
