import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AnalyticsChartAccessibility, describeChartProjection } from "../AnalyticsChartAccessibility";

describe("AnalyticsChartAccessibility", () => {
  it("names and describes a chart region from its displayed projection", () => {
    render(
      <AnalyticsChartAccessibility
        title="Trend prodaje"
        summary={describeChartProjection([{ day: "pon" }, { day: "uto" }], "po danima", ["promet", "prodajni dokumenti"])}
      >
        <div data-testid="chart">grafikon</div>
      </AnalyticsChartAccessibility>,
    );

    expect(screen.getByRole("figure", {
      name: "Trend prodaje",
      description: "Tačke na grafikonu: 2. Grupisanje: po danima. Serije: promet, prodajni dokumenti.",
    })).toContainElement(screen.getByTestId("chart"));
  });

  it("exposes an equivalent table as a keyboard-reachable link", () => {
    render(
      <AnalyticsChartAccessibility title="Raspodela" summary="Prikazane vrednosti po dobavljačima." tableTargetId="supplier-chart-data">
        <div>grafikon</div>
      </AnalyticsChartAccessibility>,
    );

    expect(screen.getByRole("link", { name: "Prikaži tabelu podataka" })).toHaveAttribute("href", "#supplier-chart-data");
  });

  it("hides a decorative chart from assistive technology", () => {
    render(
      <AnalyticsChartAccessibility title="Sakriven ukras" summary="Ukrasni trend." decorative>
        <div data-testid="sparkline">sparkline</div>
      </AnalyticsChartAccessibility>,
    );

    expect(screen.getByTestId("sparkline").parentElement).toHaveAttribute("aria-hidden", "true");
    expect(screen.queryByRole("figure")).not.toBeInTheDocument();
  });
});
