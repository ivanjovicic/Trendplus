import React from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import Sidebar from "../Sidebar";

describe("Sidebar", () => {
  it("renders the analytics IA labels and keeps the active route visible", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/products"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("button", { name: /Executive/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Odluke/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Operacije/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Kvalitet podataka/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Izveštaji \/ Legacy/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Odluke o proizvodima" })).toHaveAttribute(
      "href",
      "/analytics/products",
    );
  });
});
