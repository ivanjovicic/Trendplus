import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsRefreshStatusBanner from "../AnalyticsRefreshStatusBanner";
import type { AnalyticsRefreshRun, AnalyticsRefreshStatus } from "../../../types/analytics";

function renderBanner(status: AnalyticsRefreshStatus | null) {
  return render(
    <MemoryRouter>
      <AnalyticsRefreshStatusBanner status={status} />
    </MemoryRouter>
  );
}

function buildStatus(overrides: Partial<AnalyticsRefreshStatus>): AnalyticsRefreshStatus {
  return {
    lastSuccessfulRefreshAtUtc: "2026-05-22T07:30:00Z",
    lastSuccessfulImportAtUtc: "2026-05-22T07:15:00Z",
    observedSalesPeriodFromUtc: "2026-05-01T00:00:00Z",
    observedSalesPeriodToUtc: "2026-05-21T00:00:00Z",
    lastAttemptAtUtc: "2026-05-22T08:00:00Z",
    lastFailureAtUtc: null,
    isRunning: false,
    lastErrorMessage: null,
    currentStep: null,
    refreshedObjects: ["sales_facts_mv"],
    failedObjects: [],
    durationSeconds: 120,
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "worker",
    workersEnabled: true,
    workerWarning: null,
    workerProcessWarning: null,
    generatedAtUtc: "2026-05-22T08:01:00Z",
    jobs: [],
    ...overrides,
  };
}

function buildRecentRun(overrides: Partial<AnalyticsRefreshRun> = {}): AnalyticsRefreshRun {
  return {
    id: 77,
    jobKey: "analytics_refresh",
    jobName: "Analytics refresh",
    status: "failed",
    startedAtUtc: "2026-05-22T07:59:00Z",
    finishedAtUtc: "2026-05-22T08:00:00Z",
    durationSeconds: 60,
    refreshedObjects: [],
    failedObjects: ["supplier_decision_mv"],
    errorCode: "analytics_refresh_failed",
    errorMessage: "Refresh failed",
    correlationId: "corr-123",
    triggeredBy: "system",
    processMode: "worker",
    workerName: "analytics-refresh-worker",
    createdAtUtc: "2026-05-22T08:00:00Z",
    ...overrides,
  };
}

