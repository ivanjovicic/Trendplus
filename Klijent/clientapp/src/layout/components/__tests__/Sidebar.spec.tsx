import React from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import Sidebar from "../Sidebar";

describe("Sidebar", () => {
  it("renders the analytics IA labels and keeps the active route visible", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    expect(screen.getAllByRole("button", { name: /Glavne odluke/i }).length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Skupi meni" })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("button", { name: /Dodatne analize/i })).toBeInTheDocument();
    expect(screen.getByText("Backoffice")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Backoffice" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Dodatne analize/i })).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(screen.getByRole("button", { name: /Dodatne analize/i }));
    expect(screen.getByRole("button", { name: /Dodatne analize/i })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("link", { name: "Odluke o proizvodima" })).toHaveAttribute("href", "/analytics/products");
  });

  it("opens the primary group and marks only the canonical supplier page active", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=assortment&legacySource=operations-supplier-footwear"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    const activeLinks = screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page");
    expect(activeLinks).toHaveLength(1);
    expect(activeLinks[0]).toHaveAttribute("href", "/analytics/supplier");
    expect(activeLinks[0]).toHaveTextContent("Prodaja po dobavljačima");
    expect(screen.getByRole("button", { name: /Glavne odluke/i })).toHaveAttribute("aria-expanded", "true");
    fireEvent.click(screen.getByRole("button", { name: /Dodatne analize/i }));
    expect(screen.getByRole("link", { name: /Prodaja po smenama/ })).toHaveAttribute("href", "/analytics/daily-sales");
  });

  it("opens Dodatne analize and marks the scorecard query link active", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier?tab=scorecard"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    const activeLinks = screen.getAllByRole("link").filter((link) => link.getAttribute("aria-current") === "page");
    expect(activeLinks).toHaveLength(1);
    expect(activeLinks[0]).toHaveAttribute("href", "/analytics/supplier?tab=scorecard");
    expect(activeLinks[0]).toHaveTextContent("Ocena dobavljača");
    expect(screen.getByRole("button", { name: /Dodatne analize/i })).toHaveAttribute("aria-expanded", "true");
  });

  it("exposes the expanded state of the desktop sidebar rail toggle", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/products"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("button", { name: "Raširi meni" })).toHaveAttribute("aria-expanded", "false");
  });

  it("exposes mobile navigation as an accessible dialog with scroll lock", () => {
    const onCloseMobile = vi.fn();
    render(
      <MemoryRouter initialEntries={["/analytics/products"]}>
        <Sidebar mobileOpen={true} onCloseMobile={onCloseMobile} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    const dialog = screen.getByRole("dialog", { name: /Backoffice/i });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(document.body.style.overflow).toBe("hidden");

    fireEvent.keyDown(document, { key: "Escape" });
    expect(onCloseMobile).toHaveBeenCalledTimes(1);
  });

  it("keeps supplier aliases hidden while exposing canonical category analyses", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/inventory"]}>
        <Sidebar mobileOpen={false} onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    expect(screen.getByRole("link", { name: "Prodaja po dobavljačima" })).toHaveAttribute(
      "href",
      "/analytics/supplier",
    );
    expect(screen.queryByRole("link", { name: /Dobavljači i tipovi obuće/ })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Prodaja po tipu obuće/ })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Prodaja po boji artikla/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Prodaja po smenama/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Dodatne analize/i }));
    expect(screen.getByRole("link", { name: "Ocena dobavljača" })).toHaveAttribute("href", "/analytics/supplier?tab=scorecard");
    expect(screen.getByRole("link", { name: /Prodaja po boji artikla/ })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Prodaja po smenama/ })).toHaveAttribute("href", "/analytics/daily-sales");
    expect(screen.queryByRole("link", { name: /Prodaja po smeni i dobavljačima/ })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Pre\/Posle nivelacije/ })).toBeInTheDocument();
  });

  it("uses the same operational supplier navigation in the mobile sidebar", () => {
    render(
      <MemoryRouter initialEntries={["/analytics/supplier"]}>
        <Sidebar mobileOpen onCloseMobile={() => {}} collapsed={false} onToggleCollapse={() => {}} />
      </MemoryRouter>,
    );

    const dialog = screen.getByRole("dialog", { name: /Backoffice/i });
    expect(within(dialog).getByRole("button", { name: /Glavne odluke/i })).toHaveAttribute("aria-expanded", "true");
    expect(dialog.querySelectorAll('a[aria-current="page"]')).toHaveLength(1);
    expect(screen.getAllByRole("link", { name: "Prodaja po dobavljačima" })).toHaveLength(2);
    expect(within(dialog).getByRole("link", { name: "Prodaja po dobavljačima" })).toHaveAttribute(
      "href",
      "/analytics/supplier",
    );
  });
});
