import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReport from "../SupplierDecisionReport";

vi.mock("../KpiExplainButton", () => ({
  default: () => null,
}));

vi.mock("../MetricMethodologyPanel", () => ({
  default: () => <div data-testid="methodology-panel" />,
}));

function buildPayload() {
  return {
    tableKey: "supplier-decision-report",
    tableTitle: "Trendplus izveštaj dobavljača",
    columns: [],
    rows: [
      { section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" },
      { section: "Header", item: "Dobavljač", value: "Alpha" },
      { section: "Header", item: "Period", value: "2026-04-01 - 2026-06-30" },
      { section: "Header", item: "Efektivni dataset", value: "90d", secondary: "Poslednjih 90 dana" },
      { section: "KPI", item: "Prihod", value: "520000" },
      { section: "supplier_negotiation_pack", item: "Finalni savet", value: "Pojačaj saradnju", secondary: "Predlog razgovora" },
    ],
    filters: [],
    metadata: [
      { key: "dataQualityStatus", label: "Kvalitet podataka", value: "good" },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: "2026-07-31T05:30:00Z" },
      { key: "requestedDataset", label: "Traženi dataset", value: "30d" },
      { key: "effectiveDataset", label: "Efektivni dataset", value: "90d" },
      { key: "effectivePeriodLabel", label: "Efektivni period", value: "Poslednjih 90 dana" },
      { key: "provenanceBasis", label: "Osnova generisanja", value: "mv_supplier_decision_score_cache_90d" },
      { key: "usedFallback", label: "Korišćen fallback", value: true },
      { key: "fallbackReason", label: "Razlog fallback-a", value: "Nedovoljno transakcija u 30d opsegu" },
      { key: "fallbackReasonCode", label: "Kod fallback-a", value: "fallback_90d" },
      { key: "recommendationAllowed", label: "Preporuka dozvoljena", value: false },
      { key: "confidencePct", label: "Sigurnost signala", value: 83 },
      { key: "reliabilityPct", label: "Pouzdanost signala", value: 79 },
      { key: "reasonCodesPreview", label: "Šifarnici razloga", value: "high_share | stable_margin" },
    ],
    methodologyMetricKeys: ["revenue"],
    locale: "sr-RS",
    documentType: "supplier-decision-report",
    templateName: "supplier-decision",
    templateVersion: 2,
  };
}

describe("SupplierExplainabilitySnapshot", () => {
  it("renders the supplier explainability snapshot inside the report", () => {
    render(<SupplierDecisionReport payload={buildPayload()} />);

    const snapshot = screen.getByTestId("supplier-explainability-snapshot");

    expect(snapshot).toBeInTheDocument();
    expect(within(snapshot).getByText("Supplier explainability snapshot")).toBeInTheDocument();
    expect(within(snapshot).getByText(/Alpha/)).toBeInTheDocument();
    expect(within(snapshot).getByText("Poslednjih 90 dana")).toBeInTheDocument();
    expect(within(snapshot).getByText("mv_supplier_decision_score_cache_90d")).toBeInTheDocument();
  });
});
