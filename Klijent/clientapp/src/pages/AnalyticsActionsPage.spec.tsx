import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsActionsPage from "./AnalyticsActionsPage";
import {
  getAnalyticsActionById,
  getAnalyticsActionCounts,
  getAnalyticsActionOutcomeSummary,
  getAnalyticsActions,
  updateAnalyticsActionOutcome,
  updateAnalyticsActionStatus,
} from "../services/analyticsApi";
import type {
  AnalyticsActionCounts,
  AnalyticsActionItem,
  AnalyticsActionListResponse,
  AnalyticsActionOutcomeSummaryBucket,
  AnalyticsActionOutcomeSummaryResponse,
} from "../types/analytics";

vi.mock("../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({ title }: { title: string }) => <div data-testid="analytics-trust-header">{title}</div>,
}));

vi.mock("../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/analyticsApi")>("../services/analyticsApi");
  return {
    ...actual,
    getAnalyticsActionById: vi.fn(),
    getAnalyticsActions: vi.fn(),
    getAnalyticsActionCounts: vi.fn(),
    getAnalyticsActionOutcomeSummary: vi.fn(),
    updateAnalyticsActionOutcome: vi.fn(),
    updateAnalyticsActionStatus: vi.fn(),
  };
});

function action(overrides: Partial<AnalyticsActionItem> = {}): AnalyticsActionItem {
  return {
    id: 101,
    sourceType: "inventory",
    sourceKey: "inventory:low-cover:101",
    sourceId: 101,
    title: "Dopuni kritičan artikal",
    description: "Zaliha je ispod minimalne količine.",
    recommendationStatus: "REPLENISH",
    priority: "P1",
    impactEstimateRsd: 45000,
    dueAtUtc: "2026-07-05T10:00:00Z",
    expectedImpactRsd: 38000,
    measuredImpactRsd: null,
    outcomeStatus: "pending",
    outcomeMeasuredAtUtc: null,
    outcomeNotes: "Čeka proveru posle dopune.",
    confidencePct: 86,
    reliabilityPct: 74,
    dataQualityStatus: "warning",
    status: "new",
    actionUrl: "/analytics/inventory?sku=101",
    metadataJson: JSON.stringify({ sku: "PLU-101", store: "Centar" }),
    ledgerSnapshot: {
      schemaVersion: 1,
      creationSnapshot: {
        sourceRecommendationId: "inventory:low-cover:101",
        recommendationType: "replenish",
        expectedImpactBasis: "lost sales risk",
        impactWindowDays: 14,
        confidenceLevel: "high",
        warningCodes: ["low_cover"],
        primaryDrivers: ["stock_cover_days", "sell_through"],
        decisionReason: "Stock cover is below threshold.",
        recommendedAction: "Replenish stock.",
        generatedAtUtc: "2026-07-01T08:00:00Z",
        inputFreshnessStatus: "fresh",
      },
      resolutionSnapshot: null,
    },
    createdAtUtc: "2026-07-01T08:00:00Z",
    updatedAtUtc: "2026-07-01T08:00:00Z",
    resolvedAtUtc: null,
    createdByUserId: "system",
    updatedByUserId: "system",
    updatedByUserName: "System",
    notes: [],
    impactLedger: null,
    ...overrides,
  };
}

function list(items: AnalyticsActionItem[] = [action()]): AnalyticsActionListResponse {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 50,
    totalPages: 1,
  };
}

function counts(overrides: Partial<AnalyticsActionCounts> = {}): AnalyticsActionCounts {
  return {
    new: 2,
    accepted: 1,
    deferred: 1,
    rejected: 1,
    done: 3,
    p1Open: 2,
    ...overrides,
  };
}

function bucket(overrides: Partial<AnalyticsActionOutcomeSummaryBucket> = {}): AnalyticsActionOutcomeSummaryBucket {
  return {
    key: "inventory",
    label: "Inventory",
    totalCount: 4,
    closedCount: 2,
    measuredCount: 1,
    pendingOutcomeCount: 1,
    successCount: 1,
    neutralCount: 0,
    negativeCount: 1,
    notMeasuredCount: 0,
    expectedImpactRsd: 80000,
    measuredImpactRsd: 24000,
    outcomeCoverageRate: 0.5,
    positiveOutcomeRate: 0.5,
    negativeOutcomeRate: 0.5,
    realizationRatio: 0.3,
    measuredImpactSampleCount: 1,
    warningCodes: [],
    ...overrides,
  };
}

