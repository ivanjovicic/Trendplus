import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsPrintPage from "../AnalyticsPrintPage";
import {
  resolveAnalyticsTablePayload,
  saveBrowserPreviewPayload,
} from "../../services/analyticsTableState";
import { formatDate, formatDateTime, fmtPct, fmtRsd } from "../../utils/analyticsFormatters";

describe("AnalyticsPrintPage", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(window, "print").mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("uses the shared column contract for finite, null and formatted print cells", () => {
    const payload = resolveAnalyticsTablePayload({
      tableKey: "print-fixture",
      tableTitle: "Print fixture",
      columns: [
        { key: "revenue", header: "Prihod", dataType: "currency" },
        { key: "sharePct", header: "Udeo", dataType: "percent" },
        { key: "units", header: "Kom", dataType: "number" },
        { key: "asOf", header: "Datum", dataType: "date" },
        { key: "refreshedAt", header: "Osveženo", dataType: "datetime" },
        { key: "active", header: "Aktivan", dataType: "text" },
        { key: "missing", header: "Nedostaje", dataType: "number" },
        { key: "poisoned", header: "Neispravno", dataType: "number" },
      ],
      rows: [
        {
          revenue: 120000,
          sharePct: 35,
          units: 0,
          asOf: "2026-03-18",
          refreshedAt: "2026-03-18T10:15:00Z",
          active: true,
          missing: null,
          poisoned: Number.POSITIVE_INFINITY,
        },
      ],
      filters: [{ key: "period", label: "Period", value: "30d" }],
      metadata: [{ key: "generatedAt", label: "Generisano", value: "2026-03-18" }],
    });
    const stateKey = saveBrowserPreviewPayload(payload);

    render(
      <MemoryRouter initialEntries={[`/print/analytics/print-fixture?stateKey=${stateKey}`]}>
        <Routes>
          <Route path="/print/analytics/:table" element={<AnalyticsPrintPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText(fmtRsd(120000, 0))).toBeInTheDocument();
    expect(screen.getByText(fmtPct(35, 2))).toBeInTheDocument();
    expect(screen.getByText("0")).toBeInTheDocument();
    expect(screen.getByText(formatDate("2026-03-18"))).toBeInTheDocument();
    expect(screen.getByText(formatDateTime("2026-03-18T10:15:00Z"))).toBeInTheDocument();
    expect(screen.getByText("Da")).toBeInTheDocument();
    expect(screen.getAllByText("-")).toHaveLength(2);
    expect(screen.queryByText(/NaN|Infinity/)).not.toBeInTheDocument();

    vi.advanceTimersByTime(150);
    expect(window.print).toHaveBeenCalledTimes(1);
  });
});
