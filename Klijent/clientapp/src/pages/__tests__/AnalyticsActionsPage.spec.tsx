import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsActionsPage from "../AnalyticsActionsPage";

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useLocation: () => ({
      search: "",
      pathname: "/analytics/actions",
      hash: "",
      state: null,
      key: "test",
    }),
  };
});

const getAnalyticsActionsMock = vi.fn();
const getAnalyticsActionCountsMock = vi.fn();
const getAnalyticsActionOutcomeSummaryMock = vi.fn();
const getAnalyticsActionByIdMock = vi.fn();
const updateAnalyticsActionOutcomeMock = vi.fn();
const updateAnalyticsActionStatusMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  getAnalyticsActions: (...args: unknown[]) => getAnalyticsActionsMock(...args),
  getAnalyticsActionCounts: (...args: unknown[]) => getAnalyticsActionCountsMock(...args),
  getAnalyticsActionOutcomeSummary: (...args: unknown[]) => getAnalyticsActionOutcomeSummaryMock(...args),
  getAnalyticsActionById: (...args: unknown[]) => getAnalyticsActionByIdMock(...args),
  updateAnalyticsActionOutcome: (...args: unknown[]) => updateAnalyticsActionOutcomeMock(...args),
  updateAnalyticsActionStatus: (...args: unknown[]) => updateAnalyticsActionStatusMock(...args),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));

function createActionItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 7,
    sourceType: "inventory",
    sourceKey: "inventory-7",
    title: "Dopuni artikal A",
    description: "Brza prodaja i nizak stock cover.",
    recommendationStatus: "dopuna",
    priority: "P1",
    impactEstimateRsd: 15000,
    dueAtUtc: "2026-06-01T00:00:00Z",
    expectedImpactRsd: 12000,
    measuredImpactRsd: 3000,
    outcomeStatus: "success",
    outcomeMeasuredAtUtc: "2026-06-10T00:00:00Z",
    outcomeNotes: "Prodaja se ubrzala posle dopune.",
    confidencePct: 82,
    reliabilityPct: 75,
    dataQualityStatus: "good",
    status: "accepted",
    actionUrl: null,
    metadataJson: null,
    ledgerSnapshot: null,
    impactLedger: {
      version: 1,
      sourceRecommendationId: "inventory:7:replenish",
      sourceRecommendationIdDerivation: "source_key",
      capturedAtUtc: "2026-06-01T00:00:00Z",
      snapshot: {
        expectedImpactBasis: "sales_velocity + stock_risk",
        primaryDrivers: ["sales_velocity", "stock_risk"],
        decisionReason: "Artikal ima ubrzanu prodaju i nizak stock cover.",
        recommendedAction: "Dopuni",
        sourcePeriodStartUtc: "2026-05-18T00:00:00Z",
        sourcePeriodEndUtc: "2026-06-01T00:00:00Z",
        sourceModule: "inventory",
        inputFreshnessStatus: "stale",
        impactWindowDays: 14,
      },
      resolution: {
        outcomeStatus: "success",
        measuredImpactRsd: 3000,
        measurementMethod: "manual_review",
        evidenceSource: "action_outcome_summary",
        outcomeMeasuredAtUtc: "2026-06-10T00:00:00Z",
        resolvedAtUtc: null,
        measuredWindowDays: 14,
        resolutionNote: "Prodaja se ubrzala posle dopune.",
      },
      derived: {
        impactDeltaRsd: -9000,
        realizationRatio: 0.25,
        calibrationBucket: "partial_realization",
        hasEvidence: true,
      },
    },
    createdAtUtc: "2026-05-26T12:00:00Z",
    updatedAtUtc: "2026-05-26T12:00:00Z",
    resolvedAtUtc: null,
    createdByUserId: null,
    updatedByUserId: null,
    updatedByUserName: null,
    notes: [],
    ...overrides,
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

function buildResponse(rows: Array<Record<string, unknown>>, totalCount = rows.length) {
  return {
    items: rows,
    totalCount,
    page: 1,
    pageSize: 50,
    totalPages: 1,
  };
}