describe("AnalyticsRefreshStatusBanner", () => {
  it("shows unknown state when status is missing", () => {
    renderBanner(null);
    expect(screen.getByText("Status osvežavanja nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Otvori status osvežavanja")).toBeInTheDocument();
  });

  it("shows fresh badge, last successful import, and observed sales horizon", () => {
    renderBanner(buildStatus({ dataFreshnessStatus: "fresh" }));
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText(/Poslednji uspešan import:/)).toBeInTheDocument();
    expect(screen.getByText(/Posmatrani promet:/)).toBeInTheDocument();
  });

  it("shows stale badge", () => {
    renderBanner(buildStatus({ dataFreshnessStatus: "stale" }));
    expect(screen.getByText("Zastarelo")).toBeInTheDocument();
  });

  it("shows critical badge with error and correlation ID", () => {
    renderBanner(
      buildStatus({
        dataFreshnessStatus: "critical",
        lastErrorMessage: "supplier_decision_mv failed",
        failedObjects: ["supplier_decision_mv"],
        recentRuns: [buildRecentRun()],
      })
    );

    expect(screen.getByText("Kritično")).toBeInTheDocument();
    expect(screen.getByText("Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.")).toBeInTheDocument();
    expect(screen.getByText("Osvežavanje nije uspešno završeno.")).toBeInTheDocument();
    expect(screen.queryByText(/supplier_decision_mv failed/i)).not.toBeInTheDocument();
    expect(screen.getByText("Correlation ID:")).toBeInTheDocument();
    expect(screen.getByText("corr-123")).toBeInTheDocument();
    expect(screen.getByText("Neuspešni objekti:")).toBeInTheDocument();
  });

  it("shows running message with current step", () => {
    renderBanner(
      buildStatus({
        isRunning: true,
        currentStep: "product_dim_refresh",
      })
    );

    expect(screen.getByText(/Osvežavanje u toku \(osvežavanje proizvoda\)/)).toBeInTheDocument();
    expect(screen.queryByText("product_dim_refresh")).not.toBeInTheDocument();
  });

  it("shows worker warning", () => {
    renderBanner(
      buildStatus({
        processMode: "web",
        processType: "web",
        workersEnabled: true,
        workerWarning: "Worker nije aktivan u ovom procesu",
      })
    );

    expect(screen.getByText(/Worker nije aktivan u ovom procesu/i)).toBeInTheDocument();
  });

  it("does not show zero duration when no refresh attempt has been recorded", () => {
    renderBanner(buildStatus({
      lastSuccessfulRefreshAtUtc: null,
      lastAttemptAtUtc: null,
      durationSeconds: 0,
      generatedAtUtc: null,
      dataFreshnessStatus: "unknown",
    }));

    expect(screen.getByText("Nema pokušaja u istoriji")).toBeInTheDocument();
    expect(screen.queryByText("0 s")).not.toBeInTheDocument();
  });

  it("keeps measured zero duration and hides invalid duration values", () => {
    const { rerender } = render(
      <MemoryRouter>
        <AnalyticsRefreshStatusBanner status={buildStatus({ durationSeconds: 0 })} />
      </MemoryRouter>,
    );

    expect(screen.getByText("0 s")).toBeInTheDocument();

    for (const invalidDuration of [-1, Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY]) {
      rerender(
        <MemoryRouter>
          <AnalyticsRefreshStatusBanner status={buildStatus({ durationSeconds: invalidDuration })} />
        </MemoryRouter>,
      );

      expect(screen.queryByText(/Infinity|NaN|-Infinity/)).not.toBeInTheDocument();
      expect(screen.queryByText(/Trajanje:/)).not.toBeInTheDocument();
    }
  });

  it("handles partial status payloads and hides raw operational tokens", () => {
    renderBanner(buildStatus({
      processMode: "internal_process",
      processType: "internal_process",
      isRunning: true,
      currentStep: "secret_step",
      lastErrorMessage: "sql_timeout at internal_table",
      refreshedObjects: null as never,
      failedObjects: null as never,
      jobs: [null] as never,
      recentRuns: undefined,
      durationSeconds: Number.NaN,
    }));

    expect(screen.getByText("Nepoznat proces")).toBeInTheDocument();
    expect(screen.getByText("Nepoznato")).toBeInTheDocument();
    expect(screen.getByText(/Obrada podataka/)).toBeInTheDocument();
    expect(screen.getByText("Osvežavanje nije uspešno završeno.")).toBeInTheDocument();
    expect(screen.queryByText(/internal_process|secret_step|sql_timeout|internal_table|internal_object|secret_object|NaN/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Trajanje:/)).not.toBeInTheDocument();
  });

  it("normalizes freshness tokens without upgrading unsupported values", () => {
    const { rerender } = render(
      <MemoryRouter>
        <AnalyticsRefreshStatusBanner status={buildStatus({ dataFreshnessStatus: " STALE " })} />
      </MemoryRouter>,
    );

    expect(screen.getByText("Zastarelo")).toBeInTheDocument();

    rerender(
      <MemoryRouter>
        <AnalyticsRefreshStatusBanner status={buildStatus({ dataFreshnessStatus: " unsupported_status " })} />
      </MemoryRouter>,
    );

    expect(screen.getByText("Nepoznato")).toBeInTheDocument();
  });

  it("renders a quiet single-line strip only when everything is fresh and healthy", () => {
    const { container } = renderBanner(buildStatus({ dataFreshnessStatus: "fresh" }));
    const banner = container.querySelector("section");
    expect(banner).toHaveAttribute("data-quiet", "true");
    expect(banner).toHaveClass("analytics-refresh-banner--quiet");
    // Quiet mode only changes density; the same facts stay readable.
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText(/Poslednji uspešan import:/)).toBeInTheDocument();
  });

  it.each([
    ["stale data", { dataFreshnessStatus: "stale" }],
    ["a running refresh", { isRunning: true, currentStep: "sales_facts_mv" }],
    ["a last error", { dataFreshnessStatus: "critical", lastErrorMessage: "Refresh failed" }],
    ["failed objects", { failedObjects: ["supplier_decision_mv"] }],
    ["a worker warning", { workerWarning: "Worker nije aktivan" }],
  ] as const)("keeps the full banner for %s", (_label, overrides) => {
    const { container } = renderBanner(buildStatus(overrides as Partial<AnalyticsRefreshStatus>));
    const banner = container.querySelector("section");
    expect(banner).not.toHaveAttribute("data-quiet");
    expect(banner).not.toHaveClass("analytics-refresh-banner--quiet");
  });
});
