import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ExecutiveDecisionBoardPage from "./ExecutiveDecisionBoardPage";
import { getDecisionBoardAggregate } from "../services/analyticsApi";
import type {
  DecisionBoardAggregateResponse,
  DecisionBoardCard,
  DecisionBoardMetric,
  DecisionBoardSection,
  DecisionBoardSourceState,
} from "../types/analytics";

vi.mock("../components/analytics/AnalyticsTrustHeader", () => ({
  default: ({
    title,
    dataQualityStatus,
    recommendationNote,
    emptyStateReason,
  }: {
    title: string;
    dataQualityStatus?: string | null;
    recommendationNote?: string | null;
    emptyStateReason?: string | null;
  }) => (
    <div data-testid="analytics-trust-header">
      {title} | status: {dataQualityStatus ?? "n/a"} | note: {recommendationNote ?? "-"} | empty: {emptyStateReason ?? "-"}
    </div>
  ),
}));

vi.mock("../components/analytics/AnalyticsRefreshStatusBanner", () => ({
  default: ({ loading, error }: { loading?: boolean; error?: string | null }) => (
    <div data-testid="refresh-banner">{loading ? "loading" : error ?? "refresh-ok"}</div>
  ),
}));

vi.mock("../components/analytics/AnalyticsEmptyState", () => ({
  default: ({ title, message, emptyReason }: { title?: string; message?: string; emptyReason?: string | null }) => (
    <div data-testid="analytics-empty-state">
      {title} | {message} | {emptyReason ?? "-"}
    </div>
  ),
}));

vi.mock("../components/analytics/AnalyticsErrorState", () => ({
  default: ({ title, message, onRetry }: { title: string; message: string; onRetry?: () => void }) => (
    <div data-testid="analytics-error-state">
      <strong>{title}</strong>
      <span>{message}</span>
      <button type="button" onClick={onRetry}>Ponovo proveri</button>
    </div>
  ),
}));

vi.mock("../services/analyticsApi", async () => {
  const actual = await vi.importActual<typeof import("../services/analyticsApi")>("../services/analyticsApi");
  return {
    ...actual,
    getDecisionBoardAggregate: vi.fn(),
  };
});

function card(overrides: Partial<DecisionBoardCard> & Pick<DecisionBoardCard, "id" | "kind" | "sectionKey" | "sourceModule" | "title" | "riskIfIgnored" | "recommendedNextAction" | "actionHref" | "dataQualityStatus" | "priorityScore" | "impactScore">): DecisionBoardCard {
  return {
    sourceType: "product",
    sourceKey: overrides.id,
    summary: "Signal je spreman za odluku.",
    confidenceLevel: "high",
    confidenceScore: 88,
    reliabilityPct: 91,
    expectedImpactRsd: 120000,
    measuredImpactRsd: null,
    realizationRatio: null,
    alreadyInAction: false,
    alreadyClosed: false,
    warningCodes: [],
    generatedAtUtc: "2026-07-01T08:00:00Z",
    ...overrides,
  };
}

function section(key: string, cards: DecisionBoardCard[], overrides: Partial<DecisionBoardSection> = {}): DecisionBoardSection {
  return {
    key,
    title: overrides.title ?? `${key} sekcija`,
    description: overrides.description ?? `${key} opis`,
    sourceLink: overrides.sourceLink ?? `/analytics/${key}`,
    emptyMessage: overrides.emptyMessage ?? `${key} nema kartica`,
    warnings: overrides.warnings ?? cards.flatMap((item) => item.warningCodes),
    cards,
  };
}

function sourceState(overrides: Partial<DecisionBoardSourceState> = {}): DecisionBoardSourceState {
  return {
    sourceKey: "refresh-status",
    displayName: "Refresh status",
    status: "fresh",
    generatedAtUtc: "2026-07-01T08:00:00Z",
    warningCodes: [],
    message: null,
    sourceLink: "/admin/configuration?panel=workers",
    ...overrides,
  };
}

