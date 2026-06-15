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

describe("AnalyticsActionsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    const item = {
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
      createdAtUtc: "2026-05-26T12:00:00Z",
      updatedAtUtc: "2026-05-26T12:00:00Z",
      resolvedAtUtc: null,
      createdByUserId: null,
      updatedByUserId: null,
      updatedByUserName: null,
      notes: [],
    };

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
        pendingOutcomeCount: 1,
        successCount: 1,
        neutralCount: 0,
        negativeCount: 1,
        notMeasuredCount: 0,
        outcomeCoverageRate: 0.5,
        positiveOutcomeRate: 0.5,
        negativeOutcomeRate: 0.5,
      },
      impact: {
        expectedImpactRsd: 12000,
        measuredImpactRsd: 3000,
        realizationRatio: 0.25,
        measuredImpactSampleCount: 2,
      },
      bySourceType: [
        {
          key: "inventory",
          label: "inventory",
          totalCount: 2,
          closedCount: 1,
          measuredCount: 1,
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

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();
    expect(screen.getByText("Sažetak ishoda akcija")).toBeInTheDocument();
    expect(screen.getByText("Pokrivenost ishodom")).toBeInTheDocument();
    expect(screen.getAllByText("50%").length).toBeGreaterThan(0);
    expect(screen.getByText("Malo izmerenih ishoda. Zaključci o uticaju nisu stabilni.")).toBeInTheDocument();
    expect(screen.getAllByText("Pozitivan ishod").length).toBeGreaterThan(0);
    expect(screen.getByText("Po kvalitetu podataka")).toBeInTheDocument();
    expect(screen.getByText("Po statusu ishoda")).toBeInTheDocument();
    expect(screen.getByText(/Izmereni uticaj:/)).toBeInTheDocument();
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
        }),
      );
    });

    expect(await screen.findByText("Negativan ishod")).toBeInTheDocument();
    expect(screen.getByText(/Napomena: Pad marže posle akcije\./)).toBeInTheDocument();
  });

  it("shows a user-friendly error when outcome update fails", async () => {
    updateAnalyticsActionOutcomeMock.mockRejectedValue(new Error("outcomeNotes must be 4000 characters or fewer"));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Ažuriraj ishod" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Ažuriraj ishod" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Ishod nije sačuvan. Proverite status i iznos.");
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
        pendingOutcomeCount: 0,
        successCount: 0,
        neutralCount: 0,
        negativeCount: 0,
        notMeasuredCount: 0,
        outcomeCoverageRate: null,
        positiveOutcomeRate: null,
        negativeOutcomeRate: null,
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
});
