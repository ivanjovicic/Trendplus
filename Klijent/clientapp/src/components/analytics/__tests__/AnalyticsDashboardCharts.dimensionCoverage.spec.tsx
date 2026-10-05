import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import AnalyticsDashboardCharts from "../AnalyticsDashboardCharts";

describe("AnalyticsDashboardCharts dimension coverage", () => {
  it("shows explanatory empty state instead of a 100% unknown category chart", () => {
    render(
      <AnalyticsDashboardCharts
        dailySales={[]}
        categoryPieData={[{ name: "Ostalo", value: 100 }]}
        genderPieData={[]}
        supplierBarData={[]}
        weekdayChartData={[]}
        hourChartData={[]}
        paymentChartData={[]}
        dimensionCoverage={{
          category: {
            knownCoveragePct: 0,
            dimensionCoverageState: "not_populated_in_source",
          },
        }}
        formatCurrency={(value) => `${value}`}
        formatNumber={(value) => `${value}`}
      />,
    );

    expect(screen.getByText(/Kategorija nije popunjena u izvoru/i)).toBeTruthy();
    expect(screen.queryByRole("img")).toBeNull();
  });
});
