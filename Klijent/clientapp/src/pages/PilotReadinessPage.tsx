import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import { assessPilotImportReadiness } from "../components/analytics/PilotDataQualityIntakeReport";
import {
  getAnalyticsActionCounts,
  getAnalyticsRefreshStatus,
  getInventoryAlerts,
  getPilotDataQualityIntakeReport,
  getPilotIntakeDurableReport,
  getSupplierDecisionDurableReport,
} from "../services/analyticsApi";
import type {
  AnalyticsActionCounts,
  AnalyticsRefreshStatus,
  InventoryAlertListDto,
  PilotDataQualityIntakeReport,
} from "../types/analytics";
import { fmtNumber, fmtPctFromRatio, formatDateTime } from "../utils/analyticsFormatters";
import "./PilotReadinessPage.css";

type Status = "ready" | "ready_with_warnings" | "not_ready" | "unknown";
type Tone = "good" | "warning" | "critical" | "neutral";

type Step = {
  key: string;
  title: string;
  status: Status;
  reason: string;
  actionLabel: string;
  actionHref: string;
  links: { label: string; href: string }[];
};

const ROUTES = {
  dataQuality: "/analytics/data-quality",
  dashboard: "/analytics",
  supplier: "/analytics/supplier",
  inventory: "/analytics/inventory",
  actions: "/analytics/actions",
  pilotIntakeReport: "/analytics/reports/pilot-intake",
  supplierReport: "/analytics/supplier/report",
  import: "/access-import",
  workers: "/admin/configuration?panel=workers",
};

function label(status: Status): string {
  if (status === "ready") return "Spremno";
  if (status === "ready_with_warnings") return "Spremno uz upozorenja";
  if (status === "not_ready") return "Nije spremno";
  return "Nepoznato";
}

function tone(status: Status): Tone {
  if (status === "ready") return "good";
  if (status === "ready_with_warnings") return "warning";
  if (status === "not_ready") return "critical";
  return "warning";
}

function combine(statuses: Status[]): Status {
  if (statuses.includes("not_ready")) return "not_ready";
  if (statuses.includes("ready_with_warnings")) return "ready_with_warnings";
  if (statuses.includes("unknown")) return "unknown";
  return "ready";
}

function count(value: number | null | undefined): string {
  return value == null ? "-" : fmtNumber(value, 0, "-");
}

function hoursAgo(value: string | null | undefined): number | null {
  if (!value) return null;
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return null;
  return Math.max(0, (Date.now() - parsed) / 3_600_000);
}

function sumActionCounts(counts: AnalyticsActionCounts | null): number {
  if (!counts) return 0;
  return counts.new + counts.accepted + counts.deferred + counts.rejected + counts.done + counts.p1Open;
}

function step(
  key: string,
  title: string,
  status: Status,
  reason: string,
  actionLabel: string,
  actionHref: string,
  links: { label: string; href: string }[],
): Step {
  return { key, title, status, reason, actionLabel, actionHref, links };
}

