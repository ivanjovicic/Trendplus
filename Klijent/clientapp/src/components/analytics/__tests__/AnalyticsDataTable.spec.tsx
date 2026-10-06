import React from "react";
import { act, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import AnalyticsDataTable from "../AnalyticsDataTable";

describe("AnalyticsDataTable", () => {
  it("sticks the key column only after ResizeObserver detects horizontal overflow", () => {
    const observers: Array<{ trigger: () => void }> = [];
    vi.stubGlobal("ResizeObserver", class {
      private readonly callback: ResizeObserverCallback;

      constructor(callback: ResizeObserverCallback) {
        this.callback = callback;
        observers.push({
          trigger: () => this.callback([], this as unknown as ResizeObserver),
        });
      }

      observe() {}
      disconnect() {}
    });

    const { unmount } = render(
      <AnalyticsDataTable rowCount={1}>
        <table>
          <thead><tr><th>Datum</th><th>Prodaja</th></tr></thead>
          <tbody><tr><td>2026-04-01</td><td>120.000 RSD</td></tr></tbody>
        </table>
      </AnalyticsDataTable>,
    );

    const scrollArea = screen.getByRole("region", { name: "Tabela sa vodoravnim pomeranjem" });
    Object.defineProperties(scrollArea, {
      clientWidth: { configurable: true, value: 320 },
      scrollWidth: { configurable: true, value: 760 },
    });
    expect(scrollArea).not.toHaveClass("analytics-wide-table-scroll--overflowing");

    act(() => observers[0]?.trigger());
    expect(scrollArea).toHaveClass("analytics-wide-table-scroll--overflowing");

    Object.defineProperty(scrollArea, "scrollWidth", { configurable: true, value: 300 });
    act(() => observers[0]?.trigger());
    expect(scrollArea).not.toHaveClass("analytics-wide-table-scroll--overflowing");
    unmount();
    vi.unstubAllGlobals();
  });

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
    expect(screen.getByTestId("analytics-data-table")).toHaveClass("analytics-data-table--responsive-pilot");
    expect(screen.getByRole("button", { name: "Izvoz" })).toBeInTheDocument();
    expect(screen.getByText("Prikazano: 2 redova")).toBeInTheDocument();
    expect(
      screen.getByText("Prikaz je ogranicen na vracene redove."),
    ).toBeInTheDocument();
    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByText("Red 2")).toBeInTheDocument();
  });

  it("opts a table into keyboard-accessible contained horizontal scrolling", () => {
    render(
      <AnalyticsDataTable responsivePilot rowCount={1}>
        <table>
          <thead><tr><th>Boja</th><th>Promet</th></tr></thead>
          <tbody><tr><td>Crna</td><td>120.000 RSD</td></tr></tbody>
        </table>
      </AnalyticsDataTable>,
    );

    expect(screen.getByTestId("analytics-data-table")).toHaveClass(
      "analytics-data-table--responsive-pilot",
    );
    expect(screen.getByRole("region", { name: "Tabela sa vodoravnim pomeranjem" })).toHaveAttribute(
      "tabindex",
      "0",
    );
    expect(screen.getByRole("note")).toHaveTextContent(
      "prevucite ili skrolujte za ostale kolone",
    );
    expect(screen.getByRole("columnheader", { name: "Boja" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Promet" })).toBeInTheDocument();
  });
});
