import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionReportPage from "../SupplierDecisionReportPage";

const getPrintPayloadMock = vi.fn();

vi.mock("../../services/analyticsTableState", () => ({
  getPrintPayload: (...args: unknown[]) => getPrintPayloadMock(...args),
}));

vi.mock("../../components/analytics/SupplierDecisionReport", () => ({
  default: () => <div>report-body</div>,
}));

vi.mock("../../components/analytics/SupplierDecisionReportActions", () => ({
  default: ({ onError }: { onError?: (message: string) => void }) => (
    <button type="button" onClick={() => onError?.("PDF export trenutno nije dostupan. Koristite Print izvestaj ili Export Excel.")}>
      trigger-export-error
    </button>
  ),
}));

function renderPage(path = "/analytics/supplier/report?stateKey=test") {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/analytics/supplier/report" element={<SupplierDecisionReportPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("SupplierDecisionReportPage", () => {
  it("renders filtered_out empty state with recovery actions when payload is missing", () => {
    getPrintPayloadMock.mockReturnValueOnce(null);

    renderPage();

    expect(screen.getByText("Report nije pronadjen")).toBeInTheDocument();
    expect(screen.getByText(/Privremeni podaci su istekli/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vrati se na dobavljace" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ponovo generisi report" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Otvori scorecard" })).toBeInTheDocument();
  });

  it("shows export error state when actions report PDF failure", () => {
    getPrintPayloadMock.mockReturnValueOnce({
      tableKey: "supplier-decision-report",
      tableTitle: "Trendplus izvestaj dobavljaca",
      columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
      rows: [{ section: "Header", item: "Naziv izvestaja", value: "Trendplus izvestaj dobavljaca" }],
      filters: [],
      metadata: [],
      locale: "sr-RS",
    });

    renderPage();

    fireEvent.click(screen.getByRole("button", { name: "trigger-export-error" }));

    expect(screen.getByText("Izvoz izvestaja nije uspeo")).toBeInTheDocument();
    expect(screen.getByText(/PDF export trenutno nije dostupan/i)).toBeInTheDocument();
  });
});