export default function PilotReadinessPage() {
  const [intakeReport, setIntakeReport] = useState<PilotDataQualityIntakeReport | null>(null);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [actionCounts, setActionCounts] = useState<AnalyticsActionCounts | null>(null);
  const [inventoryAlerts, setInventoryAlerts] = useState<InventoryAlertListDto | null>(null);
  const [reportAvailability, setReportAvailability] = useState({ pilotIntake: null as boolean | null, supplierDecision: null as boolean | null });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      setLoading(true);
      const results = await Promise.allSettled([
        getPilotDataQualityIntakeReport({ dataScope: "all" }),
        getAnalyticsRefreshStatus(),
        getAnalyticsActionCounts(),
        getInventoryAlerts({ top: 8 }),
        getPilotIntakeDurableReport({ dataScope: "all" }),
        getSupplierDecisionDurableReport({ dataScope: "all" }),
      ]);

      if (cancelled) return;

      const [intakeResult, refreshResult, actionResult, inventoryResult, pilotReportResult, supplierReportResult] = results;
      setIntakeReport(intakeResult.status === "fulfilled" ? intakeResult.value : null);
      setRefreshStatus(refreshResult.status === "fulfilled" ? refreshResult.value : null);
      setActionCounts(actionResult.status === "fulfilled" ? actionResult.value : null);
      setInventoryAlerts(inventoryResult.status === "fulfilled" ? inventoryResult.value : null);
      setReportAvailability({
        pilotIntake: pilotReportResult.status === "fulfilled",
        supplierDecision: supplierReportResult.status === "fulfilled",
      });
      setLoading(false);
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const readiness = useMemo(() => {
    const steps: Step[] = [];
    const report = intakeReport;
    const refresh = refreshStatus;

    const assessment = assessPilotImportReadiness(report, refresh, null);
    const reasons = [...assessment.reasons];
    const assessmentNextActions = [...assessment.nextActions];

    steps.push(step(
      "data-loaded",
      "Podaci učitani",
      !report ? "unknown" : !report.loadedData.articlesCount || !report.loadedData.saleItemsCount ? "not_ready" : (!report.loadedData.receiptsCount || !report.loadedData.suppliersCount ? "ready_with_warnings" : "ready"),
      !report ? "Nije moguće potvrditi da su podaci učitani." : `Artikli: ${count(report.loadedData.articlesCount)}, stavke prodaje: ${count(report.loadedData.saleItemsCount)}, računi: ${count(report.loadedData.receiptsCount)}, dobavljači: ${count(report.loadedData.suppliersCount)}.`,
      "Otvori import",
      ROUTES.import,
      [{ label: "Kvalitet podataka", href: ROUTES.dataQuality }],
    ));

    const dqStatus: Status = !report ? "unknown" : report.readinessStatus === "not_ready" ? "not_ready" : report.readinessStatus === "ready_with_warnings" ? "ready_with_warnings" : "ready";
    const dqWarnings: string[] = [];
    if (report) {
      if (report.impact.recommendationsBlockedCount > 0) dqWarnings.push(`Blokirane preporuke: ${count(report.impact.recommendationsBlockedCount)}.`);
      if (report.impact.revenueWithoutCostPercent >= 0.02) dqWarnings.push(`Prihod bez nabavne cene: ${fmtPctFromRatio(report.impact.revenueWithoutCostPercent, 1, "-")}.`);
      if (report.impact.articlesWithoutSupplierPercent >= 0.02) dqWarnings.push(`Artikli bez dobavljača: ${fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")}.`);
      if (report.issues.missingCategoryCount > 0) dqWarnings.push(`Artikli bez kategorije: ${count(report.issues.missingCategoryCount)}.`);
      if ((report.issues.missingColorCount ?? 0) > 0 || (report.issues.missingSizeCount ?? 0) > 0) dqWarnings.push("Nedostaju boja ili veličina.");
      if (report.impact.insufficientSignalCount > 0) dqWarnings.push(`Nedovoljni signali: ${count(report.impact.insufficientSignalCount)}.`);
    }
    steps.push(step(
      "data-quality",
      "Kvalitet podataka proveren",
      dqStatus,
      report ? `${report.readinessLabel}${dqWarnings.length ? ` ${dqWarnings.join(" ")}` : ""}` : "Pilot intake izveštaj nije dostupan.",
      "Otvori Data Quality",
      ROUTES.dataQuality,
      [{ label: "Pilot intake report", href: ROUTES.pilotIntakeReport }],
    ));

    let refreshStep: Status = "unknown";
    let refreshReason = "Status osvežavanja nije dostupan.";
    if (refresh) {
      const freshness = (refresh.dataFreshnessStatus ?? report?.dataFreshnessStatus ?? "unknown").toLowerCase();
      const age = hoursAgo(refresh.lastSuccessfulRefreshAtUtc ?? report?.lastRefreshAtUtc ?? null);
      if (freshness === "critical") {
        refreshStep = "not_ready";
        refreshReason = "Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.";
      } else if (!refresh.lastSuccessfulRefreshAtUtc && !refresh.isRunning) {
        refreshStep = "not_ready";
        refreshReason = "Nema potvrđenog uspešnog refresh-a.";
      } else {
        refreshStep = "ready";
        if (refresh.isRunning || freshness === "stale" || (age != null && age > 72) || refresh.workerWarning || refresh.workerProcessWarning || refresh.cacheWarning) {
          refreshStep = "ready_with_warnings";
          refreshReason = [
            refresh.isRunning ? `Refresh je u toku${refresh.currentStep ? ` (${refresh.currentStep})` : ""}.` : null,
            freshness === "stale" || (age != null && age > 72) ? "Poslednje uspešno osvežavanje je starije od 72h." : null,
            refresh.workerWarning ?? refresh.workerProcessWarning ?? null,
            refresh.cacheWarning ?? null,
          ].filter(Boolean).join(" ");
        } else {
          refreshReason = `Poslednje uspešno osvežavanje: ${formatDateTime(refresh.lastSuccessfulRefreshAtUtc, "-")}.`;
        }
      }
    }
    steps.push(step("refresh", "Analytics osvežen", refreshStep, refreshReason, "Otvori worker status", ROUTES.workers, [{ label: "Status osvežavanja", href: ROUTES.workers }]));

    let dashboardStatus: Status = "unknown";
    let dashboardReason = "Nije moguće potvrditi dashboard signal.";
    if (report && refresh) {
      const freshness = (refresh.dataFreshnessStatus ?? report.dataFreshnessStatus ?? "unknown").toLowerCase();
      const readinessStatus = (report.readinessStatus ?? "unknown").toLowerCase();
      if (readinessStatus === "not_ready" || freshness === "critical") {
        dashboardStatus = "not_ready";
        dashboardReason = "Dashboard nije spreman za sigurni pilot prikaz dok se ne reše osnovni blokeri.";
      } else if (readinessStatus === "ready_with_warnings" || freshness === "stale" || refresh.isRunning) {
        dashboardStatus = "ready_with_warnings";
        dashboardReason = "Dashboard je spreman za pregled, ali postoje upozorenja koja treba objasniti pilot timu.";
      } else {
        dashboardStatus = "ready";
        dashboardReason = "Dashboard može da se otvori za pilot pregled.";
      }
    }
    steps.push(step("dashboard", "Dashboard pregledan", dashboardStatus, dashboardReason, "Otvori dashboard", ROUTES.dashboard, [{ label: "Dashboard", href: ROUTES.dashboard }, { label: "Data Quality", href: ROUTES.dataQuality }]));

    const supplierStatus: Status = !report ? "unknown" : !report.loadedData.suppliersCount ? "ready_with_warnings" : (report.impact.articlesWithoutSupplierPercent >= 0.02 || report.issues.missingSupplierCount > 0 ? "ready_with_warnings" : "ready");
    const supplierReason = !report ? "Nije moguće potvrditi dobavljačke signale." : !report.loadedData.suppliersCount ? "Nema dobavljača u importu, pa je supplier scorecard ograničen." : `U importu je ${count(report.loadedData.suppliersCount)} dobavljača.${report.impact.articlesWithoutSupplierPercent >= 0.02 ? ` Artikli bez dobavljača: ${fmtPctFromRatio(report.impact.articlesWithoutSupplierPercent, 1, "-")}.` : ""}`;
    steps.push(step("suppliers", "Dobavljači provereni", supplierStatus, supplierReason, "Otvori dobavljače", ROUTES.supplier, [{ label: "Supplier scorecard", href: ROUTES.supplier }, { label: "Supplier report", href: ROUTES.supplierReport }]));

    const inventoryCritical = inventoryAlerts?.items.filter((item) => item.severity === "critical").length ?? 0;
    const inventoryWarnings = inventoryAlerts?.items.filter((item) => item.severity === "warning").length ?? 0;
    let inventoryStatus: Status = "unknown";
    let inventoryReason = "Inventory alert signal nije dostupan.";
    if (inventoryAlerts) {
      if (!inventoryAlerts.snapshotAvailable) {
        inventoryStatus = "unknown";
        inventoryReason = "Inventory snapshot nije dostupan.";
      } else if (inventoryCritical > 0) {
        inventoryStatus = "ready_with_warnings";
        inventoryReason = `Postoje ${count(inventoryCritical)} kritična inventory upozorenja.`;
      } else if (inventoryWarnings > 0 || inventoryAlerts.totalCount > 0) {
        inventoryStatus = "ready_with_warnings";
        inventoryReason = `Postoji ${count(inventoryAlerts.totalCount)} inventory alertova za pregled.`;
      } else {
        inventoryStatus = "ready";
        inventoryReason = "Nema aktivnih inventory alertova.";
      }
    }
    steps.push(step("inventory", "Lager rizici provereni", inventoryStatus, inventoryReason, "Otvori inventory", ROUTES.inventory, [{ label: "Inventory", href: ROUTES.inventory }]));

    const reportsStatus: Status =
      reportAvailability.pilotIntake === true && reportAvailability.supplierDecision === true
        ? "ready"
        : reportAvailability.pilotIntake === false && reportAvailability.supplierDecision === false
          ? "unknown"
          : reportAvailability.pilotIntake === null && reportAvailability.supplierDecision === null
            ? "unknown"
            : "ready_with_warnings";
    const reportsReason = reportAvailability.pilotIntake === true && reportAvailability.supplierDecision === true
      ? "Pilot intake i supplier report su dostupni."
      : reportAvailability.pilotIntake === false && reportAvailability.supplierDecision === false
        ? "Reportovi nisu potvrđeni kroz dostupne endpoint-e."
        : reportAvailability.pilotIntake === null && reportAvailability.supplierDecision === null
        ? "Nije moguće potvrditi dostupnost reportova."
        : "Dostupan je samo deo reportova.";
    steps.push(step("reports", "Izveštaji spremni", reportsStatus, reportsReason, "Otvori reports", ROUTES.pilotIntakeReport, [{ label: "Pilot intake report", href: ROUTES.pilotIntakeReport }, { label: "Supplier report", href: ROUTES.supplierReport }]));

    const totalActions = sumActionCounts(actionCounts);
    steps.push(step("actions", "Akcije kreirane", !actionCounts ? "unknown" : totalActions > 0 ? "ready" : "ready_with_warnings", !actionCounts ? "Nije moguće potvrditi status action queue-a." : totalActions > 0 ? `U queue-u je ${count(totalActions)} akcija. Novo: ${count(actionCounts.new)}, P1 otvorene: ${count(actionCounts.p1Open)}.` : "Još nema kreiranih akcija za pilot plan.", "Otvori action queue", ROUTES.actions, [{ label: "Action Queue", href: ROUTES.actions }]));

    const status = combine([assessment.status, ...steps.map((item) => item.status)]);
    const readyCount = steps.filter((item) => item.status === "ready").length;
    const statusReasons = [...reasons, ...steps.filter((item) => item.status !== "ready").map((item) => `${item.title}: ${item.reason}`)].filter((value, index, array) => array.indexOf(value) === index);
    const nextActionsCombined = [...assessmentNextActions, ...steps.filter((item) => item.status !== "ready").map((item) => `Otvori ${item.title.toLowerCase()}.`)].filter((value, index, array) => array.indexOf(value) === index).slice(0, 6);

    return { status, tone: tone(status), label: label(status), summary: assessment.summary, reasons: statusReasons.slice(0, 6), nextActions: nextActionsCombined, readyCount, steps, reports: reportAvailability };
  }, [actionCounts, intakeReport, inventoryAlerts, refreshStatus, reportAvailability]);

  const dataQualitySummary = intakeReport
    ? {
        missingSupplierCount: intakeReport.issues.missingSupplierCount,
        missingCostCount: intakeReport.issues.missingCostCount,
        missingCategoryCount: intakeReport.issues.missingCategoryCount,
        insufficientSignalCount: intakeReport.impact.insufficientSignalCount,
        ignoredRowsCount: intakeReport.impact.ignoredRowsCount,
      }
    : undefined;
  const dataQualityStatus = intakeReport
    ? intakeReport.readinessStatus === "ready"
      ? "good"
      : intakeReport.readinessStatus === "ready_with_warnings"
        ? "warning"
        : intakeReport.readinessStatus === "not_ready"
          ? "critical"
          : "insufficient_data"
    : "insufficient_data";

  if (loading && !intakeReport && !refreshStatus && !actionCounts && !inventoryAlerts) {
    return <div className="pilot-readiness-loading">Učitavam pilot readiness signale...</div>;
  }

  return (
    <div className="pilot-readiness-page">
      <AnalyticsTrustHeader
        title="Pilot Readiness Checklist"
        description="Jedan operativni ekran za 30-dnevni pilot: import, kvalitet podataka, refresh, dashboard, dobavljači, lager, izveštaji i akcije."
        mode="report"
        periodFrom={intakeReport?.periodFromUtc ?? null}
        periodTo={intakeReport?.periodToUtc ?? null}
        lastRefreshAt={refreshStatus?.lastSuccessfulRefreshAtUtc ?? intakeReport?.lastRefreshAtUtc ?? null}
        dataFreshnessStatus={refreshStatus?.dataFreshnessStatus ?? intakeReport?.dataFreshnessStatus ?? "unknown"}
        refreshIsRunning={refreshStatus?.isRunning}
        refreshCurrentStep={refreshStatus?.currentStep ?? null}
        dataSource="Pilot import + refresh status + action queue + report availability"
        dataQualityStatus={dataQualityStatus}
        dataQualitySummary={dataQualitySummary}
        dataQualityHref={ROUTES.dataQuality}
        refreshStatusHref={ROUTES.workers}
      />

      <AnalyticsRefreshStatusBanner status={refreshStatus} loading={loading && !refreshStatus} />

      <section className={`pilot-readiness-summary pilot-readiness-summary-${readiness.tone}`} aria-label="Status pilota">
        <div className="pilot-readiness-summary-head">
          <div>
            <p className="pilot-readiness-overline">Status pilota</p>
            <h2>{readiness.label}</h2>
          </div>
          <span className={`pilot-readiness-badge pilot-readiness-badge-${readiness.status}`}>{readiness.label}</span>
        </div>
        <p className="pilot-readiness-summary-text">{readiness.summary}</p>
        <div className="pilot-readiness-progress">
          <div>
            <span>Potvrđeni koraci</span>
            <strong>{readiness.readyCount}/{readiness.steps.length}</strong>
          </div>
          <progress value={readiness.readyCount} max={readiness.steps.length} aria-label="Pilot readiness progress" />
        </div>
        <div className="pilot-readiness-actions">
          <Link to={ROUTES.import} className="pilot-readiness-primary-link">Otvori import</Link>
          <Link to={ROUTES.dataQuality} className="pilot-readiness-secondary-link">Otvori Data Quality</Link>
          <Link to={ROUTES.dashboard} className="pilot-readiness-secondary-link">Otvori dashboard</Link>
        </div>
        <div className="pilot-readiness-lists">
          <div>
            <h3>Razlozi</h3>
            <ul>{readiness.reasons.map((reason) => <li key={reason}>{reason}</li>)}</ul>
          </div>
          <div>
            <h3>Sledeći koraci</h3>
            <ul>{readiness.nextActions.map((action) => <li key={action}>{action}</li>)}</ul>
          </div>
        </div>
      </section>

      <section className="pilot-readiness-snapshot" aria-label="Signal snapshot">
        <article className="pilot-readiness-snapshot-card">
          <h3>Učitani podaci</h3>
          <dl>
            <div><dt>Artikli</dt><dd>{count(intakeReport?.loadedData.articlesCount)}</dd></div>
            <div><dt>Stavke prodaje</dt><dd>{count(intakeReport?.loadedData.saleItemsCount)}</dd></div>
            <div><dt>Računi</dt><dd>{count(intakeReport?.loadedData.receiptsCount)}</dd></div>
            <div><dt>Dobavljači</dt><dd>{count(intakeReport?.loadedData.suppliersCount)}</dd></div>
          </dl>
        </article>
        <article className="pilot-readiness-snapshot-card">
          <h3>Report availability</h3>
          <div className="pilot-readiness-chip-row">
            <span className={`pilot-readiness-chip ${readiness.reports.pilotIntake === true ? "is-yes" : readiness.reports.pilotIntake === false ? "is-no" : "is-unknown"}`}>Pilot intake</span>
            <span className={`pilot-readiness-chip ${readiness.reports.supplierDecision === true ? "is-yes" : readiness.reports.supplierDecision === false ? "is-no" : "is-unknown"}`}>Supplier report</span>
          </div>
          <p>{readiness.reports.pilotIntake === true && readiness.reports.supplierDecision === true ? "Oba trajna reporta su dostupna." : "Dostupnost reportova je delimična ili nije potvrđena."}</p>
          <div className="pilot-readiness-compact-links">
            <Link to={ROUTES.pilotIntakeReport}>Pilot intake report</Link>
            <Link to={ROUTES.supplierReport}>Supplier report</Link>
          </div>
        </article>
        <article className="pilot-readiness-snapshot-card">
          <h3>Action queue</h3>
          <dl>
            <div><dt>Novo</dt><dd>{count(actionCounts?.new)}</dd></div>
            <div><dt>Prihvaćeno</dt><dd>{count(actionCounts?.accepted)}</dd></div>
            <div><dt>Odloženo</dt><dd>{count(actionCounts?.deferred)}</dd></div>
            <div><dt>Done</dt><dd>{count(actionCounts?.done)}</dd></div>
            <div><dt>P1 otvorene</dt><dd>{count(actionCounts?.p1Open)}</dd></div>
          </dl>
        </article>
      </section>

      <section className="pilot-readiness-checklist" aria-label="Pilot checklist">
        {readiness.steps.map((item) => (
          <article key={item.key} className={`pilot-readiness-step pilot-readiness-step-${tone(item.status)}`}>
            <div className="pilot-readiness-step-head">
              <div>
                <h3>{item.title}</h3>
                <p>{item.reason}</p>
              </div>
              <span className={`pilot-readiness-badge pilot-readiness-badge-${item.status}`}>{label(item.status)}</span>
            </div>
            <div className="pilot-readiness-step-actions">
              <Link to={item.actionHref} className="pilot-readiness-primary-link">{item.actionLabel}</Link>
              <div className="pilot-readiness-step-links">
                {item.links.map((link) => <Link key={link.href} to={link.href}>{link.label}</Link>)}
              </div>
            </div>
          </article>
        ))}
      </section>
    </div>
  );
}
