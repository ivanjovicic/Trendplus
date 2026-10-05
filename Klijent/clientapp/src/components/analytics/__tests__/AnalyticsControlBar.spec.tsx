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
    expect(screen.getByTestId("analytics-control-bar").querySelector(".analytics-control-bar__fields"))
      .toHaveClass("analytics-control-bar__fields--overflow-safe");
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

  it("opts a single pilot into responsive filter sizing without changing its controls", () => {
    render(
      <AnalyticsControlBar
        title="Inventory filteri"
        responsiveFilterLayout
        fields={[{
          key: "supplier",
          label: "Dobavljač",
          control: (
            <select aria-label="Filter po dobavljaču" defaultValue="all">
              <option value="all">Svi dobavljači</option>
            </select>
          ),
        }]}
      />,
    );

    expect(screen.getByTestId("analytics-control-bar")).toHaveClass(
      "analytics-control-bar--responsive-pilot",
    );
    expect(screen.getByLabelText("Filter po dobavljaču")).toHaveValue("all");
  });

  it("collapses the pilot filters on mobile and keeps their values when disclosed", () => {
    vi.stubGlobal("matchMedia", vi.fn(() => ({
      matches: true,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })));

    render(
      <AnalyticsControlBar
        title="Inventory filteri"
        responsiveFilterLayout
        mobileFilterSummary="Period 30 dana · Prodavnica: sve"
        fields={[{
          key: "supplier",
          label: "Dobavljač",
          control: (
            <select aria-label="Filter po dobavljaču" defaultValue="supplier-1">
              <option value="supplier-1">Dobavljač jedan</option>
            </select>
          ),
        }]}
      />,
    );

    const disclosure = screen.getByText("Filteri").closest("details");
    expect(disclosure).not.toHaveAttribute("open");
    expect(screen.getByText("Period 30 dana · Prodavnica: sve")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Filteri"));
    expect(disclosure).toHaveAttribute("open");
    expect(screen.getByLabelText("Filter po dobavljaču")).toHaveValue("supplier-1");
    vi.unstubAllGlobals();
  });
});