function buildOutcomeSummaryResponse(createdCount: number, measuredImpactSampleCount: number) {
  return {
    meta: {
      success: true,
      periodMode: "created",
      createdFrom: "2026-03-17T00:00:00Z",
      createdTo: "2026-06-15T00:00:00Z",
      resolvedFrom: null,
      resolvedTo: null,
      measuredFrom: null,
      measuredTo: null,
      generatedAtUtc: "2026-06-15T00:00:00Z",
      sampleSize: createdCount,
      measuredSampleSize: measuredImpactSampleCount,
      warnings: [],
      emptyReason: null,
    },
    totals: {
      createdCount,
      closedCount: 0,
      openCount: 0,
      measuredCount: 0,
      measuredOutcomeCount: 0,
      pendingOutcomeCount: 0,
      successCount: 0,
      neutralCount: 0,
      negativeCount: 0,
      notMeasuredCount: 0,
      outcomeCoverageRate: null,
      positiveOutcomeRate: null,
      negativeOutcomeRate: null,
      closedOutcomeCoverageRate: null,
      measuredPositiveOutcomeRate: null,
      measuredNegativeOutcomeRate: null,
    },
    impact: {
      expectedImpactRsd: null,
      measuredImpactRsd: null,
      realizationRatio: null,
      measuredImpactSampleCount,
    },
    bySourceType: [],
    byPriority: [],
    byOutcomeStatus: [],
    byDataQuality: [],
    byConfidenceBucket: [],
    byReliabilityBucket: [],
  };
}

