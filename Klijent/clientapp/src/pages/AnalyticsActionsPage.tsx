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
  AnalyticsActionImpactLedger,
  AnalyticsActionCounts,
  AnalyticsActionFilters,
  AnalyticsActionLedgerSnapshot,
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
  supplier: "Dobavljači",
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
  created: "akcije kreirane",
  resolved: "akcije zatvorene",
  measured: "ishodi mereni",
  mixed: "akcije u kombinovanom periodu",
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

const FRESHNESS_LABELS: Record<string, string> = {
  fresh: "Sveže",
  stale: "Zastarelo",
  critical: "Kritično",
  unknown: "Nepoznato",
};

const CONFIDENCE_LEVEL_LABELS: Record<string, string> = {
  high: "Visoka",
  medium: "Srednja",
  low: "Niska",
  insufficient_data: "Nedovoljno podataka",
};

const RECOMMENDATION_TYPE_LABELS: Record<string, string> = {
  BOOST: "Pojačaj",
  REPLENISH: "Dopuni",
  WATCH: "Prati",
  MARKDOWN: "Snizi cenu",
  DO_NOT_ORDER: "Ne naručivati",
  FIX_DATA: "Proveriti podatke",
  INSUFFICIENT_DATA: "Nedovoljno podataka",
};

const ACTION_CODE_LABELS: Record<string, string> = {
  stock_cover_days: "Pokrivenost zalihe",
  sell_through: "Obrt zalihe",
  sales_velocity: "Brzina prodaje",
  stock_risk: "Rizik zalihe",
  margin: "Marža",
  trend: "Trend",
  supplier_reliability: "Pouzdanost dobavljača",
  missing_cost: "Nedostaje nabavna cena",
  sparse_sales: "Malo prodaje",
  low_cover: "Niska pokrivenost",
  low: "Niska pokrivenost",
  out_of_stock_risk: "Rizik rasprodaje",
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

function formatList(values: string[] | null | undefined): string {
  if (!values || values.length === 0) return "-";
  return values.map(formatActionCodeLabel).join(", ");
}

function formatImpactLedgerPeriod(startUtc: string | null | undefined, endUtc: string | null | undefined): string {
  if (!startUtc && !endUtc) return "-";
  return `${formatTimestamp(startUtc)} - ${formatTimestamp(endUtc)}`;
}

function formatOutcomeNotesPreview(value: string | null | undefined): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (trimmed.length === 0) return null;
  if (trimmed.length <= OUTCOME_NOTES_PREVIEW_LIMIT) return trimmed;
  return `${trimmed.slice(0, OUTCOME_NOTES_PREVIEW_LIMIT - 3)}...`;
}

function formatSummaryWindow(summary: AnalyticsActionOutcomeSummaryResponse | null): string {
  if (!summary) return "akcije kreirane u poslednjih 90 dana";

  const periodMode = summary.meta.periodMode;
  const from = summary.meta.createdFrom ?? summary.meta.resolvedFrom ?? summary.meta.measuredFrom;
  const to = summary.meta.createdTo ?? summary.meta.resolvedTo ?? summary.meta.measuredTo;

  if (periodMode === "created") {
    if (from && to) {
      return `akcije kreirane u poslednjih 90 dana (${formatDateTime(from, "-")} - ${formatDateTime(to, "-")})`;
    }

    return "akcije kreirane u poslednjih 90 dana";
  }

  const periodLabel = PERIOD_MODE_LABELS[periodMode] ?? "akcije u izabranom periodu";
  if (from && to) {
    return `${periodLabel} (${formatDateTime(from, "-")} - ${formatDateTime(to, "-")})`;
  }

  return `${periodLabel} u poslednjih 90 dana`;
}

function getOutcomeSummaryWarningLabel(code: string): string {
  return OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code;
}

function renderBucketRateLabel(rate: number | null | undefined): string {
  return fmtPctFromRatio(rate, 0, "N/A");
}

