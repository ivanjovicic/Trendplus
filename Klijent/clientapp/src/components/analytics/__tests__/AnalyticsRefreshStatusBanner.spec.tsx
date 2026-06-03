import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsRefreshStatusBanner from "../AnalyticsRefreshStatusBanner";
import type { AnalyticsRefreshStatus } from "../../../types/analytics";

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

describe("AnalyticsRefreshStatusBanner", () => {
  it("shows unknown state when status is missing", () => {
    renderBanner(null);
    expect(screen.getByText("Status osvežavanja nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Otvori worker panel")).toBeInTheDocument();
  });

  it("shows fresh badge and last successful refresh", () => {
    renderBanner(buildStatus({ dataFreshnessStatus: "fresh" }));
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText(/Poslednji uspešan refresh:/)).toBeInTheDocument();
  });

  it("shows stale badge", () => {
    renderBanner(buildStatus({ dataFreshnessStatus: "stale" }));
    expect(screen.getByText("Zastarelo")).toBeInTheDocument();
  });

  it("shows critical badge with error", () => {
    renderBanner(
      buildStatus({
        dataFreshnessStatus: "critical",
        lastErrorMessage: "supplier_decision_mv failed",
        failedObjects: ["supplier_decision_mv"],
      })
    );

    expect(screen.getByText("Kritično")).toBeInTheDocument();
    expect(screen.getByText("Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.")).toBeInTheDocument();
    expect(screen.getByText(/supplier_decision_mv failed/i)).toBeInTheDocument();
    expect(screen.getByText(/Neuspešni objekti:/)).toBeInTheDocument();
  });

  it("shows running message with current step", () => {
    renderBanner(
      buildStatus({
        isRunning: true,
        currentStep: "product_dim_refresh",
      })
    );

    expect(screen.getByText(/Osvežavanje u toku \(product_dim_refresh\)/)).toBeInTheDocument();
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
});