describe("AnalyticsActionsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    const item = createActionItem();

    getAnalyticsActionsMock.mockResolvedValue({
      items: [item],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    });
    getAnalyticsActionCountsMock.mockResolvedValue({
      new: 0,
      accepted: 1,
      deferred: 0,
      rejected: 0,
      done: 0,
      p1Open: 1,
    });
    getAnalyticsActionOutcomeSummaryMock.mockResolvedValue({
      meta: {
        success: true,
        periodMode: "created",
        createdFrom: "2026-03-17T00:00:00Z",
        createdTo: "2026-06-15T00:00:00Z",
        resolvedFrom: null,
        resolvedTo: null,
        measuredFrom: null,
        measuredTo: null,
        generatedAtUtc: "2026-06-15T00:00:00Z",
        sampleSize: 3,
        measuredSampleSize: 2,
        warnings: ["small_measured_sample"],
        emptyReason: null,
      },
      totals: {
        createdCount: 3,
        closedCount: 2,
        openCount: 1,
        measuredCount: 2,
        measuredOutcomeCount: 2,
        pendingOutcomeCount: 1,
        successCount: 1,
        neutralCount: 0,
        negativeCount: 1,
        notMeasuredCount: 0,
        outcomeCoverageRate: 0.5,
        positiveOutcomeRate: 0.5,
        negativeOutcomeRate: 0.5,
        closedOutcomeCoverageRate: 0.5,
        measuredPositiveOutcomeRate: 0.5,
        measuredNegativeOutcomeRate: 0.5,
      },
      impact: {
        expectedImpactRsd: 12000,
        measuredImpactRsd: 3000,
        realizationRatio: 0.25,
        measuredImpactSampleCount: 1,
      },
      bySourceType: [
        {
          key: "inventory",
          label: "inventory",
          totalCount: 2,
          closedCount: 1,
          measuredCount: 1,
          measuredOutcomeCount: 1,
          pendingOutcomeCount: 1,
          successCount: 1,
          neutralCount: 0,
          negativeCount: 0,
          notMeasuredCount: 0,
          expectedImpactRsd: 12000,
          measuredImpactRsd: 3000,
          outcomeCoverageRate: 1,
          positiveOutcomeRate: 1,
          negativeOutcomeRate: 0,
          closedOutcomeCoverageRate: 1,
          measuredPositiveOutcomeRate: 1,
          measuredNegativeOutcomeRate: 0,
          realizationRatio: 0.25,
          measuredImpactSampleCount: 1,
          warningCodes: [],
        },
      ],
      byPriority: [
        {
          key: "P1",
          label: "P1",
          totalCount: 2,
          closedCount: 1,
          measuredCount: 1,
          measuredOutcomeCount: 1,
          pendingOutcomeCount: 1,
          successCount: 1,
          neutralCount: 0,
          negativeCount: 0,
          notMeasuredCount: 0,
          expectedImpactRsd: 12000,
          measuredImpactRsd: 3000,
          outcomeCoverageRate: 1,
          positiveOutcomeRate: 1,
          negativeOutcomeRate: 0,
          closedOutcomeCoverageRate: 1,
          measuredPositiveOutcomeRate: 1,
          measuredNegativeOutcomeRate: 0,
          realizationRatio: 0.25,
          measuredImpactSampleCount: 1,
          warningCodes: [],
        },
      ],
      byOutcomeStatus: [
        {
          key: "success",
          label: "success",
          totalCount: 1,
          closedCount: 1,
          measuredCount: 1,
          pendingOutcomeCount: 0,
          successCount: 1,
          neutralCount: 0,
          negativeCount: 0,
          notMeasuredCount: 0,
          expectedImpactRsd: 12000,
          measuredImpactRsd: 3000,
          outcomeCoverageRate: 1,
          positiveOutcomeRate: 1,
          negativeOutcomeRate: 0,
          realizationRatio: 0.25,
          measuredImpactSampleCount: 1,
          warningCodes: [],
        },
      ],
      byDataQuality: [
        {
          key: "good",
          label: "good",
          totalCount: 2,
          closedCount: 1,
          measuredCount: 1,
          measuredOutcomeCount: 1,
          pendingOutcomeCount: 1,
          successCount: 1,
          neutralCount: 0,
          negativeCount: 0,
          notMeasuredCount: 0,
          expectedImpactRsd: 12000,
          measuredImpactRsd: 3000,
          outcomeCoverageRate: 1,
          positiveOutcomeRate: 1,
          negativeOutcomeRate: 0,
          closedOutcomeCoverageRate: 1,
          measuredPositiveOutcomeRate: 1,
          measuredNegativeOutcomeRate: 0,
          realizationRatio: 0.25,
          measuredImpactSampleCount: 1,
          warningCodes: [],
        },
      ],
      byConfidenceBucket: [],
      byReliabilityBucket: [],
    });
    getAnalyticsActionByIdMock.mockResolvedValue(item);
    updateAnalyticsActionStatusMock.mockResolvedValue(item);
    updateAnalyticsActionOutcomeMock.mockResolvedValue(item);
  });

  it("renders outcome fields in the row and updates outcome through the form", async () => {
    updateAnalyticsActionOutcomeMock.mockResolvedValue({
      id: 7,
      sourceType: "inventory",
      sourceKey: "inventory-7",
      title: "Dopuni artikal A",
      description: "Brza prodaja i nizak stock cover.",
      recommendationStatus: "dopuna",
      priority: "P1",
      impactEstimateRsd: 15000,
      dueAtUtc: "2026-06-01T00:00:00Z",
      expectedImpactRsd: 12000,
      measuredImpactRsd: -500,
      outcomeStatus: "negative",
      outcomeMeasuredAtUtc: "2026-06-12T00:00:00Z",
      outcomeNotes: "Pad marže posle akcije.",
      confidencePct: 82,
      reliabilityPct: 75,
      dataQualityStatus: "good",
      status: "accepted",
      actionUrl: null,
      metadataJson: null,
      createdAtUtc: "2026-05-26T12:00:00Z",
      updatedAtUtc: "2026-06-12T00:00:00Z",
      resolvedAtUtc: null,
      createdByUserId: null,
      updatedByUserId: null,
      updatedByUserName: null,
      notes: [],
    });

    updateAnalyticsActionOutcomeMock.mockResolvedValue(createActionItem({
      measuredImpactRsd: -500,
      outcomeStatus: "negative",
      outcomeMeasuredAtUtc: "2026-06-12T00:00:00Z",
      outcomeNotes: "Pad marže posle akcije.",
      updatedAtUtc: "2026-06-12T00:00:00Z",
      ledgerSnapshot: {
        schemaVersion: 1,
        creationSnapshot: {
          sourceRecommendationId: "inventory:7:replenish",
          recommendationType: "REPLENISH",
          expectedImpactBasis: "sales_velocity + stock_risk",
          impactWindowDays: 14,
          confidenceLevel: "medium",
          warningCodes: ["STALE_REFRESH"],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          decisionReason: "Artikal ima ubrzanu prodaju i nizak stock cover.",
          recommendedAction: "Dopuni",
          generatedAtUtc: "2026-06-01T00:00:00Z",
          inputFreshnessStatus: "stale",
        },
        resolutionSnapshot: {
          outcomeStatus: "negative",
          measuredImpactRsd: -500,
          outcomeMeasuredAtUtc: "2026-06-12T00:00:00Z",
          measuredWindowDays: 14,
          evidenceSource: "action_outcome_summary",
          evidenceReference: "summary:2026-06-12:inventory:7",
          resolutionNote: "Margin je pao posle dopune.",
        },
      },
      impactLedger: {
        version: 1,
        sourceRecommendationId: "inventory:7:replenish",
        sourceRecommendationIdDerivation: "source_key",
        capturedAtUtc: "2026-06-12T00:00:00Z",
        snapshot: {
          expectedImpactBasis: "sales_velocity + stock_risk",
          primaryDrivers: ["sales_velocity", "stock_risk"],
          decisionReason: "Artikal ima ubrzanu prodaju i nizak stock cover.",
          recommendedAction: "Dopuni",
          sourcePeriodStartUtc: "2026-05-18T00:00:00Z",
          sourcePeriodEndUtc: "2026-06-12T00:00:00Z",
          sourceModule: "inventory",
          inputFreshnessStatus: "stale",
          impactWindowDays: 14,
        },
        resolution: {
          outcomeStatus: "negative",
          measuredImpactRsd: -500,
          measurementMethod: "manual_review",
          evidenceSource: "action_outcome_summary",
          outcomeMeasuredAtUtc: "2026-06-12T00:00:00Z",
          resolvedAtUtc: null,
          measuredWindowDays: 14,
          resolutionNote: "Margin je pao posle dopune.",
        },
        derived: {
          impactDeltaRsd: -12500,
          realizationRatio: -0.0417,
          calibrationBucket: "negative_outcome",
          hasEvidence: true,
        },
      },
    }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    expect(screen.getByText("Sažetak ishoda akcija")).toBeInTheDocument();
    expect(screen.getByText(/Read-only pregled za akcije kreirane u poslednjih 90 dana/)).toBeInTheDocument();
    expect(screen.queryByText(/po datumu kreiranja/i)).not.toBeInTheDocument();
    expect(screen.getByText("Pokrivenost zatvorenih")).toBeInTheDocument();
    expect(screen.getByText("Pozitivan od izmerenih")).toBeInTheDocument();
    expect(screen.getByText("Po kvalitetu podataka")).toBeInTheDocument();
    expect(screen.getByText("Po statusu ishoda")).toBeInTheDocument();
    expect(screen.getByText(/Izmereni uticaj:/)).toBeInTheDocument();
    expect(screen.getByText("Uzorak uticaja: 1 od 2 izmerenih ishoda")).toBeInTheDocument();
    expect(screen.getByText("Realizacija pokriva samo poduzorak sa izmerenim uticajem.")).toBeInTheDocument();
    expect(screen.getByText(/Napomena: Prodaja se ubrzala posle dopune\./)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    fireEvent.change(screen.getByLabelText("Ishod"), { target: { value: "negative" } });
    fireEvent.change(screen.getByLabelText("Merljivi uticaj (RSD)"), { target: { value: "-500" } });
    fireEvent.change(screen.getByLabelText("Napomena"), { target: { value: "Pad marže posle akcije." } });
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Ažuriraj ishod" }));

    await waitFor(() => {
      expect(updateAnalyticsActionOutcomeMock).toHaveBeenCalledWith(
        7,
        expect.objectContaining({
          outcomeStatus: "negative",
          measuredImpactRsd: -500,
          outcomeNotes: "Pad marže posle akcije.",
          outcomeMeasuredAtUtc: expect.any(String),
          evidenceSource: "action_outcome_summary",
        }),
      );
    });

    expect(await screen.findByText("Negativan ishod")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));
    expect(await screen.findByText("Pregled ishoda")).toBeInTheDocument();
    expect(screen.getAllByText("action_outcome_summary").length).toBeGreaterThan(0);
    expect(screen.getAllByText("14 dana").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Pad mar/i).length).toBeGreaterThan(0);
  });

  it("locks measured fields for not_measured outcomes, clears evidence input, and submits null measured payload", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /A.*uriraj ishod/ }));

    const evidenceInput = screen.getByLabelText("Izvor dokaza") as HTMLInputElement;
    const impactInput = screen.getByLabelText("Merljivi uticaj (RSD)") as HTMLInputElement;
    const measuredAtInput = screen.getByLabelText("Datum merenja ishoda") as HTMLInputElement;

    expect(evidenceInput.value).toBe("action_outcome_summary");
    fireEvent.change(screen.getByLabelText("Ishod"), { target: { value: "not_measured" } });

    expect(screen.getByTestId("aaq-outcome-guidance")).toHaveTextContent(/Nije izmereno/);
    expect(screen.getByTestId("aaq-outcome-guidance")).toHaveTextContent(/nije 0 RSD/);
    expect(screen.getByText(/Merljiva polja su zaključana/)).toBeInTheDocument();
    expect(evidenceInput.value).toBe("");
    expect(evidenceInput).toBeDisabled();
    expect(impactInput).toBeDisabled();
    expect(measuredAtInput).toBeDisabled();

    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: /A.*uriraj ishod/ }));

    await waitFor(() => {
      expect(updateAnalyticsActionOutcomeMock).toHaveBeenCalledWith(
        7,
        expect.objectContaining({
          outcomeStatus: "not_measured",
          measuredImpactRsd: null,
          outcomeMeasuredAtUtc: null,
          evidenceSource: null,
        }),
      );
    });
  });

  it("shows qualitative outcome warning for legacy authoritative rows without evidence ledger", async () => {
    getAnalyticsActionsMock.mockResolvedValueOnce({
      items: [
        createActionItem({
          id: 11,
          title: "Legacy outcome bez dokaza",
          outcomeStatus: "success",
          measuredImpactRsd: null,
          outcomeMeasuredAtUtc: null,
          outcomeNotes: "Status je ručno unet bez merenja.",
          impactLedger: null,
          ledgerSnapshot: {
            schemaVersion: 1,
            creationSnapshot: null,
            resolutionSnapshot: {
              outcomeStatus: "success",
              measuredImpactRsd: null,
              outcomeMeasuredAtUtc: null,
              measuredWindowDays: null,
              evidenceSource: null,
              evidenceReference: null,
              resolutionNote: "Kvalitativna procena bez potvrđenog izvora.",
            },
          },
        }),
      ],
      totalCount: 1,
      page: 1,
      pageSize: 25,
      totalPages: 1,
    });
    getAnalyticsActionByIdMock.mockResolvedValueOnce(createActionItem({
      id: 11,
      title: "Legacy outcome bez dokaza",
      outcomeStatus: "success",
      measuredImpactRsd: null,
      outcomeMeasuredAtUtc: null,
      outcomeNotes: "Status je ručno unet bez merenja.",
      impactLedger: null,
      ledgerSnapshot: {
        schemaVersion: 1,
        creationSnapshot: null,
        resolutionSnapshot: {
          outcomeStatus: "success",
          measuredImpactRsd: null,
          outcomeMeasuredAtUtc: null,
          measuredWindowDays: null,
          evidenceSource: null,
          evidenceReference: null,
          resolutionNote: "Kvalitativna procena bez potvrđenog izvora.",
        },
      },
    }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Legacy outcome bez dokaza")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));
    expect(await screen.findByText("Pregled ishoda")).toBeInTheDocument();
    expect(screen.getByText("Ishod je evidentiran kvalitativno, ali bez potvrđenog dokaza i merljivog traga.")).toBeInTheDocument();
  });

  it("shows a pending outcome state without fake measured impact", async () => {
    const pendingItem = createActionItem({
      expectedImpactRsd: 12000,
      measuredImpactRsd: null,
      outcomeStatus: "pending",
      outcomeMeasuredAtUtc: null,
      outcomeNotes: null,
    });

    getAnalyticsActionsMock.mockResolvedValueOnce({
      items: [pendingItem],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    });
    getAnalyticsActionByIdMock.mockResolvedValueOnce(createActionItem({
      expectedImpactRsd: 12000,
      measuredImpactRsd: null,
      outcomeStatus: "pending",
      outcomeMeasuredAtUtc: null,
      outcomeNotes: null,
      ledgerSnapshot: {
        schemaVersion: 1,
        creationSnapshot: {
          sourceRecommendationId: "inventory:7:replenish",
          recommendationType: "REPLENISH",
          expectedImpactBasis: "sales_velocity + stock_risk",
          impactWindowDays: 14,
          confidenceLevel: "medium",
          warningCodes: [],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          decisionReason: "Artikal ima ubrzanu prodaju i nizak stock cover.",
          recommendedAction: "Dopuni",
          generatedAtUtc: "2026-06-01T00:00:00Z",
          inputFreshnessStatus: "fresh",
        },
        resolutionSnapshot: null,
      },
    }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    expect(await screen.findByText("Pregled ishoda")).toBeInTheDocument();
    expect(screen.getByText("Ishod je još u toku. Merljivi uticaj ostaje nedostupan dok merenje ne bude završeno.")).toBeInTheDocument();
    expect(screen.getAllByText("Još nije izmereno").length).toBeGreaterThan(0);
    expect(screen.queryByText(/Kalibracija poverenja/i)).not.toBeInTheDocument();
    expect(screen.queryByText("0 RSD")).not.toBeInTheDocument();
  });

  it("shows unavailable measured impact when an outcome status exists without proof", async () => {
    getAnalyticsActionsMock.mockResolvedValueOnce({
      items: [createActionItem({
        measuredImpactRsd: null,
        outcomeStatus: "success",
        outcomeMeasuredAtUtc: null,
        outcomeNotes: null,
      })],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    });
    getAnalyticsActionByIdMock.mockResolvedValueOnce(createActionItem({
      measuredImpactRsd: null,
      outcomeStatus: "success",
      outcomeMeasuredAtUtc: null,
      outcomeNotes: null,
      ledgerSnapshot: {
        schemaVersion: 1,
        creationSnapshot: {
          sourceRecommendationId: "inventory:7:replenish",
          recommendationType: "REPLENISH",
          expectedImpactBasis: "sales_velocity + stock_risk",
          impactWindowDays: 14,
          confidenceLevel: "medium",
          warningCodes: ["STALE_REFRESH"],
          primaryDrivers: ["sales_velocity", "stock_risk"],
          decisionReason: "Artikal ima ubrzanu prodaju i nizak stock cover.",
          recommendedAction: "Dopuni",
          generatedAtUtc: "2026-06-01T00:00:00Z",
          inputFreshnessStatus: "stale",
        },
        resolutionSnapshot: {
          measuredWindowDays: null,
          evidenceSource: null,
          evidenceReference: null,
          resolutionNote: null,
        },
      },
    }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Detalji" }));

    expect(await screen.findByText("Status ishoda je evidentiran, ali izmereni uticaj još nije dostupan.")).toBeInTheDocument();
    expect(screen.getAllByText("Nije dostupno").length).toBeGreaterThan(0);
    expect(screen.queryByText("0 RSD")).not.toBeInTheDocument();
  });

  it("shows a permission warning and keeps the row unchanged when status update is forbidden", async () => {
    getAnalyticsActionsMock.mockResolvedValueOnce({
      items: [
        {
          id: 7,
          sourceType: "inventory",
          sourceKey: "inventory-7",
          title: "Dopuni artikal A",
          description: "Brza prodaja i nizak stock cover.",
          recommendationStatus: "dopuna",
          priority: "P1",
          impactEstimateRsd: 15000,
          dueAtUtc: "2026-06-01T00:00:00Z",
          expectedImpactRsd: 12000,
          measuredImpactRsd: 3000,
          outcomeStatus: "success",
          outcomeMeasuredAtUtc: "2026-06-10T00:00:00Z",
          outcomeNotes: "Prodaja se ubrzala posle dopune.",
          confidencePct: 82,
          reliabilityPct: 75,
          dataQualityStatus: "good",
          status: "new",
          actionUrl: null,
          metadataJson: null,
          createdAtUtc: "2026-05-26T12:00:00Z",
          updatedAtUtc: "2026-05-26T12:00:00Z",
          resolvedAtUtc: null,
          createdByUserId: null,
          updatedByUserId: null,
          updatedByUserName: null,
          notes: [],
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
    });
    getAnalyticsActionCountsMock.mockResolvedValueOnce({
      new: 1,
      accepted: 0,
      deferred: 0,
      rejected: 0,
      done: 0,
      p1Open: 1,
    });
    updateAnalyticsActionStatusMock.mockRejectedValueOnce(Object.assign(new Error("Unauthorized"), { status: 403 }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Prihvati" }));

    expect(await screen.findByText("Nemate dozvolu za izmenu akcija. Preporuke ostaju dostupne za pregled.")).toBeInTheDocument();
    expect(screen.getAllByText("Novo").length).toBeGreaterThan(1);
  });

  it("shows a user-friendly error when outcome update fails", async () => {
    updateAnalyticsActionOutcomeMock.mockRejectedValue(new Error("outcomeNotes must be 4000 characters or fewer"));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Ažuriraj ishod" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Ishod nije sačuvan. Proverite status i iznos.");
  });

  it("shows a permission warning and keeps the row unchanged when outcome update is forbidden", async () => {
    updateAnalyticsActionOutcomeMock.mockRejectedValueOnce(Object.assign(new Error("Forbidden"), { status: 403 }));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Ažuriraj ishod" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Nemate dozvolu za izmenu akcija. Preporuke ostaju dostupne za pregled.");
    expect(updateAnalyticsActionOutcomeMock).toHaveBeenCalledTimes(1);
    expect(screen.getAllByText("Pozitivan ishod").length).toBeGreaterThan(1);
  });

  it("shows a non-blocking summary fallback when outcome summary fails", async () => {
    getAnalyticsActionOutcomeSummaryMock.mockRejectedValueOnce(new Error("summary unavailable"));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    expect(await screen.findByText("Sažetak ishoda trenutno nije dostupan. Lista akcija i dalje radi.")).toBeInTheDocument();
    expect(screen.getAllByText("Pozitivan ishod").length).toBeGreaterThan(0);
  });

  it("shows an empty summary state when there are no measured closed outcomes", async () => {
    getAnalyticsActionOutcomeSummaryMock.mockResolvedValueOnce({
      meta: {
        success: true,
        periodMode: "created",
        createdFrom: "2026-03-17T00:00:00Z",
        createdTo: "2026-06-15T00:00:00Z",
        resolvedFrom: null,
        resolvedTo: null,
        measuredFrom: null,
        measuredTo: null,
        generatedAtUtc: "2026-06-15T00:00:00Z",
        sampleSize: 0,
        measuredSampleSize: 0,
        warnings: [],
        emptyReason: "no_measured_closed_outcomes",
      },
      totals: {
        createdCount: 0,
        closedCount: 0,
        openCount: 0,
        measuredCount: 0,
        measuredOutcomeCount: 0,
        pendingOutcomeCount: 0,
        successCount: 0,
        neutralCount: 0,
        negativeCount: 0,
        notMeasuredCount: 0,
        outcomeCoverageRate: null,
        positiveOutcomeRate: null,
        negativeOutcomeRate: null,
        closedOutcomeCoverageRate: null,
        measuredPositiveOutcomeRate: null,
        measuredNegativeOutcomeRate: null,
      },
      impact: {
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        measuredImpactSampleCount: 0,
      },
      bySourceType: [],
      byPriority: [],
      byOutcomeStatus: [],
      byDataQuality: [],
      byConfidenceBucket: [],
      byReliabilityBucket: [],
    });

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    expect(await screen.findByText("Nema dovoljno zatvorenih i izmerenih akcija za pregled ishoda u ovom uzorku.")).toBeInTheDocument();
  });

  it("list error hides empty and fake measured impact", async () => {
    getAnalyticsActionsMock.mockRejectedValue(new Error("list down"));
    getAnalyticsActionOutcomeSummaryMock.mockRejectedValue(new Error("summary down"));
    getAnalyticsActionCountsMock.mockRejectedValue(new Error("counts down"));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("list down");
    expect(screen.queryByText("Nema akcija.")).not.toBeInTheDocument();
    expect(screen.queryByText("Izmereni uticaj")).not.toBeInTheDocument();
    expect(screen.queryByText("Dopuni artikal A")).not.toBeInTheDocument();
  });

  it("reloads summary only for source, priority and data quality filters", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    getAnalyticsActionOutcomeSummaryMock.mockClear();

    fireEvent.change(screen.getByLabelText("Filter po izvoru"), { target: { value: "supplier" } });
    await waitFor(() => {
      expect(getAnalyticsActionOutcomeSummaryMock).toHaveBeenLastCalledWith({
        sourceType: "supplier",
        priority: undefined,
        dataQualityStatus: undefined,
      });
    });

    fireEvent.change(screen.getByLabelText("Filter po prioritetu"), { target: { value: "P2" } });
    await waitFor(() => {
      expect(getAnalyticsActionOutcomeSummaryMock).toHaveBeenLastCalledWith({
        sourceType: "supplier",
        priority: "P2",
        dataQualityStatus: undefined,
      });
    });

    fireEvent.change(screen.getByLabelText("Filter po kvalitetu podataka"), { target: { value: "warning" } });
    await waitFor(() => {
      expect(getAnalyticsActionOutcomeSummaryMock).toHaveBeenLastCalledWith({
        sourceType: "supplier",
        priority: "P2",
        dataQualityStatus: "warning",
      });
    });
  });

  it("keeps the newest summary results when filter changes overlap", async () => {
    const firstSummary = createDeferred<ReturnType<typeof buildOutcomeSummaryResponse>>();
    const secondSummary = createDeferred<ReturnType<typeof buildOutcomeSummaryResponse>>();

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    getAnalyticsActionOutcomeSummaryMock
      .mockImplementationOnce(() => firstSummary.promise)
      .mockImplementationOnce(() => secondSummary.promise);

    fireEvent.change(screen.getByLabelText("Filter po izvoru"), { target: { value: "supplier" } });
    fireEvent.change(screen.getByLabelText("Filter po prioritetu"), { target: { value: "P2" } });

    await waitFor(() => {
      expect(getAnalyticsActionOutcomeSummaryMock).toHaveBeenCalledTimes(3);
    });

    secondSummary.resolve(buildOutcomeSummaryResponse(321, 321));

    const summaryLabel = await screen.findByText("Akcije u uzorku");
    const summaryCard = summaryLabel.closest(".aaq-summary-card");
    expect(summaryCard).not.toBeNull();
    expect(within(summaryCard as HTMLElement).getByText("321")).toBeInTheDocument();

    firstSummary.resolve(buildOutcomeSummaryResponse(7, 7));

    await waitFor(() => {
      expect(within(summaryCard as HTMLElement).getByText("321")).toBeInTheDocument();
    });
  });

  it("applies list filters when a summary bucket is clicked", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    expect(screen.getByText("Klik na red u sažetku primenjuje filter na listu akcija. Ponovni klik uklanja isti filter.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Zalihe/i }));
    await waitFor(() => {
      expect(screen.getByLabelText("Filter po izvoru")).toHaveValue("inventory");
    });

    fireEvent.click(screen.getByRole("button", { name: /Izvor: Zalihe/i }));
    await waitFor(() => {
      expect(screen.getByLabelText("Filter po izvoru")).toHaveValue("");
    });

    fireEvent.click(screen.getByRole("button", { name: /P1/i }));
    await waitFor(() => {
      expect(screen.getByLabelText("Filter po prioritetu")).toHaveValue("P1");
    });

    fireEvent.click(screen.getByRole("button", { name: /Dobar/i }));
    await waitFor(() => {
      expect(screen.getByLabelText("Filter po kvalitetu podataka")).toHaveValue("good");
    });
  });

  it("shows a reset chip for active summary filters", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Zalihe/i }));
    const chip = await screen.findByRole("button", { name: /Izvor: Zalihe/i });
    expect(chip).toBeInTheDocument();

    fireEvent.click(chip);
    await waitFor(() => {
      expect(screen.getByLabelText("Filter po izvoru")).toHaveValue("");
    });
  });

  it("clears all active summary filters with one reset action", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Filter po izvoru"), { target: { value: "inventory" } });
    fireEvent.change(screen.getByLabelText("Filter po prioritetu"), { target: { value: "P1" } });

    const resetButton = await screen.findByRole("button", { name: "Resetuj summary filtere" });
    expect(resetButton).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Izvor: Zalihe" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Prioritet: P1" })).toBeInTheDocument();

    fireEvent.click(resetButton);

    await waitFor(() => {
      expect(screen.getByLabelText("Filter po izvoru")).toHaveValue("");
      expect(screen.getByLabelText("Filter po prioritetu")).toHaveValue("");
      expect(screen.queryByRole("button", { name: "Resetuj summary filtere" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Izvor: Zalihe" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Prioritet: P1" })).not.toBeInTheDocument();
    });
  });

  it("removes one active summary filter from the helper links", async () => {
    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Filter po izvoru"), { target: { value: "inventory" } });
    fireEvent.change(screen.getByLabelText("Filter po prioritetu"), { target: { value: "P1" } });

    fireEvent.click(await screen.findByRole("button", { name: "Prioritet: P1" }));

    await waitFor(() => {
      expect(screen.getByLabelText("Filter po izvoru")).toHaveValue("inventory");
      expect(screen.getByLabelText("Filter po prioritetu")).toHaveValue("");
      expect(screen.getByRole("button", { name: /Izvor: Zalihe/i })).toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Prioritet: P1" })).not.toBeInTheDocument();
    });
  });
});
