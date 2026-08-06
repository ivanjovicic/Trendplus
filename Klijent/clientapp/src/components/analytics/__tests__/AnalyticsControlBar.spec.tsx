import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AnalyticsControlBar from "../AnalyticsControlBar";

describe("AnalyticsControlBar", () => {
  it("renders title, chips, fields and actions", () => {
    const onRefresh = vi.fn();

    render(
      <MemoryRouter>
        <AnalyticsControlBar
          title="Opseg i filteri"
          description="Kontrolisite period i fokus nad dashboardom."
          chips={[
            { key: "range", label: "Opseg", value: "30 dana", tone: "info" },
            {
              key: "freshness",
              label: "Svezina",
              value: "Dobro",
              tone: "success",
            },
          ]}
          primaryAction={{
            key: "refresh",
            label: "Osvezi dashboard",
            onClick: onRefresh,
          }}
          secondaryActions={[
            { key: "quality", label: "Kvalitet podataka", to: "/analytics/data-quality" },
          ]}
          fields={[
            {
              key: "period",
              label: "Period",
              control: (
                <select defaultValue="30d">
                  <option value="30d">Poslednjih 30 dana</option>
                </select>
              ),
            },
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByTestId("analytics-control-bar")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Opseg i filteri" })).toBeInTheDocument();
    expect(screen.getByText("Opseg")).toBeInTheDocument();
    expect(screen.getByText("30 dana")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute(
      "href",
      "/analytics/data-quality",
    );

    fireEvent.click(screen.getByRole("button", { name: "Osvezi dashboard" }));
    expect(onRefresh).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText("Period")).toBeInTheDocument();
  });
});
