import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { DecisionSummaryBar } from "./DecisionSummaryBar";

describe("DecisionSummaryBar", () => {
  it("shows a data quality warning state with a Data Quality link", () => {
    render(
      <MemoryRouter>
        <DecisionSummaryBar
          balance={null}
          actionWorkflow={null}
          dataQualityWarning={true}
          dataQualityHref="/analytics/data-quality"
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Kvalitet podataka traži proveru")).toBeInTheDocument();
    expect(screen.queryByText("podaci OK")).not.toBeInTheDocument();

    const link = screen.getByRole("link", { name: "Otvori Data Quality" });
    expect(link).toHaveAttribute("href", "/analytics/data-quality");
  });

  it("shows healthy quality copy when the warning is not present", () => {
    render(
      <MemoryRouter>
        <DecisionSummaryBar
          balance={null}
          actionWorkflow={null}
          dataQualityWarning={false}
          dataQualityHref="/analytics/data-quality"
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("podaci OK")).toBeInTheDocument();
    expect(screen.queryByText("Kvalitet podataka traži proveru")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Otvori Data Quality" })).not.toBeInTheDocument();
  });

  it("keeps low-stock and OOS counts separate without subtraction or 7d labeling", () => {
    render(
      <MemoryRouter>
        <DecisionSummaryBar
          balance={null}
          actionWorkflow={null}
          lowStockCount={10}
          outOfStockCount={5}
          dataQualityWarning={false}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Trenutno OOS")).toBeInTheDocument();
    expect(screen.getByText("Niska zaliha")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.queryByText("P1 OOS 7d")).not.toBeInTheDocument();
    expect(screen.queryByText(/7d/i)).not.toBeInTheDocument();
    expect(screen.queryByText("5")).toBeInTheDocument();
    // Must not show derived 10-5=5 as the only low-stock value or a negative
    expect(screen.queryByText("-")).not.toBeInTheDocument();
  });

  it("renders unavailable for null stock counts instead of zero", () => {
    render(
      <MemoryRouter>
        <DecisionSummaryBar
          balance={null}
          actionWorkflow={null}
          lowStockCount={null}
          outOfStockCount={null}
          dataQualityWarning={false}
        />
      </MemoryRouter>,
    );

    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText("P1 OOS 7d")).not.toBeInTheDocument();
  });

  it("preserves measured zero counts as zero", () => {
    render(
      <MemoryRouter>
        <DecisionSummaryBar
          balance={null}
          actionWorkflow={null}
          lowStockCount={0}
          outOfStockCount={0}
          dataQualityWarning={false}
        />
      </MemoryRouter>,
    );

    expect(screen.getAllByText("0").length).toBeGreaterThanOrEqual(2);
    expect(screen.queryByText("Nije dostupno")).not.toBeInTheDocument();
  });
});
