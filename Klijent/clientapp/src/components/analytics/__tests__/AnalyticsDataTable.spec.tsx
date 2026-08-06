import React from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import AnalyticsDataTable from "../AnalyticsDataTable";

describe("AnalyticsDataTable", () => {
  it("renders toolbar content, row-count metadata, truncation metadata and table children", () => {
    render(
      <AnalyticsDataTable
        rowCount={2}
        truncationLabel="Prikaz je ogranicen na vracene redove."
        toolbar={<button type="button">Izvoz</button>}
      >
        <table>
          <thead>
            <tr>
              <th>Naziv</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>Red 1</td>
            </tr>
            <tr>
              <td>Red 2</td>
            </tr>
          </tbody>
        </table>
      </AnalyticsDataTable>,
    );

    expect(screen.getByTestId("analytics-data-table")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Izvoz" })).toBeInTheDocument();
    expect(screen.getByText("Prikazano: 2 redova")).toBeInTheDocument();
    expect(
      screen.getByText("Prikaz je ogranicen na vracene redove."),
    ).toBeInTheDocument();
    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByText("Red 2")).toBeInTheDocument();
  });
});