type OutcomeRateContract = {
  outcomeCoverageRate?: number | null;
  positiveOutcomeRate?: number | null;
  negativeOutcomeRate?: number | null;
  closedOutcomeCoverageRate?: number | null;
  measuredPositiveOutcomeRate?: number | null;
  measuredNegativeOutcomeRate?: number | null;
};

type MeasuredOutcomeContract = {
  measuredCount: number;
  measuredOutcomeCount?: number | null;
};

function getClosedOutcomeCoverageRate(value: OutcomeRateContract): number | null | undefined {
  return value.closedOutcomeCoverageRate ?? value.outcomeCoverageRate;
}

function getMeasuredPositiveOutcomeRate(value: OutcomeRateContract): number | null | undefined {
  return value.measuredPositiveOutcomeRate ?? value.positiveOutcomeRate;
}

function getMeasuredNegativeOutcomeRate(value: OutcomeRateContract): number | null | undefined {
  return value.measuredNegativeOutcomeRate ?? value.negativeOutcomeRate;
}

function getMeasuredOutcomeCount(value: MeasuredOutcomeContract): number {
  return value.measuredOutcomeCount ?? value.measuredCount;
}

function isMeasuredOutcomeLocked(status: AnalyticsActionOutcomeUpdateInput["outcomeStatus"]): boolean {
  return status === "pending" || status === "not_measured";
}

function getOutcomeModalGuidance(status: AnalyticsActionOutcomeUpdateInput["outcomeStatus"]): {
  tone: "pending" | "not-measured" | "measured";
  text: string;
} {
  if (status === "pending") {
    return {
      tone: "pending",
      text: "Čeka proveru: merljivi uticaj, datum merenja i izvor dokaza nisu obavezni i ostaju prazni dok merenje nije završeno. Ne unosite 0 RSD kao zamenu za nedostajuće merenje.",
    };
  }

  if (status === "not_measured") {
    return {
      tone: "not-measured",
      text: "Nije izmereno: akcija je zatvorena bez merljivog dokaza. Polja za uticaj, datum i izvor dokaza namerno su nedostupna — to nije greška i nije 0 RSD.",
    };
  }

  return {
    tone: "measured",
    text: "Za pozitivan, neutralan ili negativan ishod izvor dokaza je obavezan. Merljivi uticaj i datum merenja unesite samo kada postoji stvarno merenje; prazno polje znači da broj još nije dostupan.",
  };
}

function formatImpactValue(value: number | null | undefined, unavailableLabel: string): string {
  return value == null ? unavailableLabel : fmtRsd(value, 0, "-");
}

function formatWindowDays(value: number | null | undefined, unavailableLabel: string): string {
  if (value == null) return unavailableLabel;
  return value === 1 ? "1 dan" : `${fmtNumber(value, 0, "0")} dana`;
}

function formatLedgerList(values: string[] | null | undefined, unavailableLabel: string): string {
  return values && values.length > 0 ? values.map(formatActionCodeLabel).join(", ") : unavailableLabel;
}

function formatActionCodeLabel(value: string): string {
  const normalized = value.trim();
  if (!normalized) return value;
  return ACTION_CODE_LABELS[normalized.toLowerCase()] ?? normalized.replaceAll("_", " ");
}

function formatSourceModuleLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim();
  if (!normalized) return "-";
  if (normalized in SOURCE_LABELS) {
    return SOURCE_LABELS[normalized as AnalyticsActionSourceType];
  }
  return normalized.replaceAll("_", " ");
}

function formatFreshnessLabel(value: string | null | undefined): string {
  if (!value) return "Nije evidentirano";
  return FRESHNESS_LABELS[value] ?? value;
}

function formatConfidenceLevelLabel(value: string | null | undefined): string {
  if (!value) return "Nije evidentirano";
  return CONFIDENCE_LEVEL_LABELS[value] ?? value;
}

function formatRecommendationTypeLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim();
  if (!normalized) return "Nije evidentirano";
  return RECOMMENDATION_TYPE_LABELS[normalized.toUpperCase()] ?? normalized.replaceAll("_", " ");
}

