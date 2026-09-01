import fs from "node:fs";
import path from "node:path";
import React from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import { rest } from "../../mocks/mswCompat";
import * as analyticsApi from "../../services/analyticsApi";
import AnalyticsDashboard from "../AnalyticsDashboard";
import DataQualityPage from "../DataQualityPage";
import InventoryPage from "../InventoryPage";
import PilotIntakeReportPage from "../PilotIntakeReportPage";
import ProductDecisionCenterPage from "../ProductDecisionCenterPage";
import SupplierDecisionHubPage from "../SupplierDecisionHubPage";

vi.mock("../../components/analytics/AnalyticsDashboardCharts", () => ({
  default: () => <div data-testid="charts-stub" />,
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({
  default: () => <div data-testid="trust-header-stub" />,
}));

vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({
  default: () => null,
}));

vi.mock("../../components/analytics/KpiExplainButton", () => ({
  default: () => null,
}));

vi.mock("../../components/analytics/PilotDataQualityIntakeReport", () => ({
  default: () => <div data-testid="pilot-intake-panel-stub" />,
}));

vi.mock("recharts", () => ({
  BarChart: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  ResponsiveContainer: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  CartesianGrid: () => null,
  XAxis: () => null,
  YAxis: () => null,
  Tooltip: () => null,
  Bar: () => null,
}));

describe("Analytics sales-readiness regressions", () => {
  beforeEach(() => {
    // Align fetch signal checks with the browser AbortSignal used by app hooks.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortController = window.AbortController;
  });

  it("AnalyticsDashboard error path shows AnalyticsErrorState without fake KPI zeros", async () => {
    server.use(
      rest.get("/api/analytics/cached/dashboard/bootstrap", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/health", (_req, res, ctx) => res(ctx.status(200), ctx.json({ status: "ok", tables: {}, message: "ok" }))),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ isRunning: false, jobs: [], dataFreshnessStatus: "unknown" }))),
    );

    render(
      <MemoryRouter>
        <AnalyticsDashboard />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Podaci trenutno nisu dostupni/i)).toBeInTheDocument();
    expect(screen.queryByText(/0 RSD/i)).not.toBeInTheDocument();
  });

  it("DataQuality empty issues render goes through EmptyState UX", async () => {
    vi.spyOn(window, "scrollTo").mockImplementation(() => undefined);
    vi.spyOn(analyticsApi, "getDataQualityIssues").mockResolvedValue({
      page: 1,
      pageSize: 25,
      total: 0,
      items: [],
      meta: { success: true, emptyReason: "no_open_issues", dataQualityStatus: "insufficient_data" },
    } as never);
    vi.spyOn(analyticsApi, "getAnalyticsDataQualityHealth").mockResolvedValue({
      scoreStatus: "warning",
      scoreValue: 55,
      orphanArticleCount: 0,
      thresholds: { orphanArticleCount: 5, missingCostRevenueSharePct: 5, unknownSupplierRevenueSharePct: 5 },
      meta: { success: true },
    } as never);
    vi.spyOn(analyticsApi, "getAnalyticsRefreshStatus").mockResolvedValue({
      isRunning: false,
      jobs: [],
      dataFreshnessStatus: "unknown",
    } as never);
    vi.spyOn(analyticsApi, "getPilotDataQualityIntakeReport").mockResolvedValue({
      readinessStatus: "warning",
      readinessLabel: "Upozorenje",
      readinessScore: 42,
      generatedAtUtc: "2026-08-21T08:00:00Z",
      lastImportAtUtc: "2026-08-21T07:45:00Z",
      lastImportStatus: "success",
      lastImportScope: "all",
      lastRefreshAtUtc: "2026-08-21T07:50:00Z",
      dataScope: "all",
      loadedData: {
        articlesCount: 0,
        saleItemsCount: 0,
        receiptsCount: 0,
        suppliersCount: 0,
        storesCount: 0,
        firstSaleDate: null,
        lastSaleDate: null,
      },
      issues: {
        missingSupplierCount: 0,
        missingCostCount: 0,
        missingCategoryCount: 0,
        missingColorCount: 0,
        missingSizeCount: 0,
        saleWithoutArticleCount: 0,
        zeroOrNegativePriceCount: 0,
        duplicateSkuCount: 0,
        missingSupplierNameCount: 0,
      },
      impact: {
        revenueWithoutCostPercent: 0,
        articlesWithoutSupplierPercent: 0,
        recommendationsBlockedCount: 0,
        ignoredRowsCount: 0,
        insufficientSignalCount: 0,
      },
      recommendedActions: [],
      meta: { success: true, emptyReason: "no_open_issues", dataQualityStatus: "insufficient_data" },
    } as never);
    vi.spyOn(analyticsApi, "getPilotIntakeDurableReport").mockResolvedValue(null as never);
    vi.spyOn(analyticsApi, "getDataQualityTopOffenders").mockResolvedValue({
      issueType: "missingSupplier",
      limit: 10,
      count: 0,
      items: [],
      meta: { success: true, emptyReason: "no_top_offenders" },
    } as never);
    vi.spyOn(analyticsApi, "getAnalyticsDataQualityTrend").mockResolvedValue({
      days: 7,
      points: [],
      dataScope: "all",
      meta: { success: true },
    } as never);

    render(
      <MemoryRouter initialEntries={["/analytics/data-quality?view=issues"]}>
        <Routes>
          <Route path="/analytics/data-quality" element={<DataQualityPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Nema dovoljno podataka za pouzdanu analizu/i })).toBeInTheDocument();
    expect(screen.getByText(/Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak/i)).toBeInTheDocument();
  });

  it("Inventory API failure renders error state instead of hanging loader", async () => {
    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/cached/filters/suppliers", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/cached/inventory/balance", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/cached/inventory/list", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/cached/inventory/insights", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/inventory/insights", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/cached/inventory/store-comparison", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", meta: { success: true, dataQualityStatus: "good" } }))),
      rest.get("/api/analytics/inventory/store-comparison", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", meta: { success: true, dataQualityStatus: "good" } }))),
      rest.get("/api/analytics/inventory/action-suggestions", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", pendingCount: 0, approvedCount: 0, deferredCount: 0, closedCount: 0, items: [], meta: { success: true, dataQualityStatus: "good" } }))),
      rest.get("/api/analytics/inventory/report-schedules", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/cached/inventory/forecast", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", items: [] }))),
      rest.get("/api/analytics/cached/inventory/alerts", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", items: [] }))),
      rest.get("/api/analytics/cached/inventory/rebalance-suggestions", (_req, res, ctx) => res(ctx.status(200), ctx.json({ generatedAtUtc: "2026-06-01T00:00:00Z", items: [] }))),
      rest.post("/api/analytics/actions/status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ items: [] }))),
    );

    render(
      <MemoryRouter>
        <InventoryPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Podaci trenutno nisu dostupni/i)).toBeInTheDocument();
  });

  it("Pilot intake report without preview state surfaces the durable empty state instead of an expired preview", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/reports/pilot-intake"]}>
        <Routes>
          <Route path="/analytics/reports/pilot-intake" element={<PilotIntakeReportPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Trajni izveštaj nema podatke/i })).toBeInTheDocument();
    expect(screen.queryByText(/privremeno u browseru/i)).not.toBeInTheDocument();
  });

  it("ProductDecisionCenter API failure renders error state without fake zeros", async () => {
    server.use(
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/cached/filters/suppliers", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/filters/suppliers", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/cached/products/decision-center", (_req, res, ctx) => res(ctx.status(500))),
      rest.post("/api/analytics/actions/status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ items: [] }))),
    );

    render(
      <MemoryRouter>
        <ProductDecisionCenterPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Podaci trenutno nisu dostupni/i)).toBeInTheDocument();
    expect(screen.queryByText(/0 RSD/i)).not.toBeInTheDocument();
  });

  it("SupplierDecisionHub API failure renders error state without fake zeros", async () => {
    server.use(
      rest.get("/api/analytics/suppliers/decision-hub/summary", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/suppliers/decision-hub/ranking", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/suppliers/decision-hub/quadrant", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ isRunning: false, jobs: [], dataFreshnessStatus: "unknown" }))),
      rest.get("/api/sezone", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/actions", (_req, res, ctx) => res(ctx.status(200), ctx.json({ items: [], total: 0 }))),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/supplier-decision-hub"]}>
        <Routes>
          <Route path="/analytics/supplier-decision-hub" element={<SupplierDecisionHubPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Podaci trenutno nisu dostupni/i)).toBeInTheDocument();
    expect(screen.queryByText(/0 RSD/i)).not.toBeInTheDocument();
  });

  it("mojibake guardrail for analytics TSX does not introduce new corrupt tokens", () => {
    const root = path.resolve(process.cwd(), "src");
    const files: string[] = [];
    const stack = [root];
    while (stack.length > 0) {
      const current = stack.pop()!;
      for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
        const full = path.join(current, entry.name);
        if (entry.isDirectory()) stack.push(full);
        else if (entry.isFile() && full.endsWith(".tsx") && full.toLowerCase().includes("analytics")) files.push(full);
      }
    }

    const mojibakeToken = /[ÃÅÄâ�]/;
    const offenders = files.filter((file) => mojibakeToken.test(fs.readFileSync(file, "utf8")));
    // TODO: reduce baseline to 0 — legacy pockets need explicit UTF-8 repair pass.
    // Baseline guard: prevent new spread. Current known offenders: ≤8.
    expect(offenders.length).toBeLessThanOrEqual(8);
  });

  it("guardrail: no local number formatter in analytics pages/components", () => {
    const root = path.resolve(process.cwd(), "src");
    const files: string[] = [];
    const stack = [root];
    while (stack.length > 0) {
      const current = stack.pop()!;
      for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
        const full = path.join(current, entry.name);
        if (entry.isDirectory()) stack.push(full);
        else if (
          entry.isFile()
          && /\.(ts|tsx)$/.test(full)
          && /analytics|inventory|supplier/i.test(full)
          && !/analyticsFormatters\.ts$/i.test(full)
          && !/supplierDecisionHub[\\/]utils\.ts$/i.test(full)
        ) {
          files.push(full);
        }
      }
    }

    const localFormatterHits = files
      .map((file) => ({ file, content: fs.readFileSync(file, "utf8") }))
      .filter(({ content }) => /new\s+Intl\.NumberFormat\s*\(/.test(content));

    expect(localFormatterHits).toEqual([]);
  });
});
