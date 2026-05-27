import { Fragment, useCallback, useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import {
  getAnalyticsActionById,
  getAnalyticsActions,
  getAnalyticsActionCounts,
  updateAnalyticsActionOutcome,
  updateAnalyticsActionStatus,
} from "../services/analyticsApi";
import { fmtNumber, fmtRsd } from "../utils/analyticsFormatters";
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
  accepted: "Prihvaceno",
  deferred: "Odlozeno",
  rejected: "Odbijeno",
  done: "Zavrseno",
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
  critical: "Kritican",
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
  pending: "Na čekanju",
  success: "Uspešno",
  neutral: "Neutralno",
  negative: "Negativno",
  not_measured: "Nije mereno",
};

const OUTCOME_CSS: Record<AnalyticsActionOutcomeUpdateInput["outcomeStatus"], string> = {
  pending: "badge-outcome badge-pending",
  success: "badge-outcome badge-success",
  neutral: "badge-outcome badge-neutral",
  negative: "badge-outcome badge-negative",
  not_measured: "badge-outcome badge-not-measured",
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
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("sr-RS", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
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
      setError(e instanceof Error ? e.message : "Greska pri ucitavanju");
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

  useEffect(() => {
    void loadItems(filters);
    void loadCounts();
  }, [filters, loadItems, loadCounts]);

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

  async function changeStatus(id: number, status: AnalyticsActionStatus, note?: string): Promise<boolean> {
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
      alert(e instanceof Error ? e.message : "Greska pri azuriranju statusa");
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
        [id]: e instanceof Error ? e.message : "Greska pri ucitavanju detalja",
      }));
    } finally {
      setDetailsLoadingId((current) => (current === id ? null : current));
    }
  }

  function openStatusNoteModal(item: AnalyticsActionItem, status: AnalyticsActionStatus) {
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
      outcomeNotes: item.outcomeNotes ?? "",
    });
  }

  async function submitOutcomeModal() {
    if (!outcomeModal) return;
    const measuredImpact = outcomeModal.measuredImpactRsd.trim();
    const parsedImpact = measuredImpact.length > 0 ? Number(measuredImpact) : null;
    if (parsedImpact != null && !Number.isFinite(parsedImpact)) {
      setOutcomeModalError("Unesite ispravan broj za izmereni uticaj.");
      return;
    }

    setOutcomeModalBusy(true);
    setOutcomeModalError(null);
    try {
      const isPending = outcomeModal.outcomeStatus === "pending";
      const result = await updateAnalyticsActionOutcome(outcomeModal.id, {
        outcomeStatus: outcomeModal.outcomeStatus,
        measuredImpactRsd: isPending ? null : parsedImpact,
        outcomeMeasuredAtUtc: isPending ? null : undefined,
        outcomeNotes: outcomeModal.outcomeNotes.trim().length > 0 ? outcomeModal.outcomeNotes.trim() : null,
      });
      setItems((prev) => prev.map((it) => (it.id === result.id ? result : it)));
      setDetailsById((prev) => ({ ...prev, [result.id]: result }));
      setOutcomeModal(null);
    } catch (e) {
      const message = e instanceof Error ? e.message : "Ishod nije sacuvan. Pokusajte ponovo.";
      setOutcomeModalError(message);
    } finally {
      setOutcomeModalBusy(false);
    }
  }

  const openStatuses: AnalyticsActionStatus[] = ["new", "accepted", "deferred"];

  return (
    <div className="aaq-page">
      <AnalyticsTrustHeader
        title="Akcije i preporuke"
        description="Centralni red akcija iz dashboarda, analize proizvoda, dobavljaca, zaliha i nivelacija."
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
          Centralni red akcija iz dashboarda, analize proizvoda, dobavljaca, zaliha i nivelacija.
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
            <span className="kpi-label">Prihvaceno</span>
          </div>
          <div className="aaq-kpi-card kpi-deferred">
            <span className="kpi-value">{counts.deferred}</span>
            <span className="kpi-label">Odlozeno</span>
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
          <option value="critical">Kritican</option>
          <option value="insufficient_data">Nedovoljno podataka</option>
        </select>
        <input
          type="search"
          placeholder="Pretrazi..."
          value={filters.search ?? ""}
          onChange={(e) => setFilter("search", e.target.value)}
          className="aaq-search"
          aria-label="Pretrazi akcije"
        />
      </div>

      {error && <div className="aaq-error">{error}</div>}

      {loading ? (
        <div className="aaq-loading">Ucitavanje...</div>
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
                  <th>Ishod</th>
                  <th>Status</th>
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
                                  title="Odlozi"
                                >
                                  Odlozi
                                </button>
                              )}
                              {item.status === "accepted" && (
                                <button
                                  className="btn-action btn-done"
                                  onClick={() => openStatusNoteModal(item, "done")}
                                  title="Oznaci kao zavrseno"
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
                                title={hasOutcomeUpdate ? "Azuriraj ishod" : "Oznaci ishod"}
                              >
                                {hasOutcomeUpdate ? "Azuriraj ishod" : "Oznaci ishod"}
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
                              <div><strong>SourceType:</strong> {detailsItem.sourceType}</div>
                              <div><strong>SourceKey:</strong> {detailsItem.sourceKey}</div>
                              <div><strong>SourceId:</strong> {detailsItem.sourceId ?? "-"}</div>
                              <div><strong>DueAtUtc:</strong> {formatTimestamp(detailsItem.dueAtUtc)}</div>
                              <div><strong>CreatedAtUtc:</strong> {formatTimestamp(detailsItem.createdAtUtc)}</div>
                              <div><strong>UpdatedAtUtc:</strong> {formatTimestamp(detailsItem.updatedAtUtc)}</div>
                              <div><strong>ResolvedAtUtc:</strong> {formatTimestamp(detailsItem.resolvedAtUtc)}</div>
                              <div><strong>Ocekivani uticaj:</strong> {fmtRsd(detailsItem.expectedImpactRsd, 0, "-")}</div>
                              <div><strong>Izmereni uticaj:</strong> {fmtRsd(detailsItem.measuredImpactRsd, 0, "-")}</div>
                              <div><strong>Status akcije:</strong> {STATUS_LABELS[detailsItem.status]}</div>
                              <div><strong>Status ishoda:</strong> {normalizeOutcomeStatus(detailsItem.outcomeStatus) ? OUTCOME_LABELS[normalizeOutcomeStatus(detailsItem.outcomeStatus)!] : "-"}</div>
                              <div><strong>Izmereno:</strong> {formatTimestamp(detailsItem.outcomeMeasuredAtUtc)}</div>
                              <div><strong>CreatedByUserId:</strong> {detailsItem.createdByUserId ?? "-"}</div>
                              <div><strong>UpdatedByUserName:</strong> {detailsItem.updatedByUserName ?? "-"}</div>
                              <div>
                                <strong>Izvorni ekran:</strong>{" "}
                                {detailsItem.actionUrl ? <a href={detailsItem.actionUrl} className="action-link">Otvori</a> : "-"}
                              </div>
                            </div>
                            {detailsItem.outcomeNotes ? (
                              <div className="aaq-metadata-panel">
                                <strong>Napomena ishoda:</strong>
                                <p>{detailsItem.outcomeNotes}</p>
                              </div>
                            ) : null}
                            {isDetailLoading && (
                              <div className="aaq-detail-loading">Ucitavanje istorije...</div>
                            )}
                            {detailError && (
                              <div className="aaq-detail-error">{detailError}</div>
                            )}
                            <div className="aaq-metadata-panel">
                              <strong>MetadataJson:</strong>
                              {prettyMetadata ? (
                                <pre>{prettyMetadata}</pre>
                              ) : (
                                <p>Metadata nije dostupan.</p>
                              )}
                            </div>
                            <div className="aaq-notes-timeline">
                              <strong>Istorija statusa i napomena</strong>
                              {notes.length === 0 ? (
                                <p className="aaq-note-empty">Nema zabelezenih promena statusa.</p>
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
                Sledeca {"->"}
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
                Otkazi
              </button>
              <button type="button" className="btn-action btn-done" onClick={() => void submitStatusModal()} disabled={statusModalBusy}>
                {statusModalBusy ? "Cuvanje..." : "Potvrdi"}
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
            <h2 id="aaq-outcome-modal-title">{outcomeModal.outcomeStatus === "pending" ? "Oznaci ishod" : "Azuriraj ishod"}</h2>
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
                setOutcomeModal((current) => current ? { ...current, outcomeStatus: e.target.value as OutcomeModalState["outcomeStatus"] } : current);
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
              disabled={outcomeModalBusy}
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
                Otkazi
              </button>
              <button type="button" className="btn-action btn-done" onClick={() => void submitOutcomeModal()} disabled={outcomeModalBusy}>
                {outcomeModalBusy ? "Cuvanje..." : "Potvrdi"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
