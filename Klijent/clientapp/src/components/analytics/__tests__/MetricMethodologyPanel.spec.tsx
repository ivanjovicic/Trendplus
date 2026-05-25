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

    expect(screen.getByText(/Metodologija za ovu metriku još nije dokumentovana/i)).toBeInTheDocument();
  });
});