function aggregate(overrides: Partial<DecisionBoardAggregateResponse> = {}): DecisionBoardAggregateResponse {
  const urgentProduct = card({
    id: "product:sku-101",
    kind: "product",
    sectionKey: "urgent",
    sourceModule: "Odluke o proizvodima",
    sourceType: "product",
    sourceKey: "product:sku-101",
    title: "Crna kožna sandala",
    summary: "Visoka prodaja i nizak stock cover.",
    confidenceLevel: "high",
    confidenceScore: 91,
    expectedImpactRsd: 120000,
    riskIfIgnored: "Može doći do propuštene prodaje.",
    recommendedNextAction: "Dopuni veličine 38-40.",
    actionHref: "/analytics/actions?sourceType=product",
    dataQualityStatus: "good",
    priorityScore: 280,
    impactScore: 120000,
  });

  const blocker = card({
    id: "blocker:missing-cost",
    kind: "blocker",
    sectionKey: "blockers",
    sourceModule: "Kvalitet podataka",
    sourceType: "data_quality",
    sourceKey: "missing-cost",
    title: "Dopuni nabavnu cenu",
    summary: "Deo marže nije pouzdan zbog nedostajućih nabavnih cena.",
    confidenceLevel: "low",
    confidenceScore: 45,
    expectedImpactRsd: null,
    riskIfIgnored: "Marža i očekivani uticaj ostaju slabiji dok nedostaje nabavna cena.",
    recommendedNextAction: "Otvori kvalitet podataka i proveri mapiranje troškova.",
    actionHref: "/analytics/data-quality",
    dataQualityStatus: "warning",
    priorityScore: 220,
    impactScore: 0,
    warningCodes: ["missing_cost"],
  });

  const action = card({
    id: "action:101",
    kind: "action",
    sectionKey: "actionsDecision",
    sourceModule: "Centralne akcije",
    sourceType: "product",
    sourceKey: "product:sku-101",
    title: "Dopuni: Crna kožna sandala",
    summary: "Akcija je već u toku.",
    confidenceLevel: "high",
    confidenceScore: 88,
    expectedImpactRsd: 120000,
    riskIfIgnored: "Akcija može ostati nezatvorena.",
    recommendedNextAction: "Prati izvršenje i zatvori kada se sprovede.",
    actionHref: "/analytics/actions",
    alreadyInAction: true,
    dataQualityStatus: "good",
    priorityScore: 180,
    impactScore: 120000,
  });

  const inventory = card({
    id: "inventory:sku-201",
    kind: "inventory",
    sectionKey: "stockRisk",
    sourceModule: "Zalihe",
    sourceType: "inventory",
    sourceKey: "inventory:sku-201",
    title: "Dopuni: Crna kožna sandala",
    summary: "Signal ukazuje na rizičan stock cover.",
    confidenceLevel: "high",
    confidenceScore: 84,
    expectedImpactRsd: 98000,
    riskIfIgnored: "Moguća je propuštena prodaja i ubrzani stockout.",
    recommendedNextAction: "Dopuni veličine 38-40 i proveri raspoloživost.",
    actionHref: "/analytics/inventory",
    dataQualityStatus: "warning",
    priorityScore: 205,
    impactScore: 98000,
    confidenceSource: "signal",
    recommendationAllowed: false,
    reasonCodes: ["slow_stock", "out_of_stock_risk"],
    warningCodes: ["slow_stock", "out_of_stock_risk"],
  });

  const outcome = card({
    id: "outcome:summary",
    kind: "outcome",
    sectionKey: "actionsOutcome",
    sourceModule: "Ishodi akcija",
    sourceType: "product",
    sourceKey: "product:sku-101",
    title: "Realizacija očekivanog uticaja",
    summary: "Feedback loop je delimičan.",
    confidenceLevel: "medium",
    confidenceScore: 62,
    expectedImpactRsd: 120000,
    measuredImpactRsd: 90000,
    realizationRatio: 0.75,
    riskIfIgnored: "Nećemo znati da li su preporuke tačne.",
    recommendedNextAction: "Uporedi očekivani i izmereni uticaj.",
    actionHref: "/analytics/actions",
    alreadyClosed: true,
    dataQualityStatus: "warning",
    priorityScore: 160,
    impactScore: 120000,
    warningCodes: ["small_measured_sample"],
  });

  return {
    generatedAtUtc: "2026-07-01T08:05:00Z",
    periodFromUtc: "2026-06-01T00:00:00Z",
    periodToUtc: "2026-07-01T00:00:00Z",
    lastRefreshAtUtc: "2026-07-01T08:00:00Z",
    overallDataQualityStatus: "warning",
    recommendationNote: "Backend aggregate je izvor istine; frontend samo prikazuje kartice.",
    warnings: ["BOARD_PARTIAL"],
    metrics: [
      { label: "Urgentne odluke", value: "1", tone: "critical", note: "Najveći rizik prvo" },
      { label: "Očekivani uticaj", value: "120.000 RSD", tone: "warning" },
      { label: "Blokatori", value: "1", tone: "critical" },
    ] satisfies DecisionBoardMetric[],
    sourceStates: [
      sourceState({ sourceKey: "refresh-status", status: "fresh", displayName: "Refresh status" }),
      sourceState({ sourceKey: "data-quality-health", status: "warning", displayName: "Data quality health", warningCodes: ["missing_cost"] }),
    ],
    sections: [
      section("urgent", [urgentProduct], { title: "Top 5 urgentnih odluka", sourceLink: "/analytics/products" }),
      section("impact", [urgentProduct], { title: "Najveći očekivani uticaj", sourceLink: "/analytics/products" }),
      section("stockRisk", [inventory], { title: "Odluke o riziku zaliha", sourceLink: "/analytics/inventory" }),
      section("supplierRisk", [], { title: "Odluke o riziku i prilici kod dobavljača", sourceLink: "/analytics/supplier?tab=overview" }),
      section("blockers", [blocker], { title: "Blokatori kvaliteta podataka", sourceLink: "/analytics/data-quality" }),
      section("actionsDecision", [action], { title: "Akcije koje čekaju odluku", sourceLink: "/analytics/actions" }),
      section("actionsOutcome", [outcome], { title: "Akcije koje čekaju ishod", sourceLink: "/analytics/actions" }),
    ],
    meta: {
      success: true,
      dataQualityStatus: "warning",
      isPartial: true,
      warningCode: "BOARD_PARTIAL",
      message: "Neki izvori su upozoravajući, pa board mora ostati označen kao delimičan.",
    },
    ...overrides,
  };
}

