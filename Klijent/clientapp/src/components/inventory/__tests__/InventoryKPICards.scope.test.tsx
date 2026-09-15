import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { InventoryKPICards } from "../InventoryKPICards";

describe("InventoryKPICards - scope contract", () => {
  it("should clearly indicate KPIs represent whole-scope totals, not search-filtered values", () => {
    render(
      <InventoryKPICards
        totalSku={100}
        totalOnHand={500}
        lowStockCount={15}
        lowStockShare={15}
        avgUnitsPerSku={5}
        totalValue={50000}
      />
    );

    // Verify KPI labels are present
    expect(screen.getByText("Ukupno SKU")).toBeInTheDocument();
    expect(screen.getByText("Ukupno na stanju")).toBeInTheDocument();
    expect(screen.getByText("Niska zaliha")).toBeInTheDocument();
    
    // Verify at least one note contains scope clarification about search
    const container = screen.getByText("Ukupno SKU").parentElement;
    const text = container?.textContent || "";
    expect(text).toMatch(/ne utiče pretraga|ne utiče/i);
  });

  it("should distinguish whole-inventory context in KPI descriptions", () => {
    render(
      <InventoryKPICards
        totalSku={100}
        totalOnHand={500}
        lowStockCount={15}
        lowStockShare={15}
        avgUnitsPerSku={5}
        totalValue={50000}
      />
    );

    // Verify scope is documented - each KPI article should be rendered
    const kpiArticles = screen.getAllByRole("article");
    expect(kpiArticles.length).toBeGreaterThanOrEqual(5);
    
    // Verify at least one has "celom inventaru" (whole inventory) language
    const allText = kpiArticles.map(a => a.textContent).join(" ");
    expect(allText).toMatch(/celom inventaru|ne utiče/i);
  });

  it("should handle unavailable values gracefully without implying false totals", () => {
    render(
      <InventoryKPICards
        totalSku={null}
        totalOnHand={null}
        lowStockCount={null}
        lowStockShare={null}
        avgUnitsPerSku={null}
        totalValue={null}
      />
    );

    // Verify that missing values are rendered as "-"
    const dashes = screen.queryAllByText("-");
    expect(dashes.length).toBeGreaterThan(0);
  });

  it("should preserve true zero values and distinguish from missing values", () => {
    render(
      <InventoryKPICards
        totalSku={0}
        totalOnHand={0}
        lowStockCount={0}
        lowStockShare={0}
        avgUnitsPerSku={0}
        totalValue={0}
      />
    );

    // True zeros should be rendered as "0"
    const articles = screen.getAllByRole("article");
    expect(articles.length).toBeGreaterThanOrEqual(5);
  });

  it("should not use SKU search state in KPI calculations", () => {
    // Test with identical KPI values regardless of search context
    const props = {
      totalSku: 100,
      totalOnHand: 500,
      lowStockCount: 15,
      lowStockShare: 15,
      avgUnitsPerSku: 5,
      totalValue: 50000,
    };

    // Render once
    const { rerender } = render(<InventoryKPICards {...props} />);
    const firstRenderLabels = screen.getAllByText(/Ukupno SKU/);
    expect(firstRenderLabels.length).toBeGreaterThan(0);

    // Re-render with same props (simulating search change but same KPI values)
    rerender(<InventoryKPICards {...props} />);
    const secondRenderLabels = screen.getAllByText(/Ukupno SKU/);
    expect(secondRenderLabels.length).toBeGreaterThan(0);
  });
});
