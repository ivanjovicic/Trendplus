import { Fragment, useCallback, useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import {
  getAnalyticsActionById,
  getAnalyticsActions,
  getAnalyticsActionCounts,
  getAnalyticsActionOutcomeSummary,
  updateAnalyticsActionOutcome,
  updateAnalyticsActionStatus,
} from "../services/analyticsApi";
import { fmtNumber, fmtPctFromRatio, fmtRsd, formatDateTime } from "../utils/analyticsFormatters";
import { getAnalyticsActionWriteErrorMessage, isAnalyticsActionWriteForbidden } from "../utils/analyticsActionWriteErrors";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import type {
  AnalyticsActionItem,
  AnalyticsActionCounts,
  AnalyticsActionFilters,
  AnalyticsActionStatus,
  AnalyticsActionSourceType,
  AnalyticsActionPriority,
  AnalyticsActionDataQualityStatus,
  AnalyticsActionAnyDataQualityStatus,
  AnalyticsActionOutcomeUpdateInput,
  AnalyticsActionOutcomeSummaryBucket,
  AnalyticsActionOutcomeSummaryResponse,
} from "../types/analytics";
import "./AnalyticsActionsPage.css";

const OUTCOME_NOTES_PREVIEW_LIMIT = 96;

const SOURCE_LABELS: Record<AnalyticsActionSourceType, string> = {
  dashboard: "Dashboard",
  product: "Proizvodi",
  supplier: "Dobavljaci",
  inventory: "Zalihe",
  nivelacija: "Nivelacija",
  data_quality: "Kvalitet podataka",
};

const STATUS_LABELS: Record<AnalyticsActionStatus, string> = {
  new: "Novo",
  accepted: "Prihvaćeno",
  deferred: "Odloženo",
  rejected: "Odbijeno",
  done: "Završeno",
};

const STATUS_CSS: Record<AnalyticsActionStatus, string> = {
  new: "badge-status badge-new",
  accepted: "badge-status badge-accepted",
  deferred: "badge-status badge-deferred",
  rejected: "badge-status badge-rejected",
  done: "badge-status badge-done",
};

const PRIORITY_CSS: Record<AnalyticsActionPriority, string> = {
  P1: "badge-priority p1",
  P2: "badge-priority p2",
  P3: "badge-priority p3",
};

const DATA_QUALITY_LABELS: Record<AnalyticsActionDataQualityStatus, string> = {
  good: "Dobar",
  warning: "Upozorenje",
  critical: "Kritičan",
  insufficient_data: "Nedovoljno podataka",
};

const DATA_QUALITY_CSS: Record<string, string> = {
  good: "dq-good",
  warning: "dq-warning",
  critical: "dq-critical",
  insufficient_data: "dq-insufficient",
  fair: "dq-warning", // legacy -> warning
  poor: "dq-critical", // legacy -> critical
};

const OUTCOME_LABELS: Record<AnalyticsActionOutcomeUpdateInput["outcomeStatus"], string> = {
  pending: "Čeka proveru",
  success: "Pozitivan ishod",
  neutral: "Neutralan ishod",
  negative: "Negativan ishod",
  not_measured: "Nije izmereno",
};

const OUTCOME_CSS: Record<AnalyticsActionOutcomeUpdateInput["outcomeStatus"], string> = {
  pending: "badge-outcome badge-pending",
  success: "badge-outcome badge-success",
  neutral: "badge-outcome badge-neutral",
  negative: "badge-outcome badge-negative",
  not_measured: "badge-outcome badge-not-measured",
};

const PERIOD_MODE_LABELS: Record<string, string> = {
  created: "po datumu kreiranja",
  resolved: "po datumu zatvaranja",
  measured: "po datumu merenja ishoda",
  mixed: "po kombinovanom periodu",
};

const OUTCOME_SUMMARY_WARNING_LABELS: Record<string, string> = {
  small_sample: "Mali uzorak. Trend čitajte orijentaciono.",
  small_measured_sample: "Malo izmerenih ishoda. Zaključci o uticaju nisu stabilni.",
  outcome_coverage_low: "Mali deo zatvorenih akcija ima upisan ishod.",
  expected_impact_denominator_missing: "Očekivani uticaj nije dostupan za deo uzorka.",
  measured_impact_missing: "Izmereni uticaj nije popunjen za deo evidentiranih ishoda.",
  rejected_actions_present: "U uzorku postoje odbijene akcije; tumačite uspeh odvojeno od odbijanja.",
  mixed_period_filters: "Kombinovani period filteri mogu otežati poređenje trendova.",
};

function normalizeDataQualityStatus(value: string | null | undefined): AnalyticsActionDataQualityStatus | null {
  if (!value) return null;
  const lower = value.toLowerCase();
  if (lower === "fair") return "warning";
  if (lower === "poor") return "critical";
  if (["good", "warning", "critical", "insufficient_data"].includes(lower)) return lower as AnalyticsActionDataQualityStatus;
  return null;
}

function getDataQualityLabel(value: AnalyticsActionAnyDataQualityStatus | null | undefined): string {
  if (!value) return "-";
  const normalized = normalizeDataQualityStatus(value);
  if (!normalized) return value;
  return DATA_QUALITY_LABELS[normalized];
}

function normalizeOutcomeStatus(value: string | null | undefined): AnalyticsActionOutcomeUpdateInput["outcomeStatus"] | null {
  if (!value) return null;
  const normalized = value.trim().toLowerCase();
  if (normalized === "pending" || normalized === "success" || normalized === "neutral" || normalized === "negative" || normalized === "not_measured") {
    return normalized;
  }
  return null;
}

function parseSourceTypeQuery(value: string | null): AnalyticsActionSourceType | undefined {
  if (!value) return undefined;
  if (value === "dashboard" || value === "product" || value === "supplier" || value === "inventory" || value === "nivelacija" || value === "data_quality") {
    return value;
  }
  return undefined;
}

function formatTimestamp(value: string | null | undefined): string {
  return formatDateTime(value, "-");
}

function isoToDateTimeLocal(value: string | null | undefined): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (num: number) => String(num).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function dateTimeLocalToIso(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed.toISOString();
}

function formatMetadataJson(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function formatOutcomeNotesPreview(value: string | null | undefined): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  if (trimmed.length <= OUTCOME_NOTES_PREVIEW_LIMIT) return trimmed;
  return `${trimmed.slice(0, OUTCOME_NOTES_PREVIEW_LIMIT - 3)}...`;
}

function formatSummaryWindow(summary: AnalyticsActionOutcomeSummaryResponse | null): string {
  if (!summary) return "poslednjih 90 dana";
  const periodMode = PERIOD_MODE_LABELS[summary.meta.periodMode] ?? "po izabranom periodu";
  const from = summary.meta.createdFrom ?? summary.meta.resolvedFrom ?? summary.meta.measuredFrom;
  const to = summary.meta.createdTo ?? summary.meta.resolvedTo ?? summary.meta.measuredTo;
  if (from && to) {
    return `${formatDateTime(from, "-")} - ${formatDateTime(to, "-")} ${periodMode}`;
  }

  return `poslednjih 90 dana ${periodMode}`;
}

function getOutcomeSummaryWarningLabel(code: string): string {
  return OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code;
}

function renderBucketRateLabel(rate: number | null | undefined): string {
  return fmtPctFromRatio(rate, 0, "N/A");
}

function getBucketHeading(bucket: AnalyticsActionOutcomeSummaryBucket): string {
  if (bucket.key in SOURCE_LABELS) {
    return SOURCE_LABELS[bucket.key as AnalyticsActionSourceType];
  }

  if (bucket.key in DATA_QUALITY_LABELS) {
    return DATA_QUALITY_LABELS[bucket.key as AnalyticsActionDataQualityStatus];
  }

  if (bucket.key in OUTCOME_LABELS) {
    return OUTCOME_LABELS[bucket.key as AnalyticsActionOutcomeUpdateInput["outcomeStatus"]];
  }

  return bucket.label;
}

type SummaryFilterKey = "sourceType" | "priority" | "dataQualityStatus";

function isSummaryBucketActive(
  filters: AnalyticsActionFilters,
  key: SummaryFilterKey,
  bucketKey: string,
): boolean {
  return (filters[key] ?? undefined) === bucketKey;
}

function getSummaryFilterLabel(key: SummaryFilterKey, value: string): string {
  if (key === "sourceType" && value in SOURCE_LABELS) {
    return `Izvor: ${SOURCE_LABELS[value as AnalyticsActionSourceType]}`;
  }

  if (key === "priority") {
    return `Prioritet: ${value}`;
  }

  if (key === "dataQualityStatus") {
    const normalized = normalizeDataQualityStatus(value);
    if (normalized) {
      return `Kvalitet: ${DATA_QUALITY_LABELS[normalized]}`;
    }
  }

  return value;
}

type StatusModalState = {
  id: number;
  title: string;
  status: AnalyticsActionStatus;
  note: string;
};

type OutcomeModalState = {
  id: number;
  title: string;
  outcomeStatus: AnalyticsActionOutcomeUpdateInput["outcomeStatus"];
  measuredImpactRsd: string;
  outcomeMeasuredAtLocal: string;
  outcomeNotes: string;
};

export default function AnalyticsActionsPage() {
  const location = useLocation();
  const [items, setItems] = useState<AnalyticsActionItem[]>([]);
  const [counts, setCounts] = useState<AnalyticsActionCounts | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [outcomeSummary, setOutcomeSummary] = useState<AnalyticsActionOutcomeSummaryResponse | null>(null);
  const [outcomeSummaryLoading, setOutcomeSummaryLoading] = useState(false);
  const [outcomeSummaryError, setOutcomeSummaryError] = useState<string | null>(null);

  const [filters, setFilters] = useState<AnalyticsActionFilters>(() => {
    const sourceType = parseSourceTypeQuery(new URLSearchParams(location.search).get("sourceType"));
    return {
      page: 1,
      pageSize: 50,
      sourceType,
    };
  });

  const [updatingId, setUpdatingId] = useState<number | null>(null);
  const [expandedIds, setExpandedIds] = useState<number[]>([]);
  const [detailsById, setDetailsById] = useState<Record<number, AnalyticsActionItem>>({});
  const [detailsLoadingId, setDetailsLoadingId] = useState<number | null>(null);
  const [detailsErrorById, setDetailsErrorById] = useState<Record<number, string>>({});
  const [statusModal, setStatusModal] = useState<StatusModalState | null>(null);
  const [statusModalBusy, setStatusModalBusy] = useState(false);
  const [outcomeModal, setOutcomeModal] = useState<OutcomeModalState | null>(null);
  const [outcomeModalBusy, setOutcomeModalBusy] = useState(false);
  const [outcomeModalError, setOutcomeModalError] = useState<string | null>(null);
  const [writeAccessMessage, setWriteAccessMessage] = useState<string | null>(null);

  const loadItems = useCallback(async (f: AnalyticsActionFilters) => {
    setLoading(true);
    setError(null);
    try {
      const res = await getAnalyticsActions(f);
      setItems(res.items);
      setTotalCount(res.totalCount);
      setPage(res.page);
      setTotalPages(res.totalPages);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Greška pri učitavanju");
    } finally {
      setLoading(false);
    }
  }, []);

  const loadCounts = useCallback(async () => {
    try {
      const c = await getAnalyticsActionCounts();
      setCounts(c);
    } catch {
      // non-critical
    }
  }, []);

  const loadOutcomeSummary = useCallback(async (f: AnalyticsActionFilters) => {
    setOutcomeSummaryLoading(true);
    setOutcomeSummaryError(null);
    try {
      const summary = await getAnalyticsActionOutcomeSummary({
        sourceType: f.sourceType,
        priority: f.priority,
        dataQualityStatus: f.dataQualityStatus,
      });
      setOutcomeSummary(summary);
    } catch (e) {
      setOutcomeSummary(null);
      setOutcomeSummaryError(e instanceof Error ? e.message : "Sažetak ishoda trenutno nije dostupan.");
    } finally {
      setOutcomeSummaryLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadItems(filters);
    void loadCounts();
  }, [filters, loadItems, loadCounts]);

  useEffect(() => {
    void loadOutcomeSummary(filters);
  }, [filters.sourceType, filters.priority, filters.dataQualityStatus, loadOutcomeSummary]);

  useEffect(() => {
    const sourceType = parseSourceTypeQuery(new URLSearchParams(location.search).get("sourceType"));
    setFilters((current) => {
      if ((current.sourceType ?? undefined) === sourceType) return current;
      return { ...current, sourceType, page: 1 };
    });
  }, [location.search]);

  function setFilter(key: keyof AnalyticsActionFilters, value: string | number | undefined) {
    const normalizedValue = value === "" || value == null ? undefined : value;
    setFilters((f) => ({ ...f, [key]: normalizedValue, page: 1 }));
  }

  function applySummaryBucketFilter(key: SummaryFilterKey, bucketKey: string) {
    setFilters((current) => ({
      ...current,
      [key]: current[key] === bucketKey ? undefined : bucketKey,
      page: 1,
    }));
  }

  function clearSummaryFilters() {
    setFilters((current) => ({
      ...current,
      sourceType: undefined,
      priority: undefined,
      dataQualityStatus: undefined,
      page: 1,
    }));
  }

  const activeSummaryFilters: Array<{ key: SummaryFilterKey; value: string; label: string }> = [];
  if (filters.sourceType) {
    activeSummaryFilters.push({
      key: "sourceType",
      value: filters.sourceType,
      label: getSummaryFilterLabel("sourceType", filters.sourceType),
    });
  }
  if (filters.priority) {
    activeSummaryFilters.push({
      key: "priority",
      value: filters.priority,
      label: getSummaryFilterLabel("priority", filters.priority),
    });
  }
  if (filters.dataQualityStatus) {
    activeSummaryFilters.push({
      key: "dataQualityStatus",
      value: filters.dataQualityStatus,
      label: getSummaryFilterLabel("dataQualityStatus", filters.dataQualityStatus),
    });
  }

  async function changeStatus(id: number, status: AnalyticsActionStatus, note?: string): Promise<boolean> {
    setWriteAccessMessage(null);
    setUpdatingId(id);
    try {
      const updated = await updateAnalyticsActionStatus(id, { status, note });
      setItems((prev) => prev.map((it) => (it.id === updated.id ? updated : it)));
      setDetailsById((prev) => ({ ...prev, [id]: updated }));
      setDetailsErrorById((prev) => {
        const next = { ...prev };
        delete next[id];
        return next;
      });
      void loadCounts();
      return true;
    } catch (e) {
      setWriteAccessMessage(getAnalyticsActionWriteErrorMessage(e));
      return false;
    } finally {
      setUpdatingId(null);
    }
  }

  async function toggleExpanded(item: AnalyticsActionItem) {
    const { id } = item;
    const willOpen = !expandedIds.includes(id);
    setExpandedIds((prev) => (willOpen ? [...prev, id] : prev.filter((x) => x !== id)));
    if (!willOpen || detailsById[id]) return;

    setDetailsLoadingId(id);
    setDetailsErrorById((prev) => {
      const next = { ...prev };
      delete next[id];
      return next;
    });
    try {
      const detail = await getAnalyticsActionById(id);
      setDetailsById((prev) => ({ ...prev, [id]: detail }));
      setItems((prev) => prev.map((it) => (it.id === id ? detail : it)));
    } catch (e) {
      setDetailsErrorById((prev) => ({
        ...prev,
        [id]: e instanceof Error ? e.message : "Greška pri učitavanju detalja",
      }));
    } finally {
      setDetailsLoadingId((current) => (current === id ? null : current));
    }
  }

  function openStatusNoteModal(item: AnalyticsActionItem, status: AnalyticsActionStatus) {
    setWriteAccessMessage(null);
    setStatusModal({
      id: item.id,
      title: item.title,
      status,
      note: "",
    });
  }

  async function submitStatusModal() {
    if (!statusModal) return;
    setStatusModalBusy(true);
    const trimmedNote = statusModal.note.trim();
    const ok = await changeStatus(statusModal.id, statusModal.status, trimmedNote.length > 0 ? trimmedNote : undefined);
    setStatusModalBusy(false);
    if (ok) setStatusModal(null);
  }

  function openOutcomeModal(item: AnalyticsActionItem) {
    setOutcomeModalError(null);
    setOutcomeModal({
      id: item.id,
      title: item.title,
      outcomeStatus: normalizeOutcomeStatus(item.outcomeStatus) ?? "pending",
      measuredImpactRsd: item.measuredImpactRsd != null ? String(item.measuredImpactRsd) : "",
      outcomeMeasuredAtLocal: isoToDateTimeLocal(item.outcomeMeasuredAtUtc),
      outcomeNotes: item.outcomeNotes ?? "",
    });
  }

  async function submitOutcomeModal() {
    if (!outcomeModal) return;

    if (!outcomeModal.outcomeStatus) {
      setOutcomeModalError("Status ishoda je obavezan.");
      return;
    }

    const isPending = outcomeModal.outcomeStatus === "pending";
    const measuredImpactRaw = outcomeModal.measuredImpactRsd.trim();
    const parsedImpact = isPending || measuredImpactRaw.length === 0
      ? null
      : Number(measuredImpactRaw.replace(",", "."));

    if (parsedImpact != null && !Number.isFinite(parsedImpact)) {
      setOutcomeModalError("Ishod nije sačuvan. Proverite status i iznos.");
      return;
    }

    const measuredAtIso = isPending ? null : dateTimeLocalToIso(outcomeModal.outcomeMeasuredAtLocal);
    if (!isPending && outcomeModal.outcomeMeasuredAtLocal.trim().length > 0 && !measuredAtIso) {
      setOutcomeModalError("Unesite validan datum merenja ishoda.");
      return;
    }

    setWriteAccessMessage(null);
    setOutcomeModalBusy(true);
    setOutcomeModalError(null);
    try {
      const result = await updateAnalyticsActionOutcome(outcomeModal.id, {
        outcomeStatus: outcomeModal.outcomeStatus,
        measuredImpactRsd: isPending ? null : parsedImpact,
        outcomeMeasuredAtUtc: measuredAtIso,
        outcomeNotes: outcomeModal.outcomeNotes.trim().length > 0 ? outcomeModal.outcomeNotes.trim() : null,
      });
      setItems((prev) => prev.map((it) => (it.id === result.id ? result : it)));
      setDetailsById((prev) => ({ ...prev, [result.id]: result }));
      setOutcomeModal(null);
    } catch (e) {
      if (isAnalyticsActionWriteForbidden(e)) {
        setOutcomeModalError(getAnalyticsActionWriteErrorMessage(e));
      } else {
        setOutcomeModalError("Ishod nije sačuvan. Proverite status i iznos.");
      }
    } finally {
      setOutcomeModalBusy(false);
    }
  }

  const openStatuses: AnalyticsActionStatus[] = ["new", "accepted", "deferred"];

  return (
    <div className="aaq-page">
      <AnalyticsTrustHeader
        title="Akcije i preporuke"
        description="Centralni red akcija iz dashboarda, analize proizvoda, dobavljača, zaliha i nivelacija."
        periodFrom={null}
        periodTo={null}
        lastRefreshAt={null}
        dataSource="Action queue"
        mode="report"
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <div className="aaq-header">
        <h1 className="aaq-title">Akcije i preporuke</h1>
        <p className="aaq-subtitle">
          Centralni red akcija iz dashboarda, analize proizvoda, dobavljača, zaliha i nivelacija.
        </p>
        {filters.sourceType === "inventory" && (
          <p className="aaq-subtitle">
            <a href="/analytics/inventory" className="action-link">Otvori Inventory Analytics</a>
          </p>
        )}
      </div>

      {counts && (
        <div className="aaq-kpi-bar">
          <div className="aaq-kpi-card kpi-new">
            <span className="kpi-value">{counts.new}</span>
            <span className="kpi-label">Novo</span>
          </div>
          <div className="aaq-kpi-card kpi-accepted">
            <span className="kpi-value">{counts.accepted}</span>
            <span className="kpi-label">Prihvaćeno</span>
          </div>
          <div className="aaq-kpi-card kpi-deferred">
            <span className="kpi-value">{counts.deferred}</span>
            <span className="kpi-label">Odloženo</span>
          </div>
          <div className="aaq-kpi-card kpi-done">
            <span className="kpi-value">{counts.done + counts.rejected}</span>
            <span className="kpi-label">Zatvoreno</span>
          </div>
          <div className="aaq-kpi-card kpi-p1">
            <span className="kpi-value kpi-p1-val">{counts.p1Open}</span>
            <span className="kpi-label">P1 otvoreno</span>
          </div>
        </div>
      )}

      <section className="aaq-summary-panel" aria-labelledby="aaq-summary-title">
        <div className="aaq-summary-header">
          <div>
            <h2 id="aaq-summary-title" className="aaq-summary-title">Sažetak ishoda akcija</h2>
            <p className="aaq-summary-subtitle">
              Read-only pregled za {formatSummaryWindow(outcomeSummary)}. Sažetak prati izvor, prioritet i kvalitet podataka,
              ali ne prati status liste ni tekstualnu pretragu.
            </p>
            <p className="aaq-summary-hint">
              Klik na red u sažetku primenjuje filter na listu akcija. Ponovni klik uklanja isti filter.
            </p>
          </div>
        </div>

        {outcomeSummaryLoading ? (
          <div className="aaq-summary-loading">Učitavanje sažetka ishoda...</div>
        ) : outcomeSummaryError ? (
          <div className="aaq-summary-error" role="status">
            Sažetak ishoda trenutno nije dostupan. Lista akcija i dalje radi.
          </div>
        ) : outcomeSummary?.meta.emptyReason || outcomeSummary?.meta.sampleSize === 0 ? (
          <div className="aaq-summary-empty">
            Nema dovoljno zatvorenih i izmerenih akcija za pregled ishoda u ovom uzorku.
          </div>
        ) : outcomeSummary ? (
          <>
            {outcomeSummary.meta.warnings.length > 0 && (
              <div className="aaq-summary-warnings" aria-label="Upozorenja za sažetak ishoda">
                {outcomeSummary.meta.warnings.map((warningCode) => (
                  <span key={warningCode} className="aaq-summary-warning-chip">
                    {getOutcomeSummaryWarningLabel(warningCode)}
                  </span>
                ))}
              </div>
            )}

            <div className="aaq-summary-cards">
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Akcije u uzorku</span>
                <strong className="aaq-summary-card-value">{fmtNumber(outcomeSummary.totals.createdCount, 0, "0")}</strong>
                <span className="aaq-summary-card-note">Zatvoreno: {fmtNumber(outcomeSummary.totals.closedCount, 0, "0")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Pokrivenost ishodom</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(outcomeSummary.totals.outcomeCoverageRate, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Na osnovu zatvorenih akcija</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Pozitivan ishod</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(outcomeSummary.totals.positiveOutcomeRate, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Negativan: {fmtPctFromRatio(outcomeSummary.totals.negativeOutcomeRate, 0, "N/A")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Izmereni uticaj</span>
                <strong className="aaq-summary-card-value">{fmtRsd(outcomeSummary.impact.measuredImpactRsd, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Očekivano: {fmtRsd(outcomeSummary.impact.expectedImpactRsd, 0, "N/A")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Realizacija plana</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(outcomeSummary.impact.realizationRatio, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Merenja: {fmtNumber(outcomeSummary.impact.measuredImpactSampleCount, 0, "0")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Ishod čeka proveru</span>
                <strong className="aaq-summary-card-value">{fmtNumber(outcomeSummary.totals.pendingOutcomeCount, 0, "0")}</strong>
                <span className="aaq-summary-card-note">Otvoreno: {fmtNumber(outcomeSummary.totals.openCount, 0, "0")}</span>
              </div>
            </div>

            <div className="aaq-summary-breakdowns">
              <section className="aaq-breakdown-card" aria-labelledby="aaq-breakdown-source">
                <h3 id="aaq-breakdown-source" className="aaq-breakdown-title">Po izvoru</h3>
                <div className="aaq-breakdown-list">
                  {outcomeSummary.bySourceType.map((bucket) => (
                    <button
                      key={bucket.key}
                      type="button"
                      className={`aaq-breakdown-row aaq-breakdown-button ${isSummaryBucketActive(filters, "sourceType", bucket.key) ? "is-active" : ""}`}
                      onClick={() => applySummaryBucketFilter("sourceType", bucket.key)}
                    >
                      <div>
                        <strong>{getBucketHeading(bucket)}</strong>
                        <div className="aaq-breakdown-meta">
                          {fmtNumber(bucket.totalCount, 0, "0")} akcija · pokrivenost {renderBucketRateLabel(bucket.outcomeCoverageRate)}
                        </div>
                      </div>
                      <div className="aaq-breakdown-values">
                        <span>Pozitivan {renderBucketRateLabel(bucket.positiveOutcomeRate)}</span>
                        <span>{fmtRsd(bucket.measuredImpactRsd, 0, "N/A")}</span>
                      </div>
                    </button>
                  ))}
                </div>
              </section>

              <section className="aaq-breakdown-card" aria-labelledby="aaq-breakdown-priority">
                <h3 id="aaq-breakdown-priority" className="aaq-breakdown-title">Po prioritetu</h3>
                <div className="aaq-breakdown-list">
                  {outcomeSummary.byPriority.map((bucket) => (
                    <button
                      key={bucket.key}
                      type="button"
                      className={`aaq-breakdown-row aaq-breakdown-button ${isSummaryBucketActive(filters, "priority", bucket.key) ? "is-active" : ""}`}
                      onClick={() => applySummaryBucketFilter("priority", bucket.key)}
                    >
                      <div>
                        <strong>{bucket.label}</strong>
                        <div className="aaq-breakdown-meta">
                          {fmtNumber(bucket.closedCount, 0, "0")} zatvoreno · negativan ishod {renderBucketRateLabel(bucket.negativeOutcomeRate)}
                        </div>
                      </div>
                      <div className="aaq-breakdown-values">
                        <span>Merljivo {fmtNumber(bucket.measuredCount, 0, "0")}</span>
                        <span>{fmtRsd(bucket.measuredImpactRsd, 0, "N/A")}</span>
                      </div>
                    </button>
                  ))}
                </div>
              </section>

              {outcomeSummary.byDataQuality.length > 0 && (
                <section className="aaq-breakdown-card" aria-labelledby="aaq-breakdown-quality">
                  <h3 id="aaq-breakdown-quality" className="aaq-breakdown-title">Po kvalitetu podataka</h3>
                  <div className="aaq-breakdown-list">
                    {outcomeSummary.byDataQuality.map((bucket) => (
                      <button
                        key={bucket.key}
                        type="button"
                        className={`aaq-breakdown-row aaq-breakdown-button ${isSummaryBucketActive(filters, "dataQualityStatus", bucket.key) ? "is-active" : ""}`}
                        onClick={() => applySummaryBucketFilter("dataQualityStatus", bucket.key)}
                      >
                        <div>
                          <strong>{getBucketHeading(bucket)}</strong>
                          <div className="aaq-breakdown-meta">
                            {fmtNumber(bucket.totalCount, 0, "0")} akcija · pozitivan ishod {renderBucketRateLabel(bucket.positiveOutcomeRate)}
                          </div>
                        </div>
                        <div className="aaq-breakdown-values">
                          <span>Pokrivenost {renderBucketRateLabel(bucket.outcomeCoverageRate)}</span>
                          <span>{fmtRsd(bucket.measuredImpactRsd, 0, "N/A")}</span>
                        </div>
                      </button>
                    ))}
                  </div>
                </section>
              )}

              {outcomeSummary.byOutcomeStatus.length > 0 && (
                <section className="aaq-breakdown-card" aria-labelledby="aaq-breakdown-outcome">
                  <h3 id="aaq-breakdown-outcome" className="aaq-breakdown-title">Po statusu ishoda</h3>
                  <div className="aaq-breakdown-list">
                    {outcomeSummary.byOutcomeStatus.map((bucket) => (
                      <div key={bucket.key} className="aaq-breakdown-row">
                        <div>
                          <strong>{getBucketHeading(bucket)}</strong>
                          <div className="aaq-breakdown-meta">
                            {fmtNumber(bucket.totalCount, 0, "0")} akcija · merljivo {fmtNumber(bucket.measuredCount, 0, "0")}
                          </div>
                        </div>
                        <div className="aaq-breakdown-values">
                          <span>Negativan {renderBucketRateLabel(bucket.negativeOutcomeRate)}</span>
                          <span>{fmtRsd(bucket.measuredImpactRsd, 0, "N/A")}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </section>
              )}
            </div>
          </>
        ) : null}
      </section>

      {activeSummaryFilters.length > 0 && (
        <div className="aaq-active-summary-filters" aria-label="Aktivni summary filteri">
          {activeSummaryFilters.length === 1 && (
            <button
              type="button"
              className="aaq-filter-chip"
              onClick={() => applySummaryBucketFilter(activeSummaryFilters[0].key, activeSummaryFilters[0].value)}
            >
              {activeSummaryFilters[0].label} ×
            </button>
          )}
          {activeSummaryFilters.length > 1 && (
            <button
              type="button"
              className="aaq-filter-chip aaq-filter-chip-reset"
              onClick={clearSummaryFilters}
            >
              Resetuj summary filtere
            </button>
          )}
          {activeSummaryFilters.length > 1 && (
            <span className="aaq-summary-filter-helper">
              Sažetak je sužen na {activeSummaryFilters.length} aktivna filtera:{" "}
              {activeSummaryFilters.map((filterItem, index) => (
                <Fragment key={`summary-helper-${filterItem.key}`}>
                  <button
                    type="button"
                    className="aaq-summary-filter-link"
                    onClick={() => applySummaryBucketFilter(filterItem.key, filterItem.value)}
                  >
                    {filterItem.label}
                  </button>
                  {index < activeSummaryFilters.length - 1 ? ", " : "."}
                </Fragment>
              ))}
            </span>
          )}
        </div>
      )}

      <div className="aaq-filters">
        <select
          value={filters.status ?? ""}
          onChange={(e) => setFilter("status", e.target.value as AnalyticsActionStatus)}
          className="aaq-select"
          aria-label="Filter po statusu"
        >
          <option value="">Svi statusi</option>
          {(Object.keys(STATUS_LABELS) as AnalyticsActionStatus[]).map((s) => (
            <option key={s} value={s}>{STATUS_LABELS[s]}</option>
          ))}
        </select>
        <select
          value={filters.priority ?? ""}
          onChange={(e) => setFilter("priority", e.target.value as AnalyticsActionPriority)}
          className="aaq-select"
          aria-label="Filter po prioritetu"
        >
          <option value="">Svi prioriteti</option>
          <option value="P1">P1</option>
          <option value="P2">P2</option>
          <option value="P3">P3</option>
        </select>
        <select
          value={filters.sourceType ?? ""}
          onChange={(e) => setFilter("sourceType", e.target.value as AnalyticsActionSourceType)}
          className="aaq-select"
          aria-label="Filter po izvoru"
        >
          <option value="">Svi izvori</option>
          {(Object.keys(SOURCE_LABELS) as AnalyticsActionSourceType[]).map((s) => (
            <option key={s} value={s}>{SOURCE_LABELS[s]}</option>
          ))}
        </select>
        <select
          value={filters.dataQualityStatus ?? ""}
          onChange={(e) => setFilter("dataQualityStatus", e.target.value)}
          className="aaq-select"
          aria-label="Filter po kvalitetu podataka"
        >
          <option value="">Svi kvaliteti</option>
          <option value="good">Dobar</option>
          <option value="warning">Upozorenje</option>
          <option value="critical">Kritičan</option>
          <option value="insufficient_data">Nedovoljno podataka</option>
        </select>
        <input
          type="search"
          placeholder="Pretraži..."
          value={filters.search ?? ""}
          onChange={(e) => setFilter("search", e.target.value)}
          className="aaq-search"
          aria-label="Pretraži akcije"
        />
      </div>

      {error && <div className="aaq-error">{error}</div>}
      {writeAccessMessage ? <div className="aaq-error" role="status">{writeAccessMessage}</div> : null}

      {loading ? (
        <div className="aaq-loading">Učitavanje...</div>
      ) : items.length === 0 ? (
        <div className="aaq-empty">
          <p>Nema akcija.</p>
          <p className="aaq-empty-hint">
            Dodajte akcije iz dashboarda, Product Decision Center-a ili Inventory workflow-a.
          </p>
        </div>
      ) : (
        <>
          <div className="aaq-table-wrap">
            <table className="aaq-table">
              <thead>
                <tr>
                  <th>P</th>
                  <th>Izvor</th>
                  <th>Naslov</th>
                  <th>Preporuka</th>
                  <th>Rok provere</th>
                  <th className="th-num">Uticaj (RSD)</th>
                  <th className="th-num">Očekivani uticaj</th>
                  <th className="th-num">Conf%</th>
                  <th>Data Q</th>
                  <th>Status ishoda</th>
                  <th>Status akcije</th>
                  <th>Akcije</th>
                  <th>Detalji</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => {
                  const busy = updatingId === item.id;
                  const isOpen = openStatuses.includes(item.status);
                  const isExpanded = expandedIds.includes(item.id);
                  const detailsItem = detailsById[item.id] ?? item;
                  const prettyMetadata = formatMetadataJson(detailsItem.metadataJson);
                  const detailError = detailsErrorById[item.id];
                  const isDetailLoading = detailsLoadingId === item.id;
                  const notes = detailsItem.notes ?? [];
                  const outcomeStatus = normalizeOutcomeStatus(item.outcomeStatus);
                  const outcomeNotesPreview = formatOutcomeNotesPreview(item.outcomeNotes);
                  const hasOutcomeUpdate = outcomeStatus != null || item.measuredImpactRsd != null || outcomeNotesPreview != null;

                  return (
                    <Fragment key={item.id}>
                      <tr className={`aaq-row status-${item.status}`}>
                        <td>
                          <span className={PRIORITY_CSS[item.priority]}>{item.priority}</span>
                        </td>
                        <td>
                          <span className="source-label">{SOURCE_LABELS[item.sourceType] ?? item.sourceType}</span>
                        </td>
                        <td className="td-title">
                          {item.actionUrl ? (
                            <a href={item.actionUrl} className="action-link">{item.title}</a>
                          ) : (
                            item.title
                          )}
                          {item.description && (
                            <div className="td-desc">{item.description}</div>
                          )}
                        </td>
                        <td className="td-rec">{item.recommendationStatus ?? "-"}</td>
                        <td>{formatTimestamp(item.dueAtUtc)}</td>
                        <td className="td-num">{fmtRsd(item.impactEstimateRsd, 0, "-")}</td>
                        <td className="td-num">{fmtRsd(item.expectedImpactRsd, 0, "-")}</td>
                        <td className="td-num">{item.confidencePct != null ? `${fmtNumber(item.confidencePct, 0, "-")}%` : "-"}</td>
                        <td>
                          {item.dataQualityStatus ? (
                            <span className={`dq-badge ${DATA_QUALITY_CSS[item.dataQualityStatus.toLowerCase()] ?? ""}`}>
                              {getDataQualityLabel(item.dataQualityStatus)}
                            </span>
                          ) : "-"}
                        </td>
                        <td>
                          {outcomeStatus ? (
                            <div>
                              <span className={OUTCOME_CSS[outcomeStatus]}>{OUTCOME_LABELS[outcomeStatus]}</span>
                              <div className="td-desc">Izmereni uticaj: {fmtRsd(item.measuredImpactRsd, 0, "-")}</div>
                              <div className="td-desc">Napomena: {outcomeNotesPreview ?? "-"}</div>
                            </div>
                          ) : (
                            <div>
                              <span>-</span>
                              <div className="td-desc">Izmereni uticaj: {fmtRsd(item.measuredImpactRsd, 0, "-")}</div>
                              <div className="td-desc">Napomena: {outcomeNotesPreview ?? "-"}</div>
                            </div>
                          )}
                        </td>
                        <td>
                          <span className={STATUS_CSS[item.status]}>{STATUS_LABELS[item.status]}</span>
                        </td>
                        <td className="td-actions">
                          {busy ? (
                            <span className="aaq-busy">...</span>
                          ) : (
                            <div className="action-btns">
                              {item.status === "new" && (
                                <button
                                  className="btn-action btn-accept"
                                  onClick={() => void changeStatus(item.id, "accepted")}
                                  title="Prihvati"
                                >
                                  Prihvati
                                </button>
                              )}
                              {isOpen && item.status !== "deferred" && (
                                <button
                                  className="btn-action btn-defer"
                                  onClick={() => openStatusNoteModal(item, "deferred")}
                                  title="Odloži"
                                >
                                  Odloži
                                </button>
                              )}
                              {item.status === "accepted" && (
                                <button
                                  className="btn-action btn-done"
                                  onClick={() => openStatusNoteModal(item, "done")}
                                  title="Označi kao završeno"
                                >
                                  Zavrsi
                                </button>
                              )}
                              {isOpen && (
                                <button
                                  className="btn-action btn-reject"
                                  onClick={() => openStatusNoteModal(item, "rejected")}
                                  title="Odbij"
                                >
                                  Odbij
                                </button>
                              )}
                              <button
                                className="btn-action btn-details"
                                onClick={() => openOutcomeModal(item)}
                                title={hasOutcomeUpdate ? "Ažuriraj ishod" : "Označi ishod"}
                              >
                                {hasOutcomeUpdate ? "Ažuriraj ishod" : "Označi ishod"}
                              </button>
                            </div>
                          )}
                        </td>
                        <td className="td-expand">
                          <button
                            type="button"
                            className="btn-action btn-details"
                            onClick={() => void toggleExpanded(item)}
                            aria-expanded={isExpanded}
                            aria-controls={`aaq-details-${item.id}`}
                          >
                            {isExpanded ? "Sakrij" : "Detalji"}
                          </button>
                        </td>
                      </tr>
                      {isExpanded && (
                        <tr id={`aaq-details-${item.id}`} className="aaq-row-details">
                          <td colSpan={13}>
                            <div className="aaq-details-grid">
                              <div>
                                <strong>Izvorni ekran:</strong>{" "}
                                {detailsItem.actionUrl ? <a href={detailsItem.actionUrl} className="action-link">Otvori</a> : "-"}
                              </div>
                              <div><strong>Rok provere:</strong> {formatTimestamp(detailsItem.dueAtUtc)}</div>
                              <div><strong>Očekivani uticaj:</strong> {fmtRsd(detailsItem.expectedImpactRsd, 0, "-")}</div>
                              <div><strong>Izmereni uticaj:</strong> {fmtRsd(detailsItem.measuredImpactRsd, 0, "-")}</div>
                              <div><strong>Status akcije:</strong> {STATUS_LABELS[detailsItem.status]}</div>
                              <div><strong>Status ishoda:</strong> {normalizeOutcomeStatus(detailsItem.outcomeStatus) ? OUTCOME_LABELS[normalizeOutcomeStatus(detailsItem.outcomeStatus)!] : "-"}</div>
                              <div><strong>Datum merenja ishoda:</strong> {formatTimestamp(detailsItem.outcomeMeasuredAtUtc)}</div>
                              <div><strong>Napomena ishoda:</strong> {detailsItem.outcomeNotes?.trim() ? detailsItem.outcomeNotes : "-"}</div>
                            </div>
                            {isDetailLoading && (
                              <div className="aaq-detail-loading">Učitavanje istorije...</div>
                            )}
                            {detailError && (
                              <div className="aaq-detail-error">{detailError}</div>
                            )}
                            <details className="aaq-metadata-panel">
                              <summary>Tehnički detalji</summary>
                              <div className="aaq-details-grid">
                                <div><strong>SourceType:</strong> {detailsItem.sourceType}</div>
                                <div><strong>SourceKey:</strong> {detailsItem.sourceKey}</div>
                                <div><strong>SourceId:</strong> {detailsItem.sourceId ?? "-"}</div>
                                <div><strong>CreatedAtUtc:</strong> {formatTimestamp(detailsItem.createdAtUtc)}</div>
                                <div><strong>UpdatedAtUtc:</strong> {formatTimestamp(detailsItem.updatedAtUtc)}</div>
                                <div><strong>ResolvedAtUtc:</strong> {formatTimestamp(detailsItem.resolvedAtUtc)}</div>
                                <div><strong>CreatedByUserId:</strong> {detailsItem.createdByUserId ?? "-"}</div>
                                <div><strong>UpdatedByUserName:</strong> {detailsItem.updatedByUserName ?? "-"}</div>
                              </div>
                              <strong>MetadataJson:</strong>
                              {prettyMetadata ? (
                                <pre>{prettyMetadata}</pre>
                              ) : (
                                <p>Metadata nije dostupan.</p>
                              )}
                            </details>
                            <div className="aaq-notes-timeline">
                              <strong>Istorija statusa i napomena</strong>
                              {notes.length === 0 ? (
                                <p className="aaq-note-empty">Nema zabeleženih promena statusa.</p>
                              ) : (
                                notes.map((entry) => (
                                  <div key={entry.id} className="aaq-note-item">
                                    <div className="aaq-note-header">
                                      <span>{formatTimestamp(entry.createdAtUtc)}</span>
                                      <span className="aaq-note-status">
                                        {STATUS_LABELS[entry.statusFrom]} {"->"} {STATUS_LABELS[entry.statusTo]}
                                      </span>
                                    </div>
                                    <div className="aaq-note-user">
                                      Korisnik: {entry.createdByUserName || entry.createdByUserId || "Sistem"}
                                    </div>
                                    <div className="aaq-note-body">
                                      {entry.note?.trim() ? entry.note : "Status promenjen bez napomene."}
                                    </div>
                                  </div>
                                ))
                              )}
                            </div>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="aaq-pagination">
              <button
                disabled={page <= 1}
                onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) - 1 }))}
                className="btn-page"
              >
                {"<-"} Prethodna
              </button>
              <span className="page-info">
                Strana {page} / {totalPages} ({totalCount} ukupno)
              </span>
              <button
                disabled={page >= totalPages}
                onClick={() => setFilters((f) => ({ ...f, page: (f.page ?? 1) + 1 }))}
                className="btn-page"
              >
                Sledeća {"->"}
              </button>
            </div>
          )}
        </>
      )}

      {statusModal && (
        <div className="aaq-modal-backdrop" role="presentation" onClick={() => !statusModalBusy && setStatusModal(null)}>
          <div
            className="aaq-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="aaq-note-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 id="aaq-note-modal-title">Napomena uz status</h2>
            <p className="aaq-modal-subtitle">
              Menjate status akcije na <strong>{STATUS_LABELS[statusModal.status]}</strong>:
            </p>
            <p className="aaq-modal-title">{statusModal.title}</p>
            <label htmlFor="aaq-note-textarea">Napomena (opciono)</label>
            <textarea
              id="aaq-note-textarea"
              value={statusModal.note}
              onChange={(e) => setStatusModal((current) => current ? { ...current, note: e.target.value } : current)}
              rows={4}
              placeholder="Unesite kratku napomenu..."
              disabled={statusModalBusy}
            />
            <div className="aaq-modal-actions">
              <button type="button" className="btn-page" onClick={() => setStatusModal(null)} disabled={statusModalBusy}>
                Otkaži
              </button>
              <button type="button" className="btn-action btn-done" onClick={() => void submitStatusModal()} disabled={statusModalBusy}>
                {statusModalBusy ? "Čuvanje..." : "Potvrdi"}
              </button>
            </div>
          </div>
        </div>
      )}

      {outcomeModal && (
        <div className="aaq-modal-backdrop" role="presentation" onClick={() => !outcomeModalBusy && setOutcomeModal(null)}>
          <div
            className="aaq-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="aaq-outcome-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 id="aaq-outcome-modal-title">Ažuriraj ishod</h2>
            <p className="aaq-modal-subtitle">
              Beležite ishod za akciju <strong>{outcomeModal.title}</strong>.
            </p>
            {outcomeModalError ? <div className="aaq-error" role="alert">{outcomeModalError}</div> : null}
            <label htmlFor="aaq-outcome-status">Ishod</label>
            <select
              id="aaq-outcome-status"
              value={outcomeModal.outcomeStatus}
              onChange={(e) => {
                setOutcomeModalError(null);
                setOutcomeModal((current) => {
                  if (!current) return current;
                  const nextStatus = e.target.value as OutcomeModalState["outcomeStatus"];
                  if (nextStatus === "pending") {
                    return {
                      ...current,
                      outcomeStatus: nextStatus,
                      measuredImpactRsd: "",
                      outcomeMeasuredAtLocal: "",
                    };
                  }

                  return { ...current, outcomeStatus: nextStatus };
                });
              }}
              disabled={outcomeModalBusy}
            >
              {(Object.keys(OUTCOME_LABELS) as OutcomeModalState["outcomeStatus"][]).map((status) => (
                <option key={status} value={status}>{OUTCOME_LABELS[status]}</option>
              ))}
            </select>
            <label htmlFor="aaq-outcome-impact">Merljivi uticaj (RSD)</label>
            <input
              id="aaq-outcome-impact"
              type="number"
              step="0.01"
              value={outcomeModal.measuredImpactRsd}
              onChange={(e) => {
                setOutcomeModalError(null);
                setOutcomeModal((current) => current ? { ...current, measuredImpactRsd: e.target.value } : current);
              }}
              placeholder="npr. 12500"
              disabled={outcomeModalBusy || outcomeModal.outcomeStatus === "pending"}
            />
            <label htmlFor="aaq-outcome-measured-at">Datum merenja ishoda</label>
            <input
              id="aaq-outcome-measured-at"
              type="datetime-local"
              value={outcomeModal.outcomeMeasuredAtLocal}
              onChange={(e) => {
                setOutcomeModalError(null);
                setOutcomeModal((current) => current ? { ...current, outcomeMeasuredAtLocal: e.target.value } : current);
              }}
              disabled={outcomeModalBusy || outcomeModal.outcomeStatus === "pending"}
            />
            <label htmlFor="aaq-outcome-notes">Napomena</label>
            <textarea
              id="aaq-outcome-notes"
              value={outcomeModal.outcomeNotes}
              onChange={(e) => {
                setOutcomeModalError(null);
                setOutcomeModal((current) => current ? { ...current, outcomeNotes: e.target.value } : current);
              }}
              rows={4}
              placeholder="Kratko zabeležite šta se desilo..."
              disabled={outcomeModalBusy}
            />
            <div className="aaq-modal-actions">
              <button type="button" className="btn-page" onClick={() => setOutcomeModal(null)} disabled={outcomeModalBusy}>
                Otkaži
              </button>
              <button type="button" className="btn-action btn-done" onClick={() => void submitOutcomeModal()} disabled={outcomeModalBusy}>
                {outcomeModalBusy ? "Čuvanje..." : "Ažuriraj ishod"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
