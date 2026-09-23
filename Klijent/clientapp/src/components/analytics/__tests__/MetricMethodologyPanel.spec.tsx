import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import MetricMethodologyPanel from "../MetricMethodologyPanel";

describe("MetricMethodologyPanel", () => {
  it("renders formula and data source for documented metrics", () => {
    render(
      <MemoryRouter>
        <MetricMethodologyPanel metricKeys={["totalRevenue"]} />
      </MemoryRouter>
    );

    expect(screen.getByText("Prihod")).toBeInTheDocument();
    expect(screen.getByText(/SUM\(prodajna_vrednost_stavke\)/i)).toBeInTheDocument();
    expect(screen.getByText(/Sales facts analytics/i)).toBeInTheDocument();
  });

  it("shows the documented fallback message for unknown metrics", () => {
    render(
      <MemoryRouter>
        <MetricMethodologyPanel metricKeys={["unknown_metric"]} />
      </MemoryRouter>
    );

    expect(screen.getByText(/Metodologija za ovu metriku.*nije dokumentovana/i)).toBeInTheDocument();
  });

  it("documents every Supplier overview KPI methodology", () => {
    render(
      <MemoryRouter>
        <MetricMethodologyPanel
          metricKeys={[
            "revenue",
            "unitsSold",
            "totalCost",
            "marginContribution",
            "supplierAverageMarginPct",
            "topSupplierRevenueShare",
            "popRevenueChangePct",
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Nabavna vrednost")).toBeInTheDocument();
    expect(screen.getByText("PoP promena prometa")).toBeInTheDocument();
    expect(screen.getByText("Prosečna marža dobavljača")).toBeInTheDocument();
    expect(screen.getByText("Udeo top 5 dobavljača")).toBeInTheDocument();
    expect(screen.queryByText(/Metodologija za ovu metriku.*nije dokumentovana/i)).not.toBeInTheDocument();
  });
});