function emptyAggregate(): DecisionBoardAggregateResponse {
  return aggregate({
    overallDataQualityStatus: "insufficient_data",
    recommendationNote: "Nema dovoljno potvrđenih signala.",
    warnings: [],
    metrics: [],
    sourceStates: [sourceState({ status: "insufficient_data", warningCodes: ["no_signal"] })],
    sections: [
      section("urgent", [], { title: "Top 5 urgentnih odluka", emptyMessage: "Nema urgentnih odluka." }),
      section("impact", [], { title: "Najveći očekivani uticaj", emptyMessage: "Nema procenjenog uticaja." }),
    ],
    meta: {
      success: true,
      dataQualityStatus: "insufficient_data",
      emptyReason: "Nema dovoljno kvalitetnih izvora za board.",
    },
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/analytics/decision-board"]}>
      <Routes>
        <Route path="/analytics/decision-board" element={<ExecutiveDecisionBoardPage />} />
        <Route path="/analytics/products" element={<div>Products route</div>} />
        <Route path="/analytics/data-quality" element={<div>Data quality route</div>} />
        <Route path="/analytics/actions" element={<div>Actions route</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("ExecutiveDecisionBoardPage", () => {
  beforeEach(() => {
    vi.mocked(getDecisionBoardAggregate).mockReset();
    vi.mocked(getDecisionBoardAggregate).mockResolvedValue(aggregate());
  });

  it("renders the executive decision board with metrics, sections, cards and action links", async () => {
    renderPage();

    expect(screen.getByTestId("refresh-banner")).toHaveTextContent("loading");
    expect((await screen.findAllByText("Crna kožna sandala")).length).toBeGreaterThan(0);

    expect(getDecisionBoardAggregate).toHaveBeenCalledWith({ dataScope: "all" });
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("Izvršni board odluka");
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("status: warning");
    expect(screen.getByText("Urgentne odluke")).toBeInTheDocument();
    expect(screen.getAllByText("120.000 RSD").length).toBeGreaterThan(0);
    expect(screen.getByText("Top 5 urgentnih odluka")).toBeInTheDocument();
    expect(screen.getByText("Blokatori kvaliteta podataka")).toBeInTheDocument();
    expect(screen.getByText("Odluke o riziku zaliha")).toBeInTheDocument();
    expect(screen.getByText("Akcije koje čekaju odluku")).toBeInTheDocument();
    expect(screen.getByText("Akcije koje čekaju ishod")).toBeInTheDocument();
    expect(screen.getByText("Dopuni nabavnu cenu")).toBeInTheDocument();
    const inventoryTitles = await screen.findAllByText("Dopuni: Crna kožna sandala");
    expect(inventoryTitles.length).toBeGreaterThan(0);
    const inventoryCard = inventoryTitles[0].closest("article");
    expect(inventoryCard).not.toBeNull();
    expect(within(inventoryCard as HTMLElement).getByText("Preporuka")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Blokirana")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Izvor pouzdanosti")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Signal zaliha")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Oprez")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Spor obrt")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).getByText("Rizik rasprodaje")).toBeInTheDocument();
    expect(within(inventoryCard as HTMLElement).queryAllByText("slow stock")).toHaveLength(0);
    expect(within(inventoryCard as HTMLElement).queryAllByText("out of stock risk")).toHaveLength(0);
    expect(screen.getByText("Realizacija očekivanog uticaja")).toBeInTheDocument();
    expect(screen.getAllByRole("link", { name: "Otvori izvor" }).some((link) => link.getAttribute("href") === "/analytics/data-quality")).toBe(true);
    expect(screen.getAllByRole("link", { name: "Dodaj u akcije" }).some((link) => link.getAttribute("href") === "/analytics/actions?sourceType=product")).toBe(true);
  });

  it("keeps partial-source warnings visible instead of presenting the board as fully green", async () => {
    renderPage();

    expect((await screen.findAllByText("Crna kožna sandala")).length).toBeGreaterThan(0);

    expect(screen.getByText("Delimični signali su dostupni.")).toBeInTheDocument();
    expect(screen.getByText(/Neki izvori su upozoravajući/i)).toBeInTheDocument();
    expect(screen.getByText("Nedostaje nabavna cena")).toBeInTheDocument();
    expect(screen.queryByText("missing cost")).not.toBeInTheDocument();
  });

  it("renders missing expected impact as unavailable, not as a fake zero", async () => {
    renderPage();

    await screen.findByText("Dopuni nabavnu cenu");
    const blockerCard = screen.getByText("Dopuni nabavnu cenu").closest("article");
    expect(blockerCard).not.toBeNull();

    expect(within(blockerCard as HTMLElement).getByText("Očekivani uticaj")).toBeInTheDocument();
    expect(within(blockerCard as HTMLElement).getByText("Nije dostupno")).toBeInTheDocument();
    expect(within(blockerCard as HTMLElement).queryByText("0 RSD")).not.toBeInTheDocument();
  });

  it("shows empty state when aggregate loads but contains no cards", async () => {
    vi.mocked(getDecisionBoardAggregate).mockResolvedValue(emptyAggregate());

    renderPage();

    const emptyState = await screen.findByTestId("analytics-empty-state");
    expect(emptyState).toHaveTextContent("Nema dovoljno signala za izvršni board");
    expect(emptyState).toHaveTextContent("Board je uspešno učitan, ali trenutno nema dovoljno kvalitetnih izvora da bi odluke bile smisleno rangirane.");
    expect(screen.getByTestId("analytics-trust-header")).toHaveTextContent("empty: Nema dovoljno kvalitetnih izvora za board.");
  });

  it("shows error state and retries the aggregate load", async () => {
    vi.mocked(getDecisionBoardAggregate)
      .mockRejectedValueOnce(new Error("Decision board API timeout"))
      .mockResolvedValueOnce(aggregate());

    renderPage();

    expect(await screen.findByTestId("analytics-error-state")).toHaveTextContent("Decision board API timeout");
    fireEvent.click(screen.getByRole("button", { name: "Ponovo proveri" }));

    expect((await screen.findAllByText("Crna kožna sandala")).length).toBeGreaterThan(0);
    await waitFor(() => expect(getDecisionBoardAggregate).toHaveBeenCalledTimes(2));
  });
});
