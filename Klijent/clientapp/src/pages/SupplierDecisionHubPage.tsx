import { startTransition, useEffect, useMemo, useState } from "react";
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
import { getSezone } from "../services/sezoneApi";
import type { Sezona } from "../types/Sezona";
import { formatDateRange } from "../components/supplierDecisionHub/utils";
import "./SupplierDecisionHubPage.css";

const PAGE_SIZE = 12;

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
          loading={loadingRanking}
          page={ranking?.page ?? page}
          pageSize={ranking?.pageSize ?? PAGE_SIZE}
          totalCount={ranking?.totalCount ?? 0}
          sortBy={sortBy}
          sortDir={sortDir}
          onPageChange={setPage}
          onSortChange={handleSortChange}
          onSelectSupplier={handleSelectSupplier}
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
