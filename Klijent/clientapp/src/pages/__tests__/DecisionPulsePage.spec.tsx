import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, useNavigate } from "react-router-dom";
import * as decisionPulseApi from "../../services/decisionPulseApi";
import DecisionPulsePage from "../DecisionPulsePage";

describe("DecisionPulsePage", () => {
  beforeEach(() => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 2,
      items: [],
      meta: {
        success: true,
        emptyReason: "no_pulse_items",
        message: "Nema actionable Pulse stavki.",
      },
    });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it("renders empty as non-error and does not invent KPI zeros", async () => {
    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { level: 1, name: "Puls odluka" })).toBeInTheDocument();
    expect(await screen.findByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Dostupnost izvora: Izvori su dostupni");
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Obuhvat: Podaci dostupni u ovoj bazi");
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Izostavljeno: 2");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText(/0 RSD/i)).not.toBeInTheDocument();
  });

  it("labels a stale digest by its observed horizon and keeps unknown impact unavailable", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      currentness: "latest_known",
      asOfUtc: "2026-08-05T00:00:00Z",
      items: [{
        id: "product:1",
        sourceType: "product",
        sourceKey: "1",
        title: "Proveri artikl",
        whySummary: "Poslednji pouzdan signal traži proveru.",
        reasonCodes: [],
        recommendationStatus: "WATCH",
        recommendationLabel: "Proveri",
        dataQualityStatus: "good",
        inputFreshnessStatus: "stale",
        deepLink: "/analytics/products?storeId=4&dataScope=imported",
        generatedAtUtc: "2026-08-20T12:00:00Z",
        tenantScope: "n/a_dedicated",
        asOfUtc: "2026-08-05T00:00:00Z",
        evidenceBasis: "product_decision_period",
        expectedImpactRsd: null,
        priorityEvidence: null,
      }],
      meta: { success: true },
    });
    vi.spyOn(decisionPulseApi, "getDecisionPulseDispositions").mockResolvedValue({});
    const saveDisposition = vi.spyOn(decisionPulseApi, "recordDecisionPulseDisposition").mockResolvedValue();

    render(<MemoryRouter initialEntries={["/analytics/decision-pulse?storeId=4&dataScope=imported"]}><DecisionPulsePage /></MemoryRouter>);

    expect(await screen.findByTestId("decision-pulse-currentness")).toHaveTextContent("Pregled odluka prema stanju do");
    expect(screen.getByText("Očekivani uticaj: nije dostupan")).toBeInTheDocument();
    expect(screen.getByText("Izvor: Odluka o proizvodu")).toBeInTheDocument();
    expect(screen.getByText("Svežina signala: Zastarelo")).toBeInTheDocument();
    expect(screen.getByText("Osnova: Period Product Decision signala")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Otvori odluku" })).toHaveAttribute(
      "href",
      "/analytics/products?storeId=4&dataScope=imported",
    );
    fireEvent.click(screen.getByRole("button", { name: "Prihvaćeno" }));
    await waitFor(() => expect(saveDisposition).toHaveBeenCalledWith(
      expect.objectContaining({ id: "product:1", expectedImpactRsd: null }),
      "accepted",
      expect.objectContaining({ storeId: 4, dataScope: "imported" }),
    ));
  });

  it("forwards the shared period and scope from the URL to the feed request", async () => {
    const getFeed = vi.spyOn(decisionPulseApi, "getDecisionPulse");
    render(
      <MemoryRouter initialEntries={["/analytics/decision-pulse?fromDate=2026-08-01&toDate=2026-08-20&storeId=4&supplierId=7&dataScope=imported"]}>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    await waitFor(() => expect(getFeed).toHaveBeenCalledWith({
      fromDate: "2026-08-01",
      toDate: "2026-08-20",
      storeId: 4,
      supplierId: 7,
      dataScope: "imported",
    }));
  });

  it.each(["storeId=abc", "storeId=0", "storeId=-3", "storeId=2&storeId=3", "supplierId=bad", "supplierId=0"])(
    "does not widen invalid URL filter %s to all records",
    async (query) => {
      const fetchFeed = vi.spyOn(decisionPulseApi, "getDecisionPulse");
      render(<MemoryRouter initialEntries={["/analytics/decision-pulse?" + query]}><DecisionPulsePage /></MemoryRouter>);
      const alert = await screen.findByRole("alert");
      expect(alert).toHaveTextContent("Pregled nije proširen");
      expect(fetchFeed).not.toHaveBeenCalled();
    },
  );

  it("clears old items on filter changes and ignores an older in-flight response", async () => {
    let resolveFirst!: (value: Awaited<ReturnType<typeof decisionPulseApi.getDecisionPulse>>) => void;
    let resolveSecond!: (value: Awaited<ReturnType<typeof decisionPulseApi.getDecisionPulse>>) => void;
    const first = new Promise<Awaited<ReturnType<typeof decisionPulseApi.getDecisionPulse>>>((resolve) => { resolveFirst = resolve; });
    const second = new Promise<Awaited<ReturnType<typeof decisionPulseApi.getDecisionPulse>>>((resolve) => { resolveSecond = resolve; });
    const getFeed = vi.spyOn(decisionPulseApi, "getDecisionPulse").mockReturnValueOnce(first).mockReturnValueOnce(second);
    function ChangePeriod() {
      const navigate = useNavigate();
      return <button onClick={() => navigate("?fromDate=2026-08-02&toDate=2026-08-20")}>Promeni period</button>;
    }
    const makeResponse = (id: string) => ({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      items: [{
        id, sourceType: "product", sourceKey: id, title: id, whySummary: "Provera", reasonCodes: [],
        recommendationStatus: "WATCH", recommendationLabel: "Proveri", dataQualityStatus: "good",
        inputFreshnessStatus: "fresh", deepLink: "/analytics/products", generatedAtUtc: "2026-08-20T12:00:00Z",
        tenantScope: "n/a_dedicated",
      }],
      meta: { success: true },
    }) as Awaited<ReturnType<typeof decisionPulseApi.getDecisionPulse>>;

    render(
      <MemoryRouter initialEntries={["/analytics/decision-pulse?fromDate=2026-08-01&toDate=2026-08-20"]}>
        <ChangePeriod />
        <DecisionPulsePage />
      </MemoryRouter>,
    );
    await waitFor(() => expect(getFeed).toHaveBeenCalledTimes(1));
    fireEvent.click(screen.getByRole("button", { name: "Promeni period" }));
    await waitFor(() => expect(getFeed).toHaveBeenCalledTimes(2));
    resolveSecond(makeResponse("new-period"));
    expect(await screen.findByText("new-period")).toBeInTheDocument();
    resolveFirst(makeResponse("old-period"));
    await waitFor(() => expect(screen.queryByText("old-period")).not.toBeInTheDocument());
    expect(screen.getByText("new-period")).toBeInTheDocument();
  });

  it("uses safe Serbian fallbacks for unknown trust and scope codes", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "private-tenant-code",
      suppressedCount: 0,
      items: [{
        id: "product:unknown",
        sourceType: "internal-source-code",
        sourceKey: "1",
        title: "Proveri izvor",
        whySummary: "Poreklo signala traži proveru.",
        reasonCodes: [],
        recommendationStatus: "WATCH",
        recommendationLabel: "Proveri",
        dataQualityStatus: "vendor-quality-code",
        inputFreshnessStatus: "vendor-freshness-code",
        deepLink: "/analytics/products",
        generatedAtUtc: "2026-08-20T12:00:00Z",
        tenantScope: "private-tenant-code",
      }],
      meta: { success: true },
    });

    render(<MemoryRouter initialEntries={["/analytics/decision-pulse?dataScope=private-scope-code"]}><DecisionPulsePage /></MemoryRouter>);

    expect(await screen.findByText("Izvor: Analitika")).toBeInTheDocument();
    expect(screen.getByText("Kvalitet dokaza: Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.getByText("Svežina signala: Nije potvrđeno")).toBeInTheDocument();
    const provenance = screen.getByTestId("decision-pulse-feed-provenance");
    expect(provenance).toHaveTextContent("Obuhvat: Obuhvat nije potvrđen");
    expect(provenance).toHaveTextContent("Opseg podataka: Opseg nije potvrđen");
    expect(provenance).not.toHaveTextContent(/private-tenant-code|private-scope-code/);
  });

  it("does not render raw metadata when the source fails", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      items: [],
      meta: {
        success: false,
        errorCode: "source_error",
        errorMessage: "SqlException: password=super-secret; connection_string=private",
      },
    });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Podaci trenutno nisu dostupni");
    expect(alert).not.toHaveTextContent(/SqlException|super-secret|connection_string/i);
  });

  it("sanitizes a rejected request error", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockRejectedValue(
      new Error("Decision Pulse HTTP 500: NpgsqlException at Database.Query()"),
    );

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Pregled odluka trenutno nije dostupan.");
    expect(alert).not.toHaveTextContent(/HTTP 500|NpgsqlException|Database\.Query/i);
  });

  it("does not try to render malformed empty metadata", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 0,
      items: [],
      meta: {
        success: true,
        emptyReason: "no_pulse_items",
        message: { unexpected: "object" } as never,
      },
    });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
    expect(screen.queryByText("[object Object]")).not.toBeInTheDocument();
  });

  it("shows a partial warning and retry for an empty partial feed", async () => {
    const getDecisionPulse = vi.spyOn(decisionPulseApi, "getDecisionPulse")
      .mockResolvedValueOnce({
        generatedAtUtc: "2026-08-20T12:00:00Z",
        periodFromUtc: null,
        periodToUtc: null,
        tenantScope: "n/a_dedicated",
        suppressedCount: 124,
        items: [],
        meta: {
          success: true,
          isPartial: true,
          warningCode: "PULSE_PARTIAL",
          warningMessage: "Supplier decision hub nije dostupan.",
        },
      })
      .mockResolvedValueOnce({
        generatedAtUtc: "2026-08-20T12:00:00Z",
        periodFromUtc: null,
        periodToUtc: null,
        tenantScope: "n/a_dedicated",
        suppressedCount: 0,
        items: [],
        meta: {
          success: true,
          emptyReason: "no_pulse_items",
          message: "Nema Decision Pulse izuzetaka za period.",
        },
      });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("decision-pulse-partial-warning")).toHaveTextContent("Supplier decision hub nije dostupan.");
    expect(screen.getByTestId("decision-pulse-partial-warning")).toHaveTextContent("delimično dostupan");
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Dostupnost izvora: Izvori su delimično dostupni");
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Izostavljeno: 124");

    fireEvent.click(screen.getByRole("button", { name: "Ponovo učitaj pregled" }));

    await waitFor(() => expect(getDecisionPulse).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByTestId("decision-pulse-partial-warning")).not.toBeInTheDocument());
    expect(screen.getByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
  });

  it("keeps populated items visible while showing the partial warning", async () => {
    vi.spyOn(decisionPulseApi, "getDecisionPulse").mockResolvedValue({
      generatedAtUtc: "2026-08-20T12:00:00Z",
      periodFromUtc: null,
      periodToUtc: null,
      tenantScope: "n/a_dedicated",
      suppressedCount: 3,
      items: [{
        id: "supplier-1",
        sourceType: "supplier",
        sourceKey: "supplier:1",
        title: "Dobavljač zahteva proveru",
        whySummary: "Izvor je delimično dostupan.",
        reasonCodes: ["supplier_partial"],
        recommendationStatus: "review",
        recommendationLabel: "Proveri",
        dataQualityStatus: "warning",
        inputFreshnessStatus: "stale",
        deepLink: "/analytics/supplier",
        generatedAtUtc: "2026-08-20T12:00:00Z",
        tenantScope: "n/a_dedicated",
      }],
      meta: {
        success: true,
        isPartial: true,
        warningCode: "PULSE_PARTIAL",
        warningMessage: "Supplier decision hub nije dostupan.",
      },
    });

    render(
      <MemoryRouter>
        <DecisionPulsePage />
      </MemoryRouter>,
    );

    expect(await screen.findByTestId("decision-pulse-partial-warning")).toBeInTheDocument();
    expect(screen.getByTestId("decision-pulse-feed-provenance")).toHaveTextContent("Izostavljeno: 3");
    expect(screen.getByRole("heading", { name: "Dobavljač zahteva proveru" })).toBeInTheDocument();
    expect(screen.getByText("Izvor je delimično dostupan.")).toBeInTheDocument();
  });

  it("offers retry when the feed request fails", async () => {
    const getDecisionPulse = vi.spyOn(decisionPulseApi, "getDecisionPulse")
      .mockRejectedValueOnce(new Error("temporary failure"))
      .mockResolvedValueOnce({
        generatedAtUtc: "2026-08-20T12:00:00Z",
        periodFromUtc: null,
        periodToUtc: null,
        tenantScope: "n/a_dedicated",
        suppressedCount: 0,
        items: [],
        meta: { success: true, emptyReason: "no_pulse_items" },
      });

    render(<MemoryRouter><DecisionPulsePage /></MemoryRouter>);

    expect(await screen.findByRole("button", { name: "Pokušaj ponovo" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));
    await waitFor(() => expect(getDecisionPulse).toHaveBeenCalledTimes(2));
    expect(await screen.findByText(/Prazan rezultat nije greška/i)).toBeInTheDocument();
  });
});
