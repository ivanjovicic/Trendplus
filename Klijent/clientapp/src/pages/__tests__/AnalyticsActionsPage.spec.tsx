import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
const getAnalyticsActionByIdMock = vi.fn();
const updateAnalyticsActionOutcomeMock = vi.fn();
const updateAnalyticsActionStatusMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  getAnalyticsActions: (...args: unknown[]) => getAnalyticsActionsMock(...args),
  getAnalyticsActionCounts: (...args: unknown[]) => getAnalyticsActionCountsMock(...args),
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
    expect(screen.getByText("Uspešno")).toBeInTheDocument();
    expect(screen.getByText(/Izmereni uticaj:/)).toBeInTheDocument();
    expect(screen.getByText(/Napomena: Prodaja se ubrzala posle dopune\./)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Azuriraj ishod" }));
    fireEvent.change(screen.getByLabelText("Ishod"), { target: { value: "negative" } });
    fireEvent.change(screen.getByLabelText("Merljivi uticaj (RSD)"), { target: { value: "-500" } });
    fireEvent.change(screen.getByLabelText("Napomena"), { target: { value: "Pad marže posle akcije." } });
    fireEvent.click(screen.getByRole("button", { name: "Potvrdi" }));

    await waitFor(() => {
      expect(updateAnalyticsActionOutcomeMock).toHaveBeenCalledWith(
        7,
        expect.objectContaining({
          outcomeStatus: "negative",
          measuredImpactRsd: -500,
          outcomeNotes: "Pad marže posle akcije.",
          outcomeMeasuredAtUtc: undefined,
        }),
      );
    });

    expect(await screen.findByText("Negativno")).toBeInTheDocument();
    expect(screen.getByText(/Napomena: Pad marže posle akcije\./)).toBeInTheDocument();
  });

  it("shows a user-friendly error when outcome update fails", async () => {
    updateAnalyticsActionOutcomeMock.mockRejectedValue(new Error("outcomeNotes must be 4000 characters or fewer"));

    render(<AnalyticsActionsPage />);

    expect(await screen.findByText("Dopuni artikal A")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Azuriraj ishod" }));
    fireEvent.click(screen.getByRole("button", { name: "Potvrdi" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("outcomeNotes must be 4000 characters or fewer");
  });
});