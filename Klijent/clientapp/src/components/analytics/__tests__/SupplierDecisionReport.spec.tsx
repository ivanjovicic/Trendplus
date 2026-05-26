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
      { section: "Paket za razgovor sa dobavljačem", item: "Dobavljač", value: "Alpha", secondary: "Sažetak", note: "Kontekst" },
      { section: "Paket za razgovor sa dobavljačem", item: "Najbolji artikli po marži", value: "Model A", secondary: "Argumenti za dobavljača" },
      { section: "Paket za razgovor sa dobavljačem", item: "Finalni savet", value: "Pojačaj saradnju", secondary: "Predlog razgovora" },
      { section: "Paket za razgovor sa dobavljačem", item: "Korišćen fallback dataset", value: "usedFallback=true", secondary: "Upozorenja", note: "fallback_90d" },
    ],
    filters: [],
    metadata: [
      { key: "dataQualityStatus", label: "Kvalitet podataka", value: "good" },
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
});
