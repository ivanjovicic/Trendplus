import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReport from "../SupplierDecisionReport";

vi.mock("../KpiExplainButton", () => ({
  default: () => null,
}));

vi.mock("../MetricMethodologyPanel", () => ({
  default: () => <div data-testid="methodology-panel" />,
}));

const clipboardWriteTextMock = vi.fn(async (_text: string) => Promise.resolve());

Object.defineProperty(navigator, "clipboard", {
  value: { writeText: clipboardWriteTextMock },
  configurable: true,
});

function buildPayload() {
  return {
    tableKey: "supplier-decision-report",
    tableTitle: "Trendplus izveštaj dobavljača",
    columns: [
      { key: "section", header: "Sekcija", dataType: "text" },
      { key: "item", header: "Stavka", dataType: "text" },
      { key: "value", header: "Vrednost", dataType: "text" },
      { key: "secondary", header: "Grupa", dataType: "text" },
      { key: "note", header: "Napomena", dataType: "text" },
    ],
    rows: [
      { section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" },
      { section: "Header", item: "Dobavljač", value: "Alpha" },
      { section: "Header", item: "Period", value: "2026-04-01 - 2026-06-30" },
      { section: "KPI", item: "Prihod", value: "520000" },
      { section: "supplier_negotiation_pack", item: "Dobavljač", value: "Alpha", secondary: "Sažetak", note: "Kontekst" },
      { section: "supplier_negotiation_pack", item: "Prihod", value: "520000", secondary: "Sažetak" },
      { section: "supplier_negotiation_pack", item: "Maržni doprinos", value: "162000", secondary: "Sažetak" },
      { section: "supplier_negotiation_pack", item: "Prodate jedinice", value: "120", secondary: "Sažetak" },
      { section: "supplier_negotiation_pack", item: "Najbolji artikli po marži", value: "Model A", secondary: "Argumenti za pregovor" },
      { section: "supplier_negotiation_pack", item: "Finalni savet", value: "Pojačaj saradnju", secondary: "Predlog razgovora" },
      { section: "supplier_negotiation_pack", item: "Korišćen fallback dataset", value: "usedFallback=true", secondary: "Upozorenja", note: "fallback_90d" },
    ],
    filters: [],
    metadata: [
      { key: "dataQualityStatus", label: "Kvalitet podataka", value: "good" },
      { key: "lastRefreshAtUtc", label: "Poslednje osveženje", value: "2026-07-31T05:30:00Z" },
      { key: "requestedDataset", label: "Traženi dataset", value: "90d" },
      { key: "effectiveDataset", label: "Efektivni dataset", value: "90d" },
      { key: "effectivePeriodLabel", label: "Efektivni period", value: "Poslednjih 90 dana" },
      { key: "effectivePeriodFromUtc", label: "Efektivni period od", value: "2026-04-01T00:00:00Z" },
      { key: "effectivePeriodToUtc", label: "Efektivni period do", value: "2026-06-30T00:00:00Z" },
      { key: "observedPeriodFromUtc", label: "Posmatrani period od", value: "2026-01-10T00:00:00Z" },
      { key: "observedPeriodToUtc", label: "Posmatrani period do", value: "2026-06-29T00:00:00Z" },
      { key: "provenanceBasis", label: "Osnova generisanja", value: "mv_supplier_decision_score_cache_90d" },
      { key: "usedFallback", label: "Korišćen fallback", value: false },
      { key: "recommendationAllowed", label: "Preporuka dozvoljena", value: true },
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

describe("SupplierDecisionReport", () => {
  it("renders supplier negotiation pack section and warning rows", () => {
    render(<SupplierDecisionReport payload={buildPayload()} />);

    expect(screen.getByRole("heading", { name: "Paket za razgovor sa dobavljačem" })).toBeInTheDocument();
    expect(screen.getByText("Korišćen fallback dataset")).toBeInTheDocument();
    expect(screen.getByText("usedFallback=true")).toBeInTheDocument();
    expect(screen.getByText("Finalni savet")).toBeInTheDocument();
    expect(screen.getByText("mv_supplier_decision_score_cache_90d")).toBeInTheDocument();
  });

  it("copies meeting summary with requested button label", async () => {
    render(<SupplierDecisionReport payload={buildPayload()} />);

    const copyButton = screen.getByRole("button", { name: "Kopiraj sažetak za sastanak" });
    fireEvent.click(copyButton);

    await waitFor(() => {
      expect(clipboardWriteTextMock).toHaveBeenCalledTimes(1);
    });

    expect(clipboardWriteTextMock.mock.calls[0][0]).toContain("Paket za razgovor sa dobavljačem");
    expect(clipboardWriteTextMock.mock.calls[0][0]).toContain("Finalni savet");
  });

  it("shows helper signal when recommendationAllowed=false", () => {
    const payload = buildPayload();
    payload.rows = payload.rows.map((row) => row.section === "supplier_negotiation_pack" && row.item === "Finalni savet"
      ? { ...row, secondary: "Pomoćni signal", value: "Proveriti podatke i signal pre pregovora" }
      : row);
    payload.metadata = [{ key: "dataQualityStatus", label: "Kvalitet podataka", value: "critical" }];

    render(<SupplierDecisionReport payload={payload} />);

    expect(screen.getByText("Pomoćni signal")).toBeInTheDocument();
    expect(screen.getByText(/proveriti podatke i signal pre pregovora/i)).toBeInTheDocument();
  });

  it("shows report generation time separately from the last refresh time", () => {
    const payload = buildPayload();
    payload.metadata = [
      ...payload.metadata,
      { key: "generatedAtUtc", label: "Generisano", value: "2026-08-26T10:00:00Z" },
      { key: "dataFreshness", label: "Svežina podataka", value: "stale" },
    ];

    render(<SupplierDecisionReport payload={payload} />);

    expect(screen.getByText("Datum izveštaja")).toBeInTheDocument();
    expect(screen.getByText("2026-08-26T10:00:00Z")).toBeInTheDocument();
    expect(screen.getByText("Poslednje osveženje")).toBeInTheDocument();
    expect(screen.getByText("2026-07-31T05:30:00Z")).toBeInTheDocument();
    expect(screen.getByText("Svežina podataka: stale")).toBeInTheDocument();
    expect(screen.getByText("Efektivni i posmatrani period")).toBeInTheDocument();
    expect(screen.getByText(/Posmatrani podaci:/)).toBeInTheDocument();
  });

  it("shows localized data quality labels instead of raw backend codes", () => {
    const payload = buildPayload();
    payload.metadata = [
      { key: "dataQualityStatus", label: "Kvalitet podataka", value: "insufficient_data" },
    ];

    render(<SupplierDecisionReport payload={payload} />);

    expect(screen.getByText("Kvalitet podataka: Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.queryByText("Kvalitet podataka: insufficient_data")).not.toBeInTheDocument();
  });
});
