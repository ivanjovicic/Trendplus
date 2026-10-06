import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import AnalyticsDashboardCharts from "../AnalyticsDashboardCharts";

describe("AnalyticsDashboardCharts accessibility", () => {
  it("names chart figures and summarizes the displayed series projection", () => {
    render(
      <AnalyticsDashboardCharts
        dailySales={[{ date: "2026-10-01", totalRevenue: 100, transactionCount: 2 }] as never}
        categoryPieData={[{ name: "Obuća", value: 100 }]}
        genderPieData={[{ name: "Ženski", value: 100 }]}
        supplierBarData={[{ name: "Alfa", totalRevenue: 100 }]}
        weekdayChartData={[{ dayName: "Ponedeljak", totalRevenue: 100 }]}
        hourChartData={[{ label: "09", totalRevenue: 100 }]}
        paymentChartData={[{ name: "Kartica", totalRevenue: 100 }]}
        formatCurrency={(value) => `${value} RSD`}
        formatNumber={(value) => `${value}`}
      />,
    );

    expect(screen.getByRole("figure", { name: "Dnevni trend prodaje", description: /Tačke na grafikonu: 1\..*Promet, prodajni dokumenti/ })).toBeInTheDocument();
    expect(screen.getByRole("figure", { name: "Prodaja po kategorijama", description: /Grupisanje: po kategorijama/ })).toBeInTheDocument();
    expect(screen.getByRole("figure", { name: "Top dobavljači po prometu", description: /Grupisanje: po dobavljačima/ })).toBeInTheDocument();
  });
});