function getMeasuredImpactLabel(item: AnalyticsActionItem): string {
  const outcomeStatus = normalizeOutcomeStatus(item.outcomeStatus);
  if (item.measuredImpactRsd != null) {
    return fmtRsd(item.measuredImpactRsd, 0, "-");
  }

  if (outcomeStatus === "pending") {
    return "Još nije izmereno";
  }

  if (outcomeStatus === "not_measured") {
    return "Nije mereno";
  }

  return "Nije dostupno";
}

function getOutcomeStatusLabel(value: string | null | undefined): string {
  const normalized = normalizeOutcomeStatus(value);
  return normalized ? OUTCOME_LABELS[normalized] : "Ishod nije evidentiran";
}

function hasConfirmedOutcomeEvidence(item: AnalyticsActionItem): boolean | null {
  const ledgerEvidence = item.impactLedger?.derived.hasEvidence;
  if (ledgerEvidence != null) {
    return ledgerEvidence;
  }

  const resolutionSnapshot = item.ledgerSnapshot?.resolutionSnapshot;
  const evidenceSource = resolutionSnapshot?.evidenceSource?.trim();
  if (evidenceSource) {
    return true;
  }

  if (item.measuredImpactRsd != null || item.outcomeMeasuredAtUtc) {
    return true;
  }

  return null;
}

function getOutcomeStateMessage(item: AnalyticsActionItem): string {
  const outcomeStatus = normalizeOutcomeStatus(item.outcomeStatus);
  const hasEvidence = hasConfirmedOutcomeEvidence(item);

  if (outcomeStatus === "pending") {
    return "Ishod je još u toku. Merljivi uticaj ostaje nedostupan dok merenje ne bude završeno.";
  }

  if (outcomeStatus === "not_measured") {
    return "Akcija je zatvorena bez dovoljno dokaza za merljiv ishod.";
  }

  if (
    (outcomeStatus === "success" || outcomeStatus === "neutral" || outcomeStatus === "negative")
    && hasEvidence !== true
    && item.measuredImpactRsd == null
    && !item.outcomeMeasuredAtUtc
  ) {
    return "Ishod je evidentiran kvalitativno, ali bez potvrđenog dokaza i merljivog traga.";
  }

  if (!outcomeStatus && item.measuredImpactRsd == null) {
    return "Ishod još nije evidentiran za ovu akciju.";
  }

  if (item.measuredImpactRsd == null) {
    return "Status ishoda je evidentiran, ali izmereni uticaj još nije dostupan.";
  }

  return "Izmereni ishod je evidentiran. Tumačite ga zajedno sa dokazom i periodom merenja.";
}

