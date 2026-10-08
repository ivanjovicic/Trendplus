import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProductDecisionCenterPage, {
  PRODUCT_DECISION_BLOCKED_KPI_VALUE,
  buildProductDecisionPopulationLine,
  formatProductDecisionActionFamilyKpi,
} from "../ProductDecisionCenterPage";
import type { AnalyticsResponseMeta, ProductDecisionCenterResponse } from "../../types/analytics";

vi.mock("react-router-dom", async () => {
  return {
    Link: ({ children }: { children: ReactNode }) => <a>{children}</a>,
  };
});

const getStoresMock = vi.fn();
const getSupplierFiltersMock = vi.fn();
const getProductDecisionCenterMock = vi.fn();
const getAnalyticsActionSourceStatusesMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getProductDecisionCenter: (...args: unknown[]) => getProductDecisionCenterMock(...args),
  getProductDecisionTimeline: vi.fn().mockResolvedValue({ events: [], emptyReason: "no_events" }),
  getProductDecisionTimelineExportCsv: vi.fn(),
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: vi.fn(),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => <button type="button">Izvoz</button> }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({ default: () => null }));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/ui/InfoTip", () => ({ default: () => null }));

function buildMeta(overrides: Partial<AnalyticsResponseMeta> = {}): AnalyticsResponseMeta {
  return {
    success: true,
    recommendationAllowed: true,
    decisionReadiness: {
      state: "decision_ready",
      surfaceRole: "recommendation",
      recommendationAllowed: true,
      reasonCodes: [],
      evidenceReferences: [],
    },
    ...overrides,
  };
}

function buildResponse(meta?: AnalyticsResponseMeta | null): ProductDecisionCenterResponse {
  return {
    generatedAtUtc: "2026-06-26T12:00:00Z",
    periodFromUtc: "2026-05-28T00:00:00Z",
    periodToUtc: "2026-06-26T00:00:00Z",
    totalRows: 1,
    analyzedRows: 12,
    ignoredRowsCount: 11,
    summary: {
      replenishCount: 0,
      markdownCount: 0,
      highPotentialCount: 0,
      badDataCount: 0,
      doNotOrderCount: 0,
      actionableCount: 0,
      blockedCount: 1,
      insufficientEvidenceCount: 1,
      stockCoverRiskCount: 0,
      insufficientStockCoverageCount: 0,
      lowCoverCount: 0,
      slowStockCount: 0,
      goodSellThroughCount: 0,
      lostSalesEstimate: null,
      slowStockCapital: null,
    },
    rows: [
      {
        productId: 101,
        productName: "Model X",
        sku: "SKU-101",
        category: "Sneakers",
        tipObuce: "Patike",
        supplierName: "Supplier A",
        supplierId: 77,
        revenue: 120000,
        unitsSold: 40,
        velocityUnitsPerDay: 1.2,
        marginContribution: 24000,
        marginPct: 24,
        marginQualityLabel: "Dobro",
        marginCoveragePct: 90,
        currentStock: 10,
        minStock: 5,
        stockGap: 0,
        trendPct: 3,
        confidencePct: 88,
        confidenceLevel: "high",
        confidenceScore: 88,
        reliabilityPct: 79,
        recommendationStatus: "REPLENISH",
        recommendationLabel: "Dopuni",
        recommendedAction: "Dopuni zalihe",
        recommendationReason: "Brza prodaja i nizak stock cover.",
        reasonCodes: ["high_velocity"],
        warningCodes: [],
        primaryDrivers: ["sales_velocity", "stock_risk"],
        lostSalesEstimate: 25000,
        expectedImpactRsd: 25000,
        impactWindowDays: 14,
        riskIfIgnored: "Rizik je gubitka prodaje.",
        explainabilityText: "Brza prodaja i nizak stock cover.",
        dataQualityStatus: "good",
        recommendationAllowed: true,
      },
    ],
    meta: meta ?? buildMeta(),
  };
}

describe("formatProductDecisionActionFamilyKpi", () => {
  it("keeps real zero when recommendations are allowed", () => {
    const result = formatProductDecisionActionFamilyKpi(0, buildMeta(), "—");
    expect(result).toEqual({ value: "0", blocked: false, reason: null });
  });

  it("shows em dash and taxonomy reason when recommendationAllowed is false", () => {
    const result = formatProductDecisionActionFamilyKpi(0, buildMeta({
      recommendationAllowed: false,
      decisionReadiness: {
        state: "blocked",
        surfaceRole: "recommendation",
        recommendationAllowed: false,
        reasonCodes: ["missing_cost_evidence"],
        evidenceReferences: [],
      },
    }), "—");
    expect(result.blocked).toBe(true);
    expect(result.value).toBe(PRODUCT_DECISION_BLOCKED_KPI_VALUE);
    expect(result.reason).toContain("Preporuka je blokirana");
    expect(result.reason).toContain("missing_cost_evidence");
  });

  it("does not invent a block for legacy payloads without readiness", () => {
    const result = formatProductDecisionActionFamilyKpi(0, { success: true }, "—");
    expect(result).toEqual({ value: "0", blocked: false, reason: null });
  });
});

describe("buildProductDecisionPopulationLine", () => {
  it("uses backend returned/analyzed totals", () => {
    expect(buildProductDecisionPopulationLine(buildResponse())).toBe(
      "Prikazano 1 od 12 redova (backend ukupno pre limita).",
    );
  });
});

describe("ProductDecisionCenterPage hierarchy", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getStoresMock.mockResolvedValue([]);
    getSupplierFiltersMock.mockResolvedValue([]);
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
  });

  it("renders blocked action KPIs as em dash with reason, never actionable zero", async () => {
    const blocked = buildResponse(buildMeta({
      recommendationAllowed: false,
      decisionReadiness: {
        state: "blocked",
        surfaceRole: "recommendation",
        recommendationAllowed: false,
        reasonCodes: ["missing_cost_evidence"],
        evidenceReferences: [],
      },
    }));
    getProductDecisionCenterMock.mockResolvedValue(blocked);

    render(<ProductDecisionCenterPage />);

    const replenish = await screen.findByTestId("kpi-replenish");
    expect(replenish).toHaveTextContent(PRODUCT_DECISION_BLOCKED_KPI_VALUE);
    expect(screen.getByTestId("kpi-replenish-reason")).toHaveTextContent(/Preporuka je blokirana/);
    expect(screen.getAllByText(/Prikazano 1 od 12 redova/).length).toBeGreaterThan(0);
  });

  it("keeps allowed-family zero and exposes Zašto aria-expanded", async () => {
    const allowed = buildResponse();
    getProductDecisionCenterMock.mockResolvedValue(allowed);

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByTestId("kpi-replenish")).toHaveTextContent("0");
    const why = await screen.findByRole("button", { name: "Zašto?" });
    expect(why).toHaveAttribute("aria-expanded", "false");
    expect(why).toHaveAttribute("aria-controls", "product-decision-why-101");

    fireEvent.click(why);
    expect(why).toHaveAttribute("aria-expanded", "true");
    expect(document.getElementById("product-decision-why-101")).not.toBeNull();
  });
});
