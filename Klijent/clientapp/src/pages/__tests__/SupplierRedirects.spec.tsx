import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from "react-router-dom";
import { describe, expect, it } from "vitest";
import {
  SupplierFootwearAnalyticsRedirect,
  SupplierSalesStatsRedirect,
} from "../SupplierRedirects";

function LocationProbe() {
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <>
      <output data-testid="location">{location.pathname}{location.search}</output>
      <button type="button" onClick={() => navigate(-1)}>Nazad</button>
    </>
  );
}

const redirectCases = [
  {
    name: "supplier sales",
    entry: "/analytics/supplier-sales-stats?fromDate=2026-06-01&toDate=2026-06-30&dataScope=imported&storeId=4",
    legacyRoute: "/analytics/supplier-sales-stats",
    redirect: SupplierSalesStatsRedirect,
    tab: "overview",
    source: "operations-supplier-sales",
  },
  {
    name: "supplier footwear",
    entry: "/analytics/dobavljaci-tipovi-obuce?fromDate=2026-05-01&toDate=2026-05-31&dataScope=existing&supplierId=7",
    legacyRoute: "/analytics/dobavljaci-tipovi-obuce",
    redirect: SupplierFootwearAnalyticsRedirect,
    tab: "assortment",
    source: "operations-supplier-footwear",
  },
] as const;

describe("Supplier legacy redirects", () => {
  it.each(redirectCases)("preserves $name query context and replaces the legacy history entry", async ({ entry, legacyRoute, redirect: Redirect, tab, source }) => {
    render(
      <MemoryRouter initialEntries={["/previous", entry]} initialIndex={1}>
        <Routes>
          <Route path={legacyRoute} element={<Redirect />} />
          <Route path="/analytics/supplier" element={<LocationProbe />} />
          <Route path="/previous" element={<div>Previous page</div>} />
        </Routes>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/analytics/supplier");
    });

    const locationText = screen.getByTestId("location").textContent ?? "";
    const query = new URLSearchParams(locationText.split("?")[1] ?? "");
    expect(query.get("tab")).toBe(tab);
    expect(query.get("legacySource")).toBe(source);
    expect(query.get("fromDate")).toBeTruthy();
    expect(query.get("toDate")).toBeTruthy();
    expect(query.get("dataScope")).toBeTruthy();
    expect(query.get("storeId") ?? query.get("supplierId")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Nazad" }));
    expect(await screen.findByText("Previous page")).toBeInTheDocument();
  });
});
