import type { ReactNode } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SupplierDecisionQuadrant from "./SupplierDecisionQuadrant";

vi.mock("recharts", () => ({
  CartesianGrid: () => null,
  ResponsiveContainer: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Scatter: () => null,
  ScatterChart: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
  ZAxis: () => null,
}));

describe("SupplierDecisionQuadrant accessibility", () => {
  it("provides a named chart, same-data table and keyboard supplier action", () => {
    const onSelectSupplier = vi.fn();
    render(
      <SupplierDecisionQuadrant
        onSelectSupplier={onSelectSupplier}
        items={[{
          supplierId: 42,
          supplierName: "Alfa obuća",
          markdownDependency: 28,
          fullPriceSellthrough: 0.72,
          revenue: 12500,
          recommendationCode: "EXPAND",
          confidenceScore: 0.85,
          supplierQualityIndex: 0.8,
          reliabilityPct: 91,
          dataQualityStatus: "good",
          statusReason: "Dovoljno uporedivih podataka.",
        }] as never}
      />,
    );

    expect(screen.getByRole("figure", { name: "Kvadrant dobavljača" })).toHaveAccessibleDescription(
      /Grupisanje: po dobavljačima/,
    );
    expect(screen.getByRole("link", { name: "Prikaži tabelu podataka" })).toHaveAttribute(
      "href",
      "#supplier-decision-quadrant-table",
    );
    expect(screen.getByRole("cell", { name: "Dovoljno uporedivih podataka." })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Otvori detalj za Alfa obuća" }));
    expect(onSelectSupplier).toHaveBeenCalledWith(42);
  });
});
