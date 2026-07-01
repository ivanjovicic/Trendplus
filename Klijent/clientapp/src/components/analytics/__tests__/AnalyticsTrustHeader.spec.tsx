import { render, screen, within } from "@testing-library/react";
import type { ComponentProps } from "react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsTrustHeader from "../AnalyticsTrustHeader";

function renderHeader(overrides: Partial<ComponentProps<typeof AnalyticsTrustHeader>> = {}) {
  return render(
    <MemoryRouter>
      <AnalyticsTrustHeader
        title="Izvršni pregled"
        description="Decision-support pregled sa statusom kvaliteta podataka."
        periodFrom="2026-06-01T00:00:00Z"
        periodTo="2026-06-30T23:59:59Z"
        lastRefreshAt="2026-07-01T08:15:00Z"
        dataFreshnessStatus="fresh"
        dataSource="analytics_daily_summary"
        dataQualityStatus="good"
        mode="recommendation"
        dataQualitySummary={{
          missingSupplierCount: 2,
          missingCostCount: 5,
          missingCategoryCount: 1,
          insufficientSignalCount: 3,
          ignoredRowsCount: 4,
        }}
        methodologyHref="/docs/methodology"
        {...overrides}
      />
    </MemoryRouter>,
  );
}

describe("AnalyticsTrustHeader", () => {
  it("renders decision context, freshness, data quality summary and support links", () => {
    renderHeader();

    expect(screen.getByRole("heading", { name: "Izvršni pregled" })).toBeInTheDocument();
    expect(screen.getByText("Preporuka sistema")).toBeInTheDocument();
    expect(screen.getByText("Podaci deluju pouzdano")).toBeInTheDocument();
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText("analytics_daily_summary")).toBeInTheDocument();

    const summary = screen.getByText("Sažetak kvaliteta podataka").closest(".ath-summary");
    expect(summary).not.toBeNull();
    expect(within(summary as HTMLElement).getByText("Artikli bez dobavljača")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("Redovi bez nabavne cene")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("Ignorisani redovi")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("5")).toBeInTheDocument();

    expect(screen.getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute("href", "/analytics/data-quality");
    expect(screen.getByRole("link", { name: "Status osvežavanja" })).toHaveAttribute("href", "/admin/configuration?panel=workers");
    expect(screen.getByRole("link", { name: "Metodologija i tumačenje signala" })).toHaveAttribute("href", "/docs/methodology");
  });

  it("shows running subtitle while refresh is active", () => {
    renderHeader({ refreshIsRunning: true, refreshCurrentStep: "supplier_decision_mvs" });

    expect(screen.getByText(/Osvežavanje je u toku \(supplier_decision_mvs\)/)).toBeInTheDocument();
  });

  it("prioritizes fallback messaging over gated messaging and keeps dataset lineage visible", () => {
    renderHeader({
      usedFallback: true,
      recommendationAllowed: false,
      requestedDataset: "requested_window",
      effectiveDataset: "all_time",
      effectivePeriodLabel: "All-time fallback",
      fallbackReason: "Nema dovoljno redova u traženom periodu.",
      fallbackReasonCode: "NO_WINDOW_ROWS",
      dataQualityStatus: "warning",
      dataFreshnessStatus: "stale",
    });

    expect(screen.getByText("Postoje upozorenja")).toBeInTheDocument();
    expect(screen.getByText("Zastarelo")).toBeInTheDocument();
    expect(screen.getByText("requested_window -> all_time")).toBeInTheDocument();
    expect(screen.getByText("All-time fallback")).toBeInTheDocument();
    expect(screen.getByText(/Fallback aktiviran\./i)).toBeInTheDocument();
    expect(screen.getByText(/NO_WINDOW_ROWS/i)).toBeInTheDocument();
    expect(screen.queryByText(/Preporuka je gated/i)).not.toBeInTheDocument();
    expect(screen.getByText(/Prikaz može biti delimičan ili zastareo/i)).toBeInTheDocument();
  });

  it("renders gated and missing-summary states without inventing quality counts", () => {
    renderHeader({
      recommendationAllowed: false,
      dataQualityStatus: "insufficient_data",
      dataQualitySummary: undefined,
      requestedDataset: null,
      effectiveDataset: null,
      mode: "signal",
      recommendationNote: "Ne prikazuj konačnu preporuku bez jačeg signala.",
      emptyStateReason: "Nema dovoljno podataka za izabrani period.",
    });

    expect(screen.getByText("Analitički signal")).toBeInTheDocument();
    expect(screen.getByText("Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.getByText(/Preporuka je gated/i)).toBeInTheDocument();
    expect(screen.getByText("Detaljan kvalitet podataka nije dostupan za ovaj ekran.")).toBeInTheDocument();
    expect(screen.getByText("Ne prikazuj konačnu preporuku bez jačeg signala.")).toBeInTheDocument();
    expect(screen.getByText("Nema dovoljno podataka za izabrani period.")).toBeInTheDocument();
    expect(screen.queryByText("Dataset")).not.toBeInTheDocument();
  });
});
