import type { ReactNode } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SizeCurveVisualization } from "./SizeCurveVisualization";

vi.mock("recharts", () => ({
  Bar: () => null,
  CartesianGrid: () => null,
  Cell: () => null,
  ComposedChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Legend: () => null,
  Line: () => null,
  ReferenceLine: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

describe("SizeCurveVisualization accessibility", () => {
  it("names and summarizes the displayed size projection", () => {
    render(
      <SizeCurveVisualization
        items={[{
          sizeCode: "38",
          actualSizeShare: 0.2,
          idealSizeShare: 0.25,
          deviationPct: -0.05,
          isDeadSize: false,
          isCoreSizeMissing: false,
          evidenceStatus: "complete",
          brokenRun: false,
          curveConfidence: 0.9,
        }] as never}
      />,
    );

    expect(screen.getByRole("figure", { name: "Kriva veličina: stvarni i idealni udeo" })).toHaveAccessibleDescription(
      /Tačke na grafikonu: 1\..*Grupisanje: po veličinama.*Stvarno, Idealno/,
    );
  });
});
