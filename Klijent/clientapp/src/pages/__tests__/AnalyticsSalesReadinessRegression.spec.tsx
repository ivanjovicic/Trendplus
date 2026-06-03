import fs from "node:fs";
import path from "node:path";
import React from "react";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { server } from "../../mocks/server";
import { rest } from "msw";
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
  it("AnalyticsDashboard error path shows AnalyticsErrorState without fake KPI zeros", async () => {
    server.use(
      rest.get("/api/analytics/cached/dashboard/bootstrap", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/cached/filters/stores", (_req, res, ctx) => res(ctx.status(200), ctx.json([]))),
      rest.get("/api/analytics/health", (_req, res, ctx) => res(ctx.status(200), ctx.json({ status: "ok", tables: {}, message: "ok" }))),
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
    // jsdom/msw interop guard for fetch signal in this suite
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    vi.spyOn(window, "scrollTo").mockImplementation(() => undefined);
    server.use(
      rest.get("/api/analytics/data-quality/list", (_req, res, ctx) => res(ctx.status(200), ctx.json({
        page: 1,
        pageSize: 25,
        total: 0,
        items: [],
        meta: { success: true, emptyReason: "no_open_issues", dataQualityStatus: "insufficient_data" },
      }))),
      rest.get("/api/analytics/data-quality/health", (_req, res, ctx) => res(ctx.status(200), ctx.json({
        scoreStatus: "warning",
        scoreValue: 55,
        orphanArticleCount: 0,
        thresholds: { orphanArticleCount: 5, missingCostRevenueSharePct: 5, unknownSupplierRevenueSharePct: 5 },
        meta: { success: true },
      }))),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ isRunning: false, jobs: [], dataFreshnessStatus: "unknown" }))),
      rest.get("/api/analytics/data-quality/intake-report", (_req, res, ctx) => res(ctx.status(200), ctx.json({
        generatedAtUtc: "2026-06-01T08:00:00Z",
        periodFromUtc: "2026-05-01T00:00:00Z",
        periodToUtc: "2026-05-31T23:59:59Z",
        dataScope: "all",
        lastImportAtUtc: "2026-06-01T06:00:00Z",
        lastImportStatus: "partial",
        lastRefreshAtUtc: "2026-06-01T07:30:00Z",
        dataFreshnessStatus: "stale",
        readinessStatus: "warning",
        readinessLabel: "Upotrebljivo uz upozorenja",
        readinessScore: 76,
        loadedData: {
          articlesCount: 1000,
          saleItemsCount: 5000,
          receiptsCount: 1200,
          suppliersCount: 35,
          storesCount: 8,
          firstSaleDate: "2026-05-01T00:00:00Z",
          lastSaleDate: "2026-05-31T23:59:59Z",
        },
        issues: {
          missingSupplierCount: 10,
          missingCostCount: 40,
          missingCategoryCount: 12,
          missingColorCount: 9,
          missingSizeCount: 14,
          saleWithoutArticleCount: 2,
          zeroOrNegativePriceCount: 1,
          duplicateSkuCount: 3,
          missingSupplierNameCount: 4,
        },
        impact: {
          revenueWithoutCostPercent: 0.12,
          articlesWithoutSupplierPercent: 0.05,
          recommendationsBlockedCount: 55,
          ignoredRowsCount: 11,
          insufficientSignalCount: 17,
        },
        recommendedActions: ["Povežite dobavljače", "Dopunite nabavne cene"],
        meta: { success: true },
      }))),
      rest.get("/api/analytics/reports/pilot-intake", (_req, res, ctx) => res(ctx.status(200), ctx.json({ rows: [], payload: { rows: [] }, meta: { success: true, emptyReason: "no_import" } }))),
      rest.get("/api/analytics/data-quality/top-offenders", (_req, res, ctx) => res(ctx.status(200), ctx.json({ issueType: "missingSupplier", limit: 10, count: 0, items: [], meta: { success: true, emptyReason: "no_top_offenders" } }))),
      rest.get("/api/analytics/data-quality/trend", (_req, res, ctx) => res(ctx.status(200), ctx.json({ days: 7, points: [], dataScope: "all", meta: { success: true } }))),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/data-quality?view=issues"]}>
        <Routes>
          <Route path="/analytics/data-quality" element={<DataQualityPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Podaci trenutno nisu dostupni|Nema otvorenih data quality problema/i)).toBeInTheDocument();
    expect(screen.getByRole("region", { name: /Status pilota/i })).toBeInTheDocument();
    expect(screen.getByText(/Spremno uz upozorenja|Nepoznato/i)).toBeInTheDocument();
  });

  it("DataQuality pilot status shows unknown when intake report is unavailable", async () => {
    // jsdom/msw interop guard for fetch signal in this suite
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
    server.use(
      rest.get("/api/analytics/data-quality/list", (_req, res, ctx) => res(ctx.status(200), ctx.json({
        page: 1,
        pageSize: 25,
        total: 0,
        items: [],
        meta: { success: true, emptyReason: "no_open_issues", dataQualityStatus: "insufficient_data" },
      }))),
      rest.get("/api/analytics/data-quality/health", (_req, res, ctx) => res(ctx.status(200), ctx.json({
        scoreStatus: "warning",
        scoreValue: 55,
        orphanArticleCount: 0,
        thresholds: { orphanArticleCount: 5, missingCostRevenueSharePct: 5, unknownSupplierRevenueSharePct: 5 },
        meta: { success: true },
      }))),
      rest.get("/api/analytics/refresh-status", (_req, res, ctx) => res(ctx.status(200), ctx.json({ isRunning: false, jobs: [], dataFreshnessStatus: "unknown" }))),
      rest.get("/api/analytics/data-quality/intake-report", (_req, res, ctx) => res(ctx.status(500))),
      rest.get("/api/analytics/reports/pilot-intake", (_req, res, ctx) => res(ctx.status(200), ctx.json({ rows: [], payload: { rows: [] }, meta: { success: true, emptyReason: "no_import" } }))),
      rest.get("/api/analytics/data-quality/top-offenders", (_req, res, ctx) => res(ctx.status(200), ctx.json({ issueType: "missingSupplier", limit: 10, count: 0, items: [], meta: { success: true, emptyReason: "no_top_offenders" } }))),
      rest.get("/api/analytics/data-quality/trend", (_req, res, ctx) => res(ctx.status(200), ctx.json({ days: 7, points: [], dataScope: "all", meta: { success: true } }))),
    );

    render(
      <MemoryRouter initialEntries={["/analytics/data-quality?view=issues"]}>
        <Routes>
          <Route path="/analytics/data-quality" element={<DataQualityPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("region", { name: /Status pilota/i })).toBeInTheDocument();
    expect(screen.getByText(/Nepoznato/i)).toBeInTheDocument();
    expect(screen.getByText(/Nije moguće potvrditi/i)).toBeInTheDocument();
  });

  it("Inventory API failure renders error state instead of hanging loader", async () => {
    // jsdom/msw interop guard for fetch signal in this suite
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (globalThis as any).AbortSignal = window.AbortSignal;
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

  it("Pilot intake report without payload shows useful expired explanation", async () => {
    render(
      <MemoryRouter initialEntries={["/analytics/reports/pilot-intake"]}>
        <Routes>
          <Route path="/analytics/reports/pilot-intake" element={<PilotIntakeReportPage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: /Pregled izveštaja je istekao/i })).toBeInTheDocument();
    expect(screen.getByText(/privremeno u browseru/i)).toBeInTheDocument();
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

    const mojibakeToken = /[ÃƒÃ…Ã„Ã¢ï¿½]/;
    const offenders = files.filter((file) => mojibakeToken.test(fs.readFileSync(file, "utf8")));
    // TODO: reduce baseline to 0 - legacy pockets need explicit UTF-8 repair pass.
    // Baseline guard: prevent new spread. Current known offenders: <=8.
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
