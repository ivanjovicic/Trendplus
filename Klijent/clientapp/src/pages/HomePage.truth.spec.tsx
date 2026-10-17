import { act, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import HomePage from "./HomePage";

const backendStatus = vi.hoisted(() => ({
  current: { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") as Date | null },
}));
const refreshStatusMock = vi.hoisted(() => vi.fn());

vi.mock("../context/useBackendStatus", () => ({
  useBackendStatus: () => backendStatus.current,
}));
vi.mock("../services/analyticsApi", () => ({
  getAnalyticsRefreshStatus: refreshStatusMock,
}));
vi.mock("../components/dashboard/TrendModelList", () => ({
  default: () => <div data-testid="trend-model-list" />,
}));
vi.mock("../components/trendshoes/SeasonalImageCarousel", () => ({
  default: () => <div data-testid="seasonal-image-carousel" />,
}));

function refreshStatus(overrides: Record<string, unknown> = {}) {
  return {
    lastSuccessfulRefreshAtUtc: "2026-10-15T05:00:00Z",
    lastSuccessfulImportAtUtc: "2026-10-15T04:30:00Z",
    observedSalesPeriodFromUtc: "2025-01-02T09:15:00Z",
    observedSalesPeriodToUtc: "2026-10-14T19:40:00Z",
    isRunning: false,
    refreshedObjects: [],
    failedObjects: [],
    dataFreshnessStatus: "fresh",
    processMode: "worker",
    processType: "web",
    workersEnabled: true,
    generatedAtUtc: "2026-10-15T06:00:00Z",
    jobs: [],
    ...overrides,
  };
}

function renderHome() {
  return render(
    <MemoryRouter initialEntries={["/"]}>
      <HomePage />
    </MemoryRouter>,
  );
}

async function settled() {
  await waitFor(() => expect(screen.getByTestId("home-freshness").textContent).not.toContain("Proveravam"));
}

describe("HomePage business truth (RQ661)", () => {
  beforeEach(() => {
    backendStatus.current = { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") };
    refreshStatusMock.mockReset();
    refreshStatusMock.mockResolvedValue(refreshStatus());
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("does not present hard-coded numbers or recommendations as measured business facts", async () => {
    const { container } = renderHome();
    await settled();
    const text = container.textContent ?? "";

    for (const claim of ["+12.8%", "Trend signal", "Import jobs", "Aktivne sekcije", "18%", "3 SKU", "top 10 artikala", "Insight Pulse", "trend scraping"]) {
      expect(text, `Home must not show "${claim}"`).not.toContain(claim);
    }
    expect(screen.queryByTestId("trend-model-list")).not.toBeInTheDocument();
    expect(screen.queryByTestId("seasonal-image-carousel")).not.toBeInTheDocument();
    expect(container.querySelector('a[href*="trend"], a[href*="scraper"], a[href*="shopping"]')).toBeNull();
  });

  it("shows a prominent entry into Pregled poslovanja before the business cards", async () => {
    renderHome();
    await settled();

    const cta = screen.getByRole("link", { name: /Otvori pregled poslovanja/ });
    expect(cta).toHaveAttribute("href", "/analytics");
    const cards = screen.getByTestId("home-business-cards");
    expect(cta.compareDocumentPosition(cards) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("lists six business cards in the agreed order, each answering a question", async () => {
    renderHome();
    await settled();

    const cards = within(screen.getByTestId("home-business-cards")).getAllByRole("link");
    expect(cards.map((card) => [card.getAttribute("href"), card.querySelector("span")?.textContent])).toEqual([
      ["/analytics", "Pregled poslovanja"],
      ["/analytics/decision-pulse", "Puls odluka"],
      ["/analytics/inventory", "Analitika zaliha"],
      ["/analytics/supplier", "Prodaja po dobavljačima"],
      ["/analytics/pre-nivelacija-prioriteti", "Prioriteti nivelacije"],
      ["/analytics/data-quality", "Kvalitet podataka"],
    ]);
    for (const card of cards) {
      expect(card.textContent).toContain("?");
    }
  });

  it("offers the five operational actions after the business cards", async () => {
    renderHome();
    await settled();

    const ops = screen.getByRole("region", { name: "Operativne akcije" });
    expect(within(ops).getAllByRole("link").map((link) => [link.getAttribute("href"), link.querySelector("h3")?.textContent])).toEqual([
      ["/prodaja", "Prodaja"],
      ["/unos-robe", "Unos robe"],
      ["/transfers", "Prenosi"],
      ["/nivelacija", "Nivelacija"],
      ["/povracaj", "Povraćaj dobavljaču"],
    ]);
  });

  it("has no duplicate links except the primary CTA and the data-quality fallback", async () => {
    refreshStatusMock.mockResolvedValue(refreshStatus({ observedSalesPeriodToUtc: null }));
    renderHome();
    await settled();

    const hrefs = screen.getAllByRole("link").map((link) => link.getAttribute("href"));
    const counts = hrefs.reduce<Record<string, number>>((acc, href) => ({ ...acc, [href ?? ""]: (acc[href ?? ""] ?? 0) + 1 }), {});
    expect(Object.entries(counts).filter(([, count]) => count > 1).map(([href]) => href).sort()).toEqual([
      "/analytics",
      "/analytics/data-quality",
    ]);
    expect(counts["/analytics"]).toBe(2);
    expect(counts["/analytics/data-quality"]).toBe(2);
  });

  it("shows the confirmed sales period, import time and fresh state without claiming store or scope coverage", async () => {
    renderHome();
    await settled();

    const note = screen.getByTestId("home-freshness");
    expect(note).toHaveTextContent("02.01.2025. – 14.10.2026.");
    expect(note).toHaveTextContent("ne po radnji ili izvoru podataka");
    expect(note).toHaveTextContent("najraniji – najkasniji datum računa sa stavkama");
    expect(note).toHaveTextContent("ne potvrđuje da su svi dani i radnje potpuni");
    expect(note).not.toHaveTextContent(/potvrđeni period/i);
    expect(note).toHaveTextContent(/Poslednji uspešan uvoz: 15\.\s?10\.\s?2026\./);
    expect(screen.getByTestId("home-freshness-status")).toHaveTextContent("Uvoz podataka je svež");
    expect(refreshStatusMock).toHaveBeenCalledTimes(1);
  });

  it.each([
    ["stale", "Podaci su zastareli"],
    ["critical", "kritično zastareli"],
    ["unknown", "Svežina podataka nije potvrđena"],
    ["ok", "Svežina podataka nije potvrđena"],
  ])("labels freshness status %s without turning it positive", async (status, expected) => {
    refreshStatusMock.mockResolvedValue(refreshStatus({ dataFreshnessStatus: status }));
    renderHome();
    await settled();

    expect(screen.getByTestId("home-freshness-status")).toHaveTextContent(expected);
    expect(screen.getByTestId("home-freshness-status")).not.toHaveTextContent("je svež");
  });

  it("keeps stale data visible as stale while the backend is online", async () => {
    refreshStatusMock.mockResolvedValue(refreshStatus({ dataFreshnessStatus: "stale" }));
    renderHome();
    await settled();

    expect(screen.getByTestId("home-backend-status")).toHaveTextContent("ONLINE");
    expect(screen.getByTestId("home-freshness-status")).toHaveTextContent("zastareli");
  });

  it.each([
    ["missing period", refreshStatus({ observedSalesPeriodFromUtc: null, observedSalesPeriodToUtc: null, lastSuccessfulImportAtUtc: null, dataFreshnessStatus: "unknown" })],
    ["partial period", refreshStatus({ observedSalesPeriodToUtc: null })],
    ["invalid dates", refreshStatus({ observedSalesPeriodFromUtc: "not-a-date", observedSalesPeriodToUtc: "" })],
  ])("falls back honestly for a %s", async (_label, payload) => {
    refreshStatusMock.mockResolvedValue(payload);
    renderHome();
    await settled();

    const note = screen.getByTestId("home-freshness");
    expect(note).toHaveTextContent("Raspon zabeležene Access prodaje nije dostupan");
    expect(within(note).getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute("href", "/analytics/data-quality");
  });

  it.each([
    ["error", new Error("500")],
    ["timeout", new DOMException("The operation timed out.", "TimeoutError")],
  ])("shows the fallback and invents no numbers on refresh-status %s", async (_label, reason) => {
    refreshStatusMock.mockRejectedValue(reason);
    const { container } = renderHome();
    await settled();

    expect(screen.getByTestId("home-freshness")).toHaveTextContent("Raspon zabeležene Access prodaje nije dostupan");
    expect(screen.getByTestId("home-freshness-status")).toHaveTextContent("nije moguće proveriti");
    expect(container.textContent ?? "").not.toMatch(/\d/);
  });

  it("shows a loading state before the refresh status resolves", () => {
    refreshStatusMock.mockReturnValue(new Promise(() => {}));
    renderHome();

    expect(screen.getByTestId("home-freshness")).toHaveTextContent("Proveravam");
    expect(screen.queryByText(/Poslednji uspešan uvoz/)).not.toBeInTheDocument();
  });

  it("loads refresh status once and does not poll", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderHome();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(10 * 60 * 1000);
    });

    expect(refreshStatusMock).toHaveBeenCalledTimes(1);
  });

  it("keeps all links keyboard reachable", async () => {
    renderHome();
    await settled();

    for (const link of screen.getAllByRole("link")) {
      expect(link).not.toHaveAttribute("tabindex", "-1");
      link.focus();
      expect(link).toHaveFocus();
    }
  });

  it.each([
    ["initial probe", { online: false, checking: true, lastCheckedAt: null }, "PROVERA", "status"],
    ["recovering", { online: false, checking: true, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "BUDI SE", "status"],
    ["online", { online: true, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "ONLINE", "status"],
    ["offline", { online: false, checking: false, lastCheckedAt: new Date("2026-10-14T10:00:00Z") }, "OFFLINE", "alert"],
  ])("shows the real backend status while %s", async (_label, status, expected, role) => {
    backendStatus.current = status;
    renderHome();
    await settled();

    const badge = screen.getByTestId("home-backend-status");
    expect(badge).toHaveTextContent("Backend status");
    expect(badge).toHaveTextContent(expected);
    expect(badge).toHaveAttribute("role", role);
    if (expected === "OFFLINE") {
      expect(badge).toHaveTextContent("Podaci se ne mogu učitati");
    }
    expect(screen.getByRole("link", { name: /Otvori pregled poslovanja/ })).toBeInTheDocument();
  });
});
