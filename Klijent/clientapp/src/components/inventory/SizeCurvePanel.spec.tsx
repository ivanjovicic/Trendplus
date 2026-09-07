import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { SizeCurveDto } from "../../types/analytics";
import { SizeCurvePanel } from "./SizeCurvePanel";

vi.mock("./SizeCurveVisualization", () => ({
  SizeCurveVisualization: ({ items }: { items: unknown[] }) => (
    <div data-testid="size-curve-visualization">{items.length} size curve redova</div>
  ),
}));

const sizeCurveItem = {
  skuId: 101,
  storeId: 7,
  sizeCode: "42",
  actualSizeShare: 0.4,
  idealSizeShare: 0.35,
  deviationPct: 0.05,
  isCoreSizeMissing: false,
  isDeadSize: false,
  brokenRun: false,
  curveConfidence: 0.9,
  evidenceStatus: "complete" as const,
  reasonCodes: [],
};

function renderPanel(sizeCurve: SizeCurveDto | null) {
  return render(
    <SizeCurvePanel
      sizeCurveSkuId={101}
      sizeCurve={sizeCurve}
      sizeCurveLoading={false}
      sizeCurveError={null}
      onChangeSkuId={vi.fn()}
    />,
  );
}

describe("SizeCurvePanel evidence states", () => {
  it("keeps a missing snapshot relation distinct from a valid empty result", () => {
    renderPanel({
      generatedAtUtc: "2026-08-10T10:00:00Z",
      totalCount: 0,
      snapshotAvailable: false,
      warning: "size_curve_snapshot_missing_relation",
      items: [],
    });

    expect(screen.getByText("Size curve nije dostupna za SKU #101.")).toBeInTheDocument();
    expect(screen.getByText("Size curve snapshot trenutno nije dostupan.")).toBeInTheDocument();
    expect(screen.queryByText(/size_curve_snapshot_missing_relation/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Nema size curve podataka/i)).not.toBeInTheDocument();
  });

  it("keeps a successful empty snapshot as an empty state", () => {
    renderPanel({
      generatedAtUtc: "2026-08-10T10:00:00Z",
      totalCount: 0,
      snapshotAvailable: true,
      warning: "Size curve snapshot postoji, ali nema redova za trazene filtere.",
      items: [],
    });

    expect(screen.getByText("Size curve snapshot je dostupan, ali nema podataka za SKU #101 u izabranom opsegu.")).toBeInTheDocument();
    expect(screen.getByText("Size curve snapshot nema redove za izabrani opseg.")).toBeInTheDocument();
    expect(screen.queryByText(/nije dostupna/i)).not.toBeInTheDocument();
  });

  it("renders partial warning next to the populated size curve without exposing backend wording", () => {
    renderPanel({
      generatedAtUtc: "2026-08-10T10:00:00Z",
      totalCount: 1,
      snapshotAvailable: true,
      warning: "Size curve snapshot sadrzi redove sa nepotpunom signalnom evidencijom.",
      items: [sizeCurveItem],
    });

    expect(screen.getByText("Size curve snapshot sadrži delimične ili nepotpune podatke.")).toBeInTheDocument();
    expect(screen.getByTestId("size-curve-visualization")).toHaveTextContent("1 size curve redova");
    expect(screen.queryByText(/sadrzi redove sa nepotpunom signalnom evidencijom/i)).not.toBeInTheDocument();
  });

  it("renders populated complete data without an artificial warning", () => {
    renderPanel({
      generatedAtUtc: "2026-08-10T10:00:00Z",
      totalCount: 1,
      snapshotAvailable: true,
      warning: null,
      items: [sizeCurveItem],
    });

    expect(screen.getByTestId("size-curve-visualization")).toHaveTextContent("1 size curve redova");
    expect(screen.queryByText(/delimične ili nepotpune/i)).not.toBeInTheDocument();
  });
});
