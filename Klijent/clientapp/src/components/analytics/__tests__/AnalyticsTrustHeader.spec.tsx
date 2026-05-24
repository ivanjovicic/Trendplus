import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import AnalyticsTrustHeader from "../AnalyticsTrustHeader";

describe("AnalyticsTrustHeader", () => {
  it("shows last refresh and freshness badge", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="report"
        periodFrom="2026-05-01T00:00:00Z"
        periodTo="2026-05-22T23:59:59Z"
        lastRefreshAt="2026-05-22T07:30:00Z"
        dataFreshnessStatus="fresh"
      />
    );

    expect(screen.getByText("Sveze")).toBeInTheDocument();
    expect(screen.getByText("Poslednje osvezenje")).toBeInTheDocument();
  });

  it("shows running subtitle when refresh is active", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="report"
        refreshIsRunning
        refreshCurrentStep="supplier_decision_mvs"
      />
    );

    expect(screen.getByText(/Osvezavanje je u toku \(supplier_decision_mvs\)/)).toBeInTheDocument();
  });

  it("shows unknown freshness badge", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="report"
        dataFreshnessStatus="unknown"
      />
    );

    expect(screen.getByText("Nije poznato")).toBeInTheDocument();
  });

  it("shows missing summary message when summary object has no values", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="report"
        dataQualitySummary={{}}
      />
    );

    expect(screen.getByText("Detaljan kvalitet podataka nije dostupan za ovaj ekran.")).toBeInTheDocument();
  });

  it("shows dataset and fallback banner when usedFallback is true", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="recommendation"
        requestedDataset="30d"
        effectiveDataset="90d"
        effectivePeriodLabel="Poslednjih 90 dana"
        usedFallback
        fallbackReason="Trazeni 30d nema zaseban scorecard dataset."
        fallbackReasonCode="no_mv_30d"
      />
    );

    expect(screen.getByText("Dataset")).toBeInTheDocument();
    expect(screen.getByText(/30d -> 90d/)).toBeInTheDocument();
    expect(screen.getByText(/Fallback aktiviran\./)).toBeInTheDocument();
    expect(screen.getByText(/\(no_mv_30d\)/)).toBeInTheDocument();
  });

  it("shows default footer links and highlights insufficient data", () => {
    render(
      <AnalyticsTrustHeader
        title="Kontekst"
        description="Opis"
        mode="recommendation"
        dataQualityStatus="insufficient_data"
        compact
      />
    );

    expect(screen.getByText("Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute("href", "/analytics/data-quality");
    expect(screen.getByRole("link", { name: "Worker status" })).toHaveAttribute("href", "/admin/configuration?panel=workers");
  });
});
