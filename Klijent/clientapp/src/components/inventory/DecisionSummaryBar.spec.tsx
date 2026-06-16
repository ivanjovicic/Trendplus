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
});
