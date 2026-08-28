import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import SupplierDecisionReportPage from "../SupplierDecisionReportPage";

const resolveAnalyticsTablePayloadMock = vi.fn((payload) => payload);
const getSupplierDecisionDurableReportMock = vi.fn();

vi.mock("../../services/analyticsTableState", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsTableState")>("../../services/analyticsTableState");
  return {
    ...actual,
    resolveAnalyticsTablePayload: (...args: unknown[]) => resolveAnalyticsTablePayloadMock(...args),
  };
});

vi.mock("../../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../../services/analyticsApi")>("../../services/analyticsApi");
  return {
    ...actual,
    getSupplierDecisionDurableReport: (...args: unknown[]) => getSupplierDecisionDurableReportMock(...args),
  };
});

vi.mock("../../components/analytics/SupplierDecisionReport", () => ({
  default: () => <div>report-body</div>,
}));

vi.mock("../../components/analytics/SupplierDecisionReportActions", () => ({
  default: ({ onError }: { onError?: (message: string) => void }) => (
    <button type="button" onClick={() => onError?.("PDF export trenutno nije dostupan. Koristite Print izveštaj ili Export Excel.")}>
      trigger-export-error
    </button>
  ),
}));

function legacySnapshot(overrides: Record<string, unknown> = {}) {
  return {
    payload: {
      tableKey: "supplier-decision-report",
      tableTitle: "Trendplus izveštaj dobavljača",
      columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
      rows: [{ section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" }],
      filters: [],
      metadata: [],
      locale: "sr-RS",
    },
    savedAtUtc: "2026-08-05T08:00:00.000Z",
    expiresAtUtc: "2026-08-05T08:10:00.000Z",
    ttlMs: 10 * 60 * 1000,
    ageMs: 60_000,
    ...overrides,
  };
}

function durableResponse() {
  return {
    payload: {
      tableKey: "supplier-decision-report",
      tableTitle: "Trendplus izveštaj dobavljača",
      documentType: "supplier-decision-report",
      templateName: "supplier-decision",
      locale: "sr-RS",
      columns: [{ key: "section", header: "Sekcija", dataType: "text" }],
      rows: [{ section: "Header", item: "Naziv izveštaja", value: "Trendplus izveštaj dobavljača" }],
      filters: [],
      metadata: [],
    },
  };
}

function storeBrowserPreview(key: string) {
  const snapshot = legacySnapshot();
  localStorage.setItem(key, JSON.stringify({
    savedAtUtc: new Date().toISOString(),
    payload: snapshot.payload,
  }));
}

function renderPage(path = "/analytics/supplier/report") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/analytics/supplier/report" element={<SupplierDecisionReportPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe("SupplierDecisionReportPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("forwards section query to durable endpoint and injects methodology keys", async () => {
    getSupplierDecisionDurableReportMock.mockResolvedValueOnce(durableResponse());

    renderPage("/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30&section=supplier_negotiation_pack");

    await screen.findByText("report-body");

    expect(getSupplierDecisionDurableReportMock).toHaveBeenCalledWith(
      expect.objectContaining({
        fromDate: "2026-04-01",
        toDate: "2026-06-30",
        section: "supplier_negotiation_pack",
      })
    );

    expect(resolveAnalyticsTablePayloadMock).toHaveBeenCalledWith(
      expect.objectContaining({
        methodologyMetricKeys: [
          "revenue",
          "marginContribution",
          "markdownDependency",
          "stockAtRisk",
          "confidencePct",
          "reliabilityPct",
          "sellThrough",
        ],
      })
    );
  });

  it("shows expired state only for a preview-only URL whose browser snapshot is missing", () => {
    renderPage("/analytics/supplier/report?preview=browser&stateKey=missing");

    expect(screen.getByRole("heading", { name: "Pregled izveštaja je istekao" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vrati se na dobavljače" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ponovo generiši report" })).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Otvori Scorecard" }).length).toBeGreaterThan(0);
  });

  it("uses browser state only for an explicitly requested preview", async () => {
    storeBrowserPreview("test");

    renderPage("/analytics/supplier/report?preview=browser&stateKey=test");

    expect(await screen.findByTestId("local-preview-banner")).toBeInTheDocument();
    expect(screen.getByText(/LOKALNI BROWSER PREVIEW/i)).toBeInTheDocument();
    expect(screen.getByTestId("local-preview-meta")).toHaveTextContent(/TTL/i);
    expect(screen.getByTestId("local-preview-export-disabled")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "trigger-export-error" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Štampaj iz pregleda" })).not.toBeInTheDocument();
    expect(screen.getByText("report-body")).toBeInTheDocument();
    expect(getSupplierDecisionDurableReportMock).not.toHaveBeenCalled();
  });

  it("keeps stateKey-only links as backwards-compatible browser previews", () => {
    storeBrowserPreview("legacy-preview");

    renderPage("/analytics/supplier/report?stateKey=legacy-preview");

    expect(screen.getByTestId("local-preview-banner")).toBeInTheDocument();
    expect(screen.getByTestId("local-preview-export-disabled")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Štampaj iz pregleda" })).not.toBeInTheDocument();
  });

  it("reloads a durable URL from the backend after remount and ignores a stale stateKey", async () => {
    getSupplierDecisionDurableReportMock.mockResolvedValue(durableResponse());
    const durableUrl = "/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30&stateKey=stale";

    const first = renderPage(durableUrl);
    expect(await screen.findByText("report-body")).toBeInTheDocument();
    first.unmount();

    renderPage(durableUrl);
    expect(await screen.findByText("report-body")).toBeInTheDocument();

    expect(getSupplierDecisionDurableReportMock).toHaveBeenCalledTimes(2);
    expect(screen.queryByRole("heading", { name: "Pregled izveštaja je istekao" })).not.toBeInTheDocument();
  });

  it("shows export error state when actions report PDF failure on durable payload", async () => {
    getSupplierDecisionDurableReportMock.mockResolvedValueOnce(durableResponse());

    renderPage("/analytics/supplier/report?fromDate=2026-04-01&toDate=2026-06-30");

    await screen.findByText("report-body");
    fireEvent.click(screen.getByRole("button", { name: "trigger-export-error" }));

    expect(screen.getByText("Izvoz izveštaja nije uspeo")).toBeInTheDocument();
    expect(screen.getByText(/PDF export trenutno nije dostupan/i)).toBeInTheDocument();
  });
});