function getLedgerSchemaLabel(snapshot: AnalyticsActionLedgerSnapshot | null | undefined): string {
  return snapshot ? `v${fmtNumber(snapshot.schemaVersion, 0, "0")}` : "Nije dostupno";
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

function getImpactLedger(item: AnalyticsActionItem): AnalyticsActionImpactLedger | null {
  return item.impactLedger ?? null;
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
  evidenceSource: string;
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
      evidenceSource: item.impactLedger?.resolution?.evidenceSource ?? item.ledgerSnapshot?.resolutionSnapshot?.evidenceSource ?? "",
    });
  }

  async function submitOutcomeModal() {
    if (!outcomeModal) return;

    if (!outcomeModal.outcomeStatus) {
      setOutcomeModalError("Status ishoda je obavezan.");
      return;
    }

    const locksMeasuredState = isMeasuredOutcomeLocked(outcomeModal.outcomeStatus);
    const authoritativeOutcome = outcomeModal.outcomeStatus === "success"
      || outcomeModal.outcomeStatus === "neutral"
      || outcomeModal.outcomeStatus === "negative";
    const measuredImpactRaw = outcomeModal.measuredImpactRsd.trim();
    const parsedImpact = locksMeasuredState || measuredImpactRaw.length === 0
      ? null
      : Number(measuredImpactRaw.replace(",", "."));

    if (parsedImpact != null && !Number.isFinite(parsedImpact)) {
      setOutcomeModalError("Ishod nije sačuvan. Proverite status i iznos.");
      return;
    }

    const evidenceSource = outcomeModal.evidenceSource.trim();
    if (authoritativeOutcome && evidenceSource.length === 0) {
      setOutcomeModalError("Izvor dokaza je obavezan za pozitivan, neutralan i negativan ishod.");
      return;
    }

    const measuredAtIso = locksMeasuredState ? null : dateTimeLocalToIso(outcomeModal.outcomeMeasuredAtLocal);
    if (!locksMeasuredState && outcomeModal.outcomeMeasuredAtLocal.trim().length > 0 && !measuredAtIso) {
      setOutcomeModalError("Unesite validan datum merenja ishoda.");
      return;
    }

    setWriteAccessMessage(null);
    setOutcomeModalBusy(true);
    setOutcomeModalError(null);
    try {
      const result = await updateAnalyticsActionOutcome(outcomeModal.id, {
        outcomeStatus: outcomeModal.outcomeStatus,
        measuredImpactRsd: locksMeasuredState ? null : parsedImpact,
        outcomeMeasuredAtUtc: measuredAtIso,
        outcomeNotes: outcomeModal.outcomeNotes.trim().length > 0 ? outcomeModal.outcomeNotes.trim() : null,
        evidenceSource: authoritativeOutcome ? evidenceSource : null,
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
  const outcomeModalGuidance = outcomeModal
    ? getOutcomeModalGuidance(outcomeModal.outcomeStatus)
    : null;

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
                <span className="aaq-summary-card-label">Pokrivenost zatvorenih</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(getClosedOutcomeCoverageRate(outcomeSummary.totals), 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Na osnovu zatvorenih akcija</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Pozitivan od izmerenih</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(getMeasuredPositiveOutcomeRate(outcomeSummary.totals), 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Negativan od izmerenih: {fmtPctFromRatio(getMeasuredNegativeOutcomeRate(outcomeSummary.totals), 0, "N/A")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Izmereni uticaj</span>
                <strong className="aaq-summary-card-value">{fmtRsd(outcomeSummary.impact.measuredImpactRsd, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Očekivano: {fmtRsd(outcomeSummary.impact.expectedImpactRsd, 0, "N/A")}</span>
              </div>
              <div className="aaq-summary-card">
                <span className="aaq-summary-card-label">Realizacija plana</span>
                <strong className="aaq-summary-card-value">{fmtPctFromRatio(outcomeSummary.impact.realizationRatio, 0, "N/A")}</strong>
                <span className="aaq-summary-card-note">Uzorak uticaja: {fmtNumber(outcomeSummary.impact.measuredImpactSampleCount, 0, "0")} od {fmtNumber(getMeasuredOutcomeCount(outcomeSummary.totals), 0, "0")} izmerenih ishoda</span>{outcomeSummary.impact.measuredImpactSampleCount < getMeasuredOutcomeCount(outcomeSummary.totals) ? (
                  <span className="aaq-summary-card-note">Realizacija pokriva samo poduzorak sa izmerenim uticajem.</span>
                ) : null}
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
                        <span>Pozitivan {renderBucketRateLabel(getMeasuredPositiveOutcomeRate(bucket))}</span>
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
                        <span>Merljivo {fmtNumber(getMeasuredOutcomeCount(bucket), 0, "0")}</span>
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
                          <span>Pokrivenost {renderBucketRateLabel(getClosedOutcomeCoverageRate(bucket))}</span>
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
                          <span>Negativan {renderBucketRateLabel(getMeasuredNegativeOutcomeRate(bucket))}</span>
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
                  const impactLedger = getImpactLedger(detailsItem);
                  const detailError = detailsErrorById[item.id];
                  const isDetailLoading = detailsLoadingId === item.id;
                  const notes = detailsItem.notes ?? [];
                  const outcomeStatus = normalizeOutcomeStatus(item.outcomeStatus);
                  const outcomeNotesPreview = formatOutcomeNotesPreview(item.outcomeNotes);
                  const hasOutcomeUpdate = outcomeStatus != null || item.measuredImpactRsd != null || outcomeNotesPreview != null;
                  const creationSnapshot = detailsItem.ledgerSnapshot?.creationSnapshot ?? null;
                  const resolutionSnapshot = detailsItem.ledgerSnapshot?.resolutionSnapshot ?? null;

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
                        <td className="td-rec">{formatRecommendationTypeLabel(item.recommendationStatus)}</td>
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
                              <div className="td-desc">Izmereni uticaj: {getMeasuredImpactLabel(item)}</div>
                              <div className="td-desc">Napomena: {outcomeNotesPreview ?? "-"}</div>
                            </div>
                          ) : (
                            <div>
                              <span>Ishod nije evidentiran</span>
                              <div className="td-desc">Izmereni uticaj: {getMeasuredImpactLabel(item)}</div>
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
                                  Završi
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
                            <div className="aaq-detail-sections">
                              <section className="aaq-detail-card" aria-label={`Detalji ishoda za ${detailsItem.title}`}>
                                <h3 className="aaq-detail-card-title">Pregled ishoda</h3>
                                <p className="aaq-detail-card-note">{getOutcomeStateMessage(detailsItem)}</p>
                                <div className="aaq-details-grid">
                                  <div><strong>Status ishoda:</strong> {getOutcomeStatusLabel(detailsItem.outcomeStatus)}</div>
                                  <div><strong>Očekivani uticaj:</strong> {formatImpactValue(detailsItem.expectedImpactRsd, "Nije procenjeno")}</div>
                                  <div><strong>Izmereni uticaj:</strong> {getMeasuredImpactLabel(detailsItem)}</div>
                                  <div><strong>Datum merenja ishoda:</strong> {formatTimestamp(detailsItem.outcomeMeasuredAtUtc)}</div>
                                  <div><strong>Period merenja:</strong> {formatWindowDays(resolutionSnapshot?.measuredWindowDays, "Nije evidentirano")}</div>
                                  <div><strong>Izvor dokaza:</strong> {resolutionSnapshot?.evidenceSource?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>Referenca dokaza:</strong> {resolutionSnapshot?.evidenceReference?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>Napomena ishoda:</strong> {detailsItem.outcomeNotes?.trim() ? detailsItem.outcomeNotes : "-"}</div>
                                  <div><strong>Rezoluciona beleška:</strong> {resolutionSnapshot?.resolutionNote?.trim() || "Nije evidentirano"}</div>
                                </div>
                              </section>

                              <section className="aaq-detail-card" aria-label={`Kontekst preporuke za ${detailsItem.title}`}>
                                <h3 className="aaq-detail-card-title">Kontekst preporuke</h3>
                                <div className="aaq-details-grid">
                                  <div><strong>Verzija ledger šeme:</strong> {getLedgerSchemaLabel(detailsItem.ledgerSnapshot)}</div>
                                  <div><strong>Tip preporuke:</strong> {formatRecommendationTypeLabel(creationSnapshot?.recommendationType)}</div>
                                  <div><strong>Nivo pouzdanosti:</strong> {formatConfidenceLevelLabel(creationSnapshot?.confidenceLevel)}</div>
                                  <div><strong>Svežina ulaza:</strong> {formatFreshnessLabel(creationSnapshot?.inputFreshnessStatus)}</div>
                                  <div><strong>Prozor uticaja:</strong> {formatWindowDays(creationSnapshot?.impactWindowDays, "Nije evidentirano")}</div>
                                  <div><strong>Generisano:</strong> {formatTimestamp(creationSnapshot?.generatedAtUtc)}</div>
                                  <div><strong>Osnova očekivanog uticaja:</strong> {creationSnapshot?.expectedImpactBasis?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>ID izvorne preporuke:</strong> {creationSnapshot?.sourceRecommendationId?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>Preporučena akcija:</strong> {creationSnapshot?.recommendedAction?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>Razlog odluke:</strong> {creationSnapshot?.decisionReason?.trim() || "Nije evidentirano"}</div>
                                  <div><strong>Primarni signali:</strong> {formatLedgerList(creationSnapshot?.primaryDrivers, "Nije evidentirano")}</div>
                                  <div><strong>Kodovi upozorenja:</strong> {formatLedgerList(creationSnapshot?.warningCodes, "Nije evidentirano")}</div>
                                </div>
                              </section>
                            </div>
                            <div className="aaq-details-grid">
                              <div>
                                <strong>Izvorni ekran:</strong>{" "}
                                {detailsItem.actionUrl ? <a href={detailsItem.actionUrl} className="action-link">Otvori</a> : "-"}
                              </div>
                              <div><strong>Rok provere:</strong> {formatTimestamp(detailsItem.dueAtUtc)}</div>
                              <div><strong>Očekivani uticaj:</strong> {fmtRsd(detailsItem.expectedImpactRsd, 0, "-")}</div>
                              <div><strong>Izmereni uticaj:</strong> {getMeasuredImpactLabel(detailsItem)}</div>
                              <div><strong>Status akcije:</strong> {STATUS_LABELS[detailsItem.status]}</div>
                              <div><strong>Status ishoda:</strong> {getOutcomeStatusLabel(detailsItem.outcomeStatus)}</div>
                              <div><strong>Datum merenja ishoda:</strong> {formatTimestamp(detailsItem.outcomeMeasuredAtUtc)}</div>
                              <div><strong>Napomena ishoda:</strong> {detailsItem.outcomeNotes?.trim() ? detailsItem.outcomeNotes : "-"}</div>
                            </div>
                            <div className="aaq-ledger-panel">
                              <div className="aaq-panel-title">Ledger uticaja</div>
                              {impactLedger ? (
                                <div className="aaq-details-grid">
                                  <div><strong>Ledger verzija:</strong> {impactLedger.version}</div>
                                  <div><strong>Izvor preporuke:</strong> {impactLedger.sourceRecommendationId ?? "-"}</div>
                                  <div><strong>Derivacija izvora:</strong> {impactLedger.sourceRecommendationIdDerivation}</div>
                                  <div><strong>Prikupljeno:</strong> {formatTimestamp(impactLedger.capturedAtUtc)}</div>
                                  <div><strong>Osnova očekivanja:</strong> {impactLedger.snapshot.expectedImpactBasis}</div>
                                  <div><strong>Primarni signali:</strong> {formatList(impactLedger.snapshot.primaryDrivers)}</div>
                                  <div><strong>Razlog odluke:</strong> {impactLedger.snapshot.decisionReason}</div>
                                  <div><strong>Preporučena akcija:</strong> {impactLedger.snapshot.recommendedAction}</div>
                                  <div><strong>Period izvora:</strong> {formatImpactLedgerPeriod(impactLedger.snapshot.sourcePeriodStartUtc, impactLedger.snapshot.sourcePeriodEndUtc)}</div>
                                  <div><strong>Izvorni modul:</strong> {formatSourceModuleLabel(impactLedger.snapshot.sourceModule)}</div>
                                  <div><strong>Svežina ulaza:</strong> {formatFreshnessLabel(impactLedger.snapshot.inputFreshnessStatus)}</div>
                                  <div><strong>Opseg signala:</strong> {impactLedger.snapshot.impactWindowDays != null ? `${impactLedger.snapshot.impactWindowDays} dana` : "-"}</div>
                                  <div><strong>Status ishoda:</strong> {normalizeOutcomeStatus(impactLedger.resolution.outcomeStatus) ? OUTCOME_LABELS[normalizeOutcomeStatus(impactLedger.resolution.outcomeStatus)!] : impactLedger.resolution.outcomeStatus}</div>
                                  <div><strong>Izmeren uticaj:</strong> {fmtRsd(impactLedger.resolution.measuredImpactRsd, 0, "N/A")}</div>
                                  <div><strong>Razlika uticaja:</strong> {fmtRsd(impactLedger.derived.impactDeltaRsd, 0, "N/A")}</div>
                                  <div><strong>Realizacija:</strong> {fmtPctFromRatio(impactLedger.derived.realizationRatio, 0, "N/A")}</div>
                                  <div><strong>Korekcioni bucket:</strong> {impactLedger.derived.calibrationBucket}</div>
                                  <div><strong>Dokaz:</strong> {impactLedger.derived.hasEvidence ? "Da" : "Ne"}</div>
                                  <div><strong>Metod merenja:</strong> {impactLedger.resolution.measurementMethod ?? "-"}</div>
                                  <div><strong>Izvor dokaza:</strong> {impactLedger.resolution.evidenceSource ?? "-"}</div>
                                  <div><strong>Datum merenja:</strong> {formatTimestamp(impactLedger.resolution.outcomeMeasuredAtUtc)}</div>
                                  <div><strong>Datum zatvaranja:</strong> {formatTimestamp(impactLedger.resolution.resolvedAtUtc)}</div>
                                  <div><strong>Opseg merenja:</strong> {impactLedger.resolution.measuredWindowDays != null ? `${impactLedger.resolution.measuredWindowDays} dana` : "-"}</div>
                                  <div><strong>Napomena:</strong> {impactLedger.resolution.resolutionNote ?? "-"}</div>
                                </div>
                              ) : (
                                <p className="aaq-ledger-empty">Ledger uticaja nije dostupan za ovaj red.</p>
                              )}
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
              Beležite ishod za akciju <strong>{outcomeModal.title}</strong>. Status određuje da li su merljiva polja obavezna, opciona ili namerno nedostupna.
            </p>
            {outcomeModalGuidance ? (
              <div
                className={`aaq-outcome-guidance aaq-outcome-guidance--${outcomeModalGuidance.tone}`}
                role="status"
                data-testid="aaq-outcome-guidance"
              >
                {outcomeModalGuidance.text}
              </div>
            ) : null}
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
                  if (isMeasuredOutcomeLocked(nextStatus)) {
                    return {
                      ...current,
                      outcomeStatus: nextStatus,
                      measuredImpactRsd: "",
                      outcomeMeasuredAtLocal: "",
                      evidenceSource: "",
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
            <div className={isMeasuredOutcomeLocked(outcomeModal.outcomeStatus) ? "aaq-outcome-fields aaq-outcome-fields--locked" : "aaq-outcome-fields"}>
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
                placeholder={isMeasuredOutcomeLocked(outcomeModal.outcomeStatus) ? "Nije dostupno za ovaj status" : "npr. 12500 (ostavite prazno ako nema merenja)"}
                disabled={outcomeModalBusy || isMeasuredOutcomeLocked(outcomeModal.outcomeStatus)}
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
                disabled={outcomeModalBusy || isMeasuredOutcomeLocked(outcomeModal.outcomeStatus)}
              />
              <label htmlFor="aaq-outcome-evidence-source">Izvor dokaza</label>
              <input
                id="aaq-outcome-evidence-source"
                type="text"
                value={outcomeModal.evidenceSource}
                onChange={(e) => {
                  setOutcomeModalError(null);
                  setOutcomeModal((current) => current ? { ...current, evidenceSource: e.target.value } : current);
                }}
                placeholder={isMeasuredOutcomeLocked(outcomeModal.outcomeStatus) ? "Nije potreban za ovaj status" : "npr. action_outcome_summary"}
                disabled={outcomeModalBusy || isMeasuredOutcomeLocked(outcomeModal.outcomeStatus)}
              />
              {!isMeasuredOutcomeLocked(outcomeModal.outcomeStatus) ? (
                <p className="aaq-modal-field-hint">
                  Izvor dokaza je obavezan. Merljivi uticaj i datum merenja unesite samo kada postoji stvarno merenje.
                </p>
              ) : (
                <p className="aaq-modal-field-hint">
                  Merljiva polja su zaključana za status „{OUTCOME_LABELS[outcomeModal.outcomeStatus]}”.
                </p>
              )}
            </div>
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