function summary(overrides: Partial<AnalyticsActionOutcomeSummaryResponse> = {}): AnalyticsActionOutcomeSummaryResponse {
  return {
    meta: {
      success: true,
      periodMode: "created",
      createdFrom: "2026-04-01T00:00:00Z",
      createdTo: "2026-07-01T00:00:00Z",
      resolvedFrom: null,
      resolvedTo: null,
      measuredFrom: null,
      measuredTo: null,
      generatedAtUtc: "2026-07-01T08:00:00Z",
      sampleSize: 6,
      measuredSampleSize: 1,
      warnings: ["small_measured_sample"],
      emptyReason: null,
    },
    totals: {
      createdCount: 6,
      closedCount: 3,
      openCount: 3,
      measuredCount: 1,
      pendingOutcomeCount: 2,
      successCount: 1,
      neutralCount: 1,
      negativeCount: 1,
      notMeasuredCount: 0,
      outcomeCoverageRate: 0.5,
      positiveOutcomeRate: 0.33,
      negativeOutcomeRate: 0.33,
    },
    impact: {
      expectedImpactRsd: 120000,
      measuredImpactRsd: 24000,
      realizationRatio: 0.2,
      measuredImpactSampleCount: 1,
    },
    bySourceType: [bucket({ key: "inventory", label: "Inventory" })],
    byPriority: [bucket({ key: "P1", label: "P1" })],
    byOutcomeStatus: [bucket({ key: "success", label: "Success" })],
    byDataQuality: [bucket({ key: "warning", label: "Warning" })],
    byConfidenceBucket: [bucket({ key: "high", label: "High" })],
    byReliabilityBucket: [bucket({ key: "medium", label: "Medium" })],
    ...overrides,
  };
}

function renderPage(initialEntry = "/analytics/actions") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AnalyticsActionsPage />
    </MemoryRouter>,
  );
}

