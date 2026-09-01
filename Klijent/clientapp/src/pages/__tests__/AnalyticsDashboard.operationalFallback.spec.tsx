import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { rest } from "../../mocks/mswCompat";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import * as analyticsApi from "../../services/analyticsApi";
import AnalyticsDashboard from "../AnalyticsDashboard";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

describe("AnalyticsDashboard operational fallback", () => {
  beforeEach(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortController = window.AbortController;

    vi.spyOn(analyticsApi, "getDashboardBootstrap").mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: {
        totalSkuCount: 100,
        totalOnHand: 40,
        outOfStockCount: 5,
        lowStockCount: 10,
        usedOperationalFallback: true,
      },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: null,
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [],
      errors: [],
      meta: {
        success: true,
        isPartial: true,
        warningCode: "inventory_status_operational_fallback",
        warningMessage:
          "Status zaliha je učitan iz operativne tabele Artikli jer analytics relacija nije dostupna.",
        dataQualityStatus: "warning",
      },
    });

    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) =>
        res(ctx.status(200), ctx.json([])),
      ),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) =>
        res(
          ctx.status(200),
          ctx.json({
            isRunning: false,
            currentStep: null,
            dataFreshnessStatus: "good",
            lastSuccessfulRefreshAtUtc: "2026-08-05T10:00:00Z",
            jobs: [],
          }),
        ),
      ),
    );
  });

  it("shows inventory Artikli fallback as a warning, not a trusted healthy count", async () => {
    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    const warnings = await screen.findAllByText(/Status zaliha je učitan iz operativne tabele Artikli/i);
    expect(warnings.length).toBeGreaterThan(0);
    expect(screen.getByText("Postoje upozorenja")).toBeInTheDocument();
    expect(screen.queryByText("Nema kritičnih count signala u ovom preseku.")).not.toBeInTheDocument();
  });

  it("renders dashboard action trust payloads without collapsing them into one legacy fallback", async () => {
    vi.mocked(analyticsApi.getDashboardBootstrap).mockResolvedValue({
      summary: {
        totalRevenue: 12345,
        totalTransactions: 12,
        totalUnits: 8,
      },
      inventory: {
        totalSkuCount: 100,
        totalOnHand: 40,
        outOfStockCount: 5,
        lowStockCount: 10,
        usedOperationalFallback: false,
      },
      dailySales: [],
      categoryData: [],
      genderData: [],
      supplierData: [],
      supplierOptions: [],
      weekdayData: [],
      hourData: [],
      paymentData: [],
      quickInsights: null,
      transactionStats: null,
      advanced: null,
      topAdvanced: null,
      validationCompleteness: null,
      validationFreshness: null,
      validationLostSales: null,
      executive: null,
      decisionActions: [
        {
          sourceType: "dashboard",
          priority: "P1",
          title: "Otvori inventar",
          description: "Prioritetna operativna akcija.",
          reason: "Rizična zaliha zahteva pregled.",
          statusReason: "Signal ready.",
          recommendationStatus: "READY",
          expectedImpact: "12.000 RSD",
          impactEstimateRsd: 12000,
          confidencePct: 82,
          reliabilityPct: 74,
          recommendationAllowed: true,
          dataQualityStatus: "good",
          actionUrl: "/analytics/inventory",
          metadata: {},
          link: "/analytics/inventory",
          linkLabel: "Otvori inventar",
        },
        {
          sourceType: "dashboard",
          priority: "P1",
          title: "Proveri kvalitet podataka",
          description: "Signal ograničen dok se ne potvrdi kvaliteta.",
          reason: "Missing cost coverage blocks the signal.",
          statusReason: "Freshness validation indicates stale data.",
          recommendationStatus: null,
          expectedImpact: null,
          impactEstimateRsd: null,
          confidencePct: null,
          reliabilityPct: null,
          recommendationAllowed: false,
          dataQualityStatus: "critical",
          actionUrl: "/analytics/data-quality",
          metadata: {},
          link: "/analytics/data-quality",
          linkLabel: "Otvori kvalitet podataka",
        },
        {
          sourceType: "dashboard",
          priority: "P3",
          title: "Monitor",
          description: "Legacy action without trust payload.",
          reason: "Nastavite monitoring metrika i osvežavajte agregate dnevno.",
          statusReason: "Legacy dashboard action bez trust payloada.",
          recommendationStatus: null,
          expectedImpact: null,
          impactEstimateRsd: null,
          confidencePct: null,
          reliabilityPct: null,
          recommendationAllowed: false,
          dataQualityStatus: "insufficient_data",
          actionUrl: "/analytics",
          metadata: {},
          link: "/analytics",
          linkLabel: "Otvori ekran",
        },
      ],
      errors: [],
      meta: {
        success: true,
        dataQualityStatus: "good",
      },
    } as never);

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    await screen.findByText("Otvori inventar", { selector: "strong" });
    expect(screen.getAllByText("Preporuka sistema")).toHaveLength(2);
    expect(screen.getAllByText("Pomoćni signal")).toHaveLength(2);
    expect(screen.getByText("Sigurnost: 82%")).toBeInTheDocument();
    expect(screen.getByText("Pouzdanost: 74%")).toBeInTheDocument();
    expect(screen.getByText("Kvalitet podataka: Dobro")).toBeInTheDocument();
    expect(screen.getByText("Kvalitet podataka: Kriticno / ne veruj")).toBeInTheDocument();
    expect(screen.getByText("Kvalitet podataka: Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.getByText("Legacy dashboard action bez trust payloada.")).toBeInTheDocument();
  });
});
