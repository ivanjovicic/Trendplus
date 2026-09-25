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
    expect(screen.getByText("Backoffice")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Backoffice" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Odluke o proizvodima" })).toHaveAttribute(
      "href",
      "/analytics/products",
    );
  });

  it("marks only the canonical supplier page active after a legacy redirect", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=assortment&legacySource=operations-supplier-footwear"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    const activeLinks = screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page");
    expect(activeLinks).toHaveLength(1);
    expect(activeLinks[0]).toHaveAttribute("href", "/analytics/supplier");
    expect(activeLinks[0]).toHaveTextContent("Pregled dobavljača");
  });

  it("explains Operations supplier aliases before navigation", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("link", { name: /Prodaja po dobavljačima/ })).toBeInTheDocument();
    expect(screen.getByTitle("Kompatibilna veza: otvara Pregled dobavljača, tab Pregled")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Dobavljači i tipovi obuće/ })).toBeInTheDocument();
    expect(screen.getByTitle("Kompatibilna veza: otvara Pregled dobavljača, tab Asortiman")).toBeInTheDocument();
  });
});