describe("AnalyticsActionsPage", () => {
  beforeEach(() => {
    vi.mocked(getAnalyticsActions).mockResolvedValue(list());
    vi.mocked(getAnalyticsActionCounts).mockResolvedValue(counts());
    vi.mocked(getAnalyticsActionOutcomeSummary).mockResolvedValue(summary());
    vi.mocked(getAnalyticsActionById).mockResolvedValue(action({
      notes: [
        {
          id: 1,
          actionItemId: 101,
          statusFrom: "new",
          statusTo: "accepted",
          note: "Prihvatio menadžer.",
          createdAtUtc: "2026-07-01T09:00:00Z",
          createdByUserId: "u1",
          createdByUserName: "Ivan",
        },
      ],
      impactLedger: {
        version: 1,
        sourceRecommendationId: "inventory:low-cover:101",
        sourceRecommendationIdDerivation: "exact",
        capturedAtUtc: "2026-07-01T08:00:00Z",
        snapshot: {
          expectedImpactBasis: "lost sales risk",
          primaryDrivers: ["stock_cover_days"],
          decisionReason: "Niska zaliha.",
          recommendedAction: "Dopuniti artikal.",
          sourcePeriodStartUtc: "2026-06-01T00:00:00Z",
          sourcePeriodEndUtc: "2026-06-30T23:59:59Z",
          sourceModule: "inventory",
          inputFreshnessStatus: "fresh",
          impactWindowDays: 14,
        },
        resolution: {
          outcomeStatus: "pending",
          measuredImpactRsd: null,
          measurementMethod: null,
          evidenceSource: null,
          outcomeMeasuredAtUtc: null,
          resolvedAtUtc: null,
          measuredWindowDays: null,
          resolutionNote: null,
        },
        derived: {
          impactDeltaRsd: null,
          realizationRatio: null,
          calibrationBucket: null,
          hasEvidence: false,
        },
      },
    }));
    vi.mocked(updateAnalyticsActionStatus).mockImplementation(async (id, input) => action({ id, status: input.status }));
    vi.mocked(updateAnalyticsActionOutcome).mockImplementation(async (id, input) => action({
      id,
      outcomeStatus: input.outcomeStatus,
      measuredImpactRsd: input.measuredImpactRsd ?? null,
      outcomeMeasuredAtUtc: input.outcomeMeasuredAtUtc ?? null,
      outcomeNotes: input.outcomeNotes ?? null,
    }));
  });

  it("renders action KPIs, outcome summary, list rows and pending evidence copy", async () => {
    renderPage();

    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Akcije i preporuke");
    await screen.findByText("Dopuni kritičan artikal");

    expect(screen.getByText("P1 otvoreno")).toBeInTheDocument();
    expect(screen.getByText("Sažetak ishoda akcija")).toBeInTheDocument();
    expect(screen.getByText("Malo izmerenih ishoda. Zaključci o uticaju nisu stabilni.")).toBeInTheDocument();
    expect(screen.getByText("Akcije u uzorku")).toBeInTheDocument();
    expect(screen.getByText("Zalihe")).toBeInTheDocument();
    expect(screen.getByText("REPLENISH")).toBeInTheDocument();
    expect(screen.getByText("Upozorenje")).toBeInTheDocument();
    expect(screen.getByText("Čeka proveru")).toBeInTheDocument();
    expect(screen.getByText(/Izmereni uticaj: Još nije izmereno/i)).toBeInTheDocument();
  });

  it("initializes from query string and keeps inventory shortcut visible", async () => {
    renderPage("/analytics/actions?sourceType=inventory");

    await screen.findByText("Dopuni kritičan artikal");

    expect(getAnalyticsActions).toHaveBeenCalledWith(expect.objectContaining({ sourceType: "inventory", page: 1, pageSize: 50 }));
    expect(screen.getByRole("link", { name: "Otvori Inventory Analytics" })).toHaveAttribute("href", "/analytics/inventory");
  });

  it("applies outcome-summary bucket filters to the action list", async () => {
    renderPage();
    await screen.findByText("Dopuni kritičan artikal");

    fireEvent.click(screen.getByRole("button", { name: /Zalihe/i }));

    await waitFor(() => expect(getAnalyticsActions).toHaveBeenLastCalledWith(expect.objectContaining({
      sourceType: "inventory",
      page: 1,
      pageSize: 50,
    })));
    expect(screen.getByRole("button", { name: /Izvor: Zalihe/i })).toBeInTheDocument();
  });

  it("expands details with ledger and note evidence", async () => {
    renderPage();
    await screen.findByText("Dopuni kritičan artikal");

    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    expect(await screen.findByText("Outcome pregled")).toBeInTheDocument();
    expect(getAnalyticsActionById).toHaveBeenCalledWith(101);
    expect(screen.getByText(/Ishod je još u toku/i)).toBeInTheDocument();
    expect(screen.getByText("Ledger uticaja")).toBeInTheDocument();
    expect(screen.getByText("Prihvatio menadžer.")).toBeInTheDocument();
  });

  it("updates action status directly for accept action", async () => {
    renderPage();
    await screen.findByText("Dopuni kritičan artikal");

    fireEvent.click(screen.getByRole("button", { name: "Prihvati" }));

    await waitFor(() => expect(updateAnalyticsActionStatus).toHaveBeenCalledWith(101, { status: "accepted", note: undefined }));
  });

  it("keeps pending outcome evidence fields disabled and submits null measured evidence", async () => {
    renderPage();
    await screen.findByText("Dopuni kritičan artikal");

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    const dialog = await screen.findByRole("dialog", { name: "Ažuriraj ishod" });

    expect(within(dialog).getByLabelText("Merljivi uticaj (RSD)")).toBeDisabled();
    expect(within(dialog).getByLabelText("Datum merenja ishoda")).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText("Napomena"), { target: { value: "Još čekamo dokaz." } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Ažuriraj ishod" }));

    await waitFor(() => expect(updateAnalyticsActionOutcome).toHaveBeenCalledWith(101, {
      outcomeStatus: "pending",
      measuredImpactRsd: null,
      outcomeMeasuredAtUtc: null,
      outcomeNotes: "Još čekamo dokaz.",
    }));
  });

  it("submits measured successful outcome with parsed impact and note", async () => {
    renderPage();
    await screen.findByText("Dopuni kritičan artikal");

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    const dialog = await screen.findByRole("dialog", { name: "Ažuriraj ishod" });

    fireEvent.change(within(dialog).getByLabelText("Ishod"), { target: { value: "success" } });
    fireEvent.change(within(dialog).getByLabelText("Merljivi uticaj (RSD)"), { target: { value: "12500,50" } });
    fireEvent.change(within(dialog).getByLabelText("Datum merenja ishoda"), { target: { value: "2026-07-01T10:30" } });
    fireEvent.change(within(dialog).getByLabelText("Napomena"), { target: { value: "Dopuna je donela dodatnu prodaju." } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Ažuriraj ishod" }));

    await waitFor(() => expect(updateAnalyticsActionOutcome).toHaveBeenCalledWith(101, expect.objectContaining({
      outcomeStatus: "success",
      measuredImpactRsd: 12500.5,
      outcomeMeasuredAtUtc: expect.any(String),
      outcomeNotes: "Dopuna je donela dodatnu prodaju.",
    })));
  });
});
