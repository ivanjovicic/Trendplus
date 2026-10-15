import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ProductDecisionCenterPage from "../ProductDecisionCenterPage";

vi.mock("react-router-dom", async () => {
  return {
    Link: ({ children }: { children: ReactNode }) => <a>{children}</a>,
  };
});

const getStoresMock = vi.fn();
const getSupplierFiltersMock = vi.fn();
const getProductDecisionCenterMock = vi.fn();
const getAnalyticsActionSourceStatusesMock = vi.fn();
const upsertAnalyticsActionWithResultMock = vi.fn();

vi.mock("../../services/analyticsApi", () => ({
  AnalyticsMetaError: class extends Error {},
  getStores: (...args: unknown[]) => getStoresMock(...args),
  getSupplierFilters: (...args: unknown[]) => getSupplierFiltersMock(...args),
  getProductDecisionCenter: (...args: unknown[]) => getProductDecisionCenterMock(...args),
  getProductDecisionTimelineExportCsv: vi.fn(),
  getAnalyticsActionSourceStatuses: (...args: unknown[]) => getAnalyticsActionSourceStatusesMock(...args),
  upsertAnalyticsActionWithResult: (...args: unknown[]) => upsertAnalyticsActionWithResultMock(...args),
}));

vi.mock("../../components/analytics/AnalyticsTrustHeader", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsTableToolbar", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsEmptyState", () => ({ default: () => null }));
vi.mock("../../components/analytics/AnalyticsErrorState", () => ({
  default: ({
    title,
    message,
  }: {
    title: string;
    message: string;
  }) => (
    <div role="alert">
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
  ),
}));
vi.mock("../../components/analytics/KpiExplainButton", () => ({ default: () => null }));
vi.mock("../../components/ui/InfoTip", () => ({ default: () => null }));

describe("ProductDecisionCenterPage queue status sync", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    window.history.replaceState({}, "", "/");

    getStoresMock.mockResolvedValue([]);
    getSupplierFiltersMock.mockResolvedValue([]);
    getAnalyticsActionSourceStatusesMock.mockResolvedValue({ items: [] });
    upsertAnalyticsActionWithResultMock.mockResolvedValue({
      item: { id: 1, sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all" },
      created: true,
      existing: false,
      status: "new",
      sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all",
    });
    getProductDecisionCenterMock.mockResolvedValue({
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
          inputFreshnessStatus: "fresh",
          dataQualityStatus: "good",
          stockCoverDays: 5,
          stockCoverStatus: "low_cover",
          sellThroughRatio: 0.6,
          sellThroughStatus: "good",
        },
      ],
      summary: {
        lostSalesEstimate: 25000,
        slowStockCapital: 0,
      },
      totalRows: 1,
      generatedAtUtc: "2026-05-26T12:00:00Z",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      meta: {
        success: true,
        dataQualityStatus: "good",
      },
    });
  });


  it("uses batch source status endpoint for visible rows", async () => {
    const { unmount } = render(<ProductDecisionCenterPage />);

    await waitFor(() => {
      expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalled();
    });

    expect(getAnalyticsActionSourceStatusesMock).toHaveBeenCalledWith(
      expect.objectContaining({
        items: expect.arrayContaining([
          expect.objectContaining({ sourceType: "product" }),
        ]),
      }),
    );

    unmount();
  });

  it("sends search to the backend before the top-row limit is applied", async () => {
    const { unmount } = render(<ProductDecisionCenterPage />);

    const input = await screen.findByPlaceholderText("npr. Air, 45123...");
    fireEvent.change(input, { target: { value: "T" } });
    fireEvent.change(input, { target: { value: "TAIL-SKU" } });

    await waitFor(() => {
      expect(getProductDecisionCenterMock).toHaveBeenCalledWith(
        expect.objectContaining({ search: "TAIL-SKU", top: 500 }),
      );
    });

    expect(getProductDecisionCenterMock.mock.calls.filter(([options]) => options?.search === "T")).toHaveLength(0);
    expect(getProductDecisionCenterMock.mock.calls.filter(([options]) => options?.search === "TAIL-SKU")).toHaveLength(1);

    unmount();
  });

  it("forwards the global data scope to products and supplier filters, then reloads on a scope change", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    const { unmount } = render(<ProductDecisionCenterPage />);

    await waitFor(() => {
      expect(getProductDecisionCenterMock).toHaveBeenCalledWith(
        expect.objectContaining({ dataScope: "imported", top: 500 }),
      );
      expect(getSupplierFiltersMock).toHaveBeenCalledWith(
        expect.any(String),
        expect.any(String),
        true,
        null,
        "imported",
      );
    });

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(getProductDecisionCenterMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ dataScope: "existing", top: 500 }),
      );
      expect(getSupplierFiltersMock).toHaveBeenLastCalledWith(
        expect.any(String),
        expect.any(String),
        true,
        null,
        "existing",
      );
    });

    unmount();
  });

  it("blocks retained supplier options when their refresh fails after a scope change", async () => {
    getSupplierFiltersMock.mockResolvedValue([
      { supplierId: 101, supplierName: "Dobavljač A" },
    ]);
    const { unmount } = render(<ProductDecisionCenterPage />);

    const supplierSelect = await screen.findByRole("combobox", { name: "Dobavljač" });
    await waitFor(() => {
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeInTheDocument();
    });

    getSupplierFiltersMock.mockRejectedValueOnce(new Error("supplier filters unavailable"));
    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(supplierSelect).toBeDisabled();
      expect(screen.getByText(/Lista dobavljača nije osvežena/i)).toBeInTheDocument();
      expect(screen.getByText(/Lista dobavljača je zastarela/i)).toBeInTheDocument();
      expect(screen.getByRole("option", { name: "Dobavljač A" })).toBeDisabled();
    });

    unmount();
  });

  it("clears the dependent supplier selection before loading the newly selected global scope", async () => {
    getSupplierFiltersMock.mockResolvedValue([
      { supplierId: 101, supplierName: "Dobavljač A" },
    ]);
    const { unmount } = render(<ProductDecisionCenterPage />);

    const supplierSelect = await screen.findByRole("combobox", { name: "Dobavljač" });
    await screen.findByRole("option", { name: "Dobavljač A" });
    fireEvent.change(supplierSelect, { target: { value: "101" } });

    await waitFor(() => {
      expect(getProductDecisionCenterMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ supplierId: 101 }),
      );
    });

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(supplierSelect).toHaveValue("");
      expect(getProductDecisionCenterMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ dataScope: "existing", supplierId: null }),
      );
    });

    unmount();
  });

  it("keeps explicit UI labels but does not rewrite backend reason or action text", async () => {
    getProductDecisionCenterMock.mockResolvedValue({
      rows: [
        {
          productId: 202,
          productName: "Model Y",
          sku: "SKU-202",
          category: "Sneakers",
          tipObuce: "Patike",
          supplierName: "Supplier B",
          supplierId: 88,
          revenue: 95000,
          unitsSold: 22,
          velocityUnitsPerDay: 0.8,
          marginPct: 18,
          marginQualityLabel: "Visok kvalitet",
          marginCoveragePct: 91,
          currentStock: 6,
          minStock: 4,
          stockGap: 0,
          trendPct: 5,
          confidencePct: 82,
          confidenceLevel: "medium",
          confidenceScore: 82,
          reliabilityPct: 70,
          recommendationStatus: "BOOST",
          recommendationLabel: "Pojacaj",
          recommendedAction: "Nastavi pracenje.",
          recommendationReason: "Marza je ispod zeljenog nivoa.",
          reasonCodes: ["poor_margin"],
          warningCodes: [],
          primaryDrivers: ["margin"],
          lostSalesEstimate: 10000,
          expectedImpactRsd: 10000,
          impactWindowDays: 14,
          riskIfIgnored: "Rizik je sporijeg obrta zalihe.",
          explainabilityText: "Marza je ispod zeljenog nivoa.",
          inputFreshnessStatus: "fresh",
          dataQualityStatus: "good",
          stockCoverDays: 7,
          stockCoverStatus: "healthy",
          sellThroughRatio: 0.7,
          sellThroughStatus: "good",
        },
      ],
      summary: {
        lostSalesEstimate: 10000,
        slowStockCapital: 0,
      },
      totalRows: 1,
      generatedAtUtc: "2026-05-26T12:00:00Z",
      periodFromUtc: "2026-04-27",
      periodToUtc: "2026-05-26",
      meta: {
        success: true,
        dataQualityStatus: "good",
      },
    });

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Pojačaj")).toBeInTheDocument();
    expect(screen.queryByText("Pojacaj")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zašto?" })).toHaveAttribute("title", "Marza je ispod zeljenog nivoa.");
    expect(screen.getByText("Nastavi pracenje.")).toBeInTheDocument();
  });
  it("ignores immediate duplicate add-to-queue clicks for the same row", async () => {
    let resolveUpsert: ((value: unknown) => void) | null = null;
    upsertAnalyticsActionWithResultMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveUpsert = resolve;
        }),
    );

    render(<ProductDecisionCenterPage />);

    const addButtons = await screen.findAllByRole("button", { name: "Dodaj u proveru" });
    fireEvent.click(addButtons[0]);
    fireEvent.click(addButtons[0]);

    expect(upsertAnalyticsActionWithResultMock).toHaveBeenCalledTimes(1);
    const upsertInput = upsertAnalyticsActionWithResultMock.mock.calls[0]?.[0] as {
      sourceKey: string;
      metadataJson: string;
    };
    expect(upsertInput.sourceKey).toMatch(/^product:v2:all:/);
    expect(JSON.parse(upsertInput.metadataJson)).toEqual(expect.objectContaining({ dataScope: "all" }));

    resolveUpsert?.({
      item: { id: 1, sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all" },
      created: true,
      existing: false,
      status: "new",
      sourceKey: "product:101:replenish:2026-05-28:2026-06-26:all:all",
    });

    await waitFor(() => {
      expect(screen.getAllByRole("button", { name: "U akcijama" }).length).toBeGreaterThan(0);
    });
  });

  it("shows a permission warning and keeps the recommendation visible when add-to-queue is forbidden", async () => {
    upsertAnalyticsActionWithResultMock.mockRejectedValueOnce(Object.assign(new Error("Unauthorized"), { status: 403 }));

    render(<ProductDecisionCenterPage />);

    const addButtons = await screen.findAllByRole("button", { name: "Dodaj u proveru" });
    fireEvent.click(addButtons[0]);

    expect(await screen.findByText("Nemate dozvolu za izmenu akcija. Preporuke ostaju dostupne za pregled.")).toBeInTheDocument();
    expect(screen.getByText("Model X")).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "U akcijama" })).not.toBeInTheDocument();
  });

  it("keeps product recommendations visible when optional action status lookup fails", async () => {
    getAnalyticsActionSourceStatusesMock.mockRejectedValueOnce(new Error("404 Not Found"));

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByText("Model X")).toBeInTheDocument();
    expect(await screen.findByText("Status akcija trenutno nije dostupan.")).toBeInTheDocument();
    expect(screen.getByText("Dopuni zalihe")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "U akcijama" })).not.toBeInTheDocument();
    expect(screen.queryByText("404 Not Found")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Dodaj u proveru" })).toBeInTheDocument();
  });

  it("keeps blocking error state when the main product decision endpoint fails", async () => {
    getProductDecisionCenterMock.mockRejectedValueOnce(
      new Error("Product Decision Center podaci trenutno nisu dostupni."),
    );

    render(<ProductDecisionCenterPage />);

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByText("Podaci trenutno nisu dostupni")).toBeInTheDocument();
    expect(screen.getByText("Product Decision Center podaci trenutno nisu dostupni.")).toBeInTheDocument();
    expect(screen.queryByText("Status akcija trenutno nije dostupan.")).not.toBeInTheDocument();
    expect(screen.queryByText("Model X")).not.toBeInTheDocument();
  });
  it("isolates Product Decision action status and evidence by data scope", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    let importedSourceKeyFromApi: string | null = null;
    getAnalyticsActionSourceStatusesMock.mockImplementation(async (input: { items: Array<{ sourceType: string; sourceKey: string }> }) => {
      if (importedSourceKeyFromApi == null) {
        importedSourceKeyFromApi = input.items.find((item) => item.sourceKey.startsWith("product:v2:imported:"))?.sourceKey ?? null;
      }
      return {
        items: input.items.map((item) => {
          const isCurrentImportedAction = item.sourceKey === importedSourceKeyFromApi;
          const isUnscopedLegacyAction = item.sourceKey.startsWith("product:") && !item.sourceKey.startsWith("product:v2:");
          return {
            ...item,
            exists: isCurrentImportedAction || isUnscopedLegacyAction,
            actionId: isCurrentImportedAction || isUnscopedLegacyAction ? 41 : null,
            hasEvidenceSnapshot: isCurrentImportedAction,
            evidenceSnapshotCapturedAtUtc: isCurrentImportedAction ? "2026-10-15T10:00:00Z" : null,
            evidenceSnapshotRecommendationId: isCurrentImportedAction ? "imported-recommendation" : null,
          };
        }),
      };
    });
    const latestSourceKey = () => {
      const call = [...getAnalyticsActionSourceStatusesMock.mock.calls]
        .reverse()
        .find(([input]) => input?.items?.length > 0);
      return call?.[0]?.items?.[0]?.sourceKey as string | undefined;
    };

    render(<ProductDecisionCenterPage />);

    await waitFor(() => expect(latestSourceKey()).toBeTruthy());
    const importedSourceKey = latestSourceKey()!;
    expect(importedSourceKey).toMatch(/^product:v2:imported:/);
    expect(await screen.findByRole("button", { name: "U akcijama" })).toBeDisabled();
    fireEvent.click(screen.getByText("Model X"));
    expect((await screen.findByTestId("decision-evidence-snapshot")).textContent).toContain("Snimljen");
    expect(await screen.findByTestId("legacy-product-action-warning")).toBeInTheDocument();

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => {
      expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
        options?.dataScope === "existing" && options?.top === 500)).toBe(true);
    });
    await waitFor(() => expect(latestSourceKey()).not.toBe(importedSourceKey));
    expect(screen.queryByRole("button", { name: "U akcijama" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByText("Model X"));
    expect((await screen.findByTestId("decision-evidence-snapshot")).textContent).toContain("Nije snimljen");
    expect(screen.queryByRole("button", { name: "U akcijama" })).not.toBeInTheDocument();
    expect(screen.getByTestId("legacy-product-action-warning")).toBeInTheDocument();
    const addActionButtons = await screen.findAllByRole("button", { name: "Dodaj u proveru" });
    expect(addActionButtons.some((button) => !button.hasAttribute("disabled"))).toBe(true);
  });

  it("loads and validates selected stores against each Product Decision data scope", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    window.history.replaceState({}, "", "/analytics/products?store=11");
    getStoresMock
      .mockResolvedValueOnce([{ storeId: 11, storeName: "Imported store" }])
      .mockResolvedValueOnce([{ storeId: 22, storeName: "Existing store" }]);

    render(<ProductDecisionCenterPage />);

    await waitFor(() => expect(getStoresMock).toHaveBeenCalledWith(true, "imported"));
    await waitFor(() => {
      expect(getProductDecisionCenterMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ dataScope: "imported", storeId: 11 }),
      );
    });

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => expect(getStoresMock).toHaveBeenLastCalledWith(true, "existing"));
    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: "Prodavnica" })).toHaveValue("");
      expect(screen.getByText(/Prodavnica 11 iz URL-a nije dostupna u izvoru/i)).toBeInTheDocument();
    });
    expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
      options?.dataScope === "existing" && options?.storeId === 11)).toBe(false);
    await waitFor(() => {
      expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
        options?.dataScope === "existing" && options?.storeId === null)).toBe(true);
    });
  });

  it("blocks recommendations when scoped store loading fails and retries the same scope", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    getStoresMock
      .mockResolvedValueOnce([])
      .mockRejectedValueOnce(new Error("store list unavailable"))
      .mockResolvedValueOnce([{ storeId: 22, storeName: "Existing store" }]);

    render(<ProductDecisionCenterPage />);

    await waitFor(() => expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
      options?.dataScope === "imported" && options?.top === 500)).toBe(true));

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));

    await waitFor(() => expect(getStoresMock).toHaveBeenLastCalledWith(true, "existing"));
    expect(await screen.findByRole("button", { name: "Pokušaj ponovo" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Prodavnica" })).toBeDisabled();
    expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
      options?.dataScope === "existing" && options?.top === 500)).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));
    await waitFor(() => expect(getStoresMock).toHaveBeenLastCalledWith(true, "existing"));
    await waitFor(() => expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
      options?.dataScope === "existing" && options?.top === 500)).toBe(true));
    expect(screen.getByRole("combobox", { name: "Prodavnica" })).toBeEnabled();
  });

  it("ignores a late store response after the active data scope changes again", async () => {
    localStorage.setItem("trendplus:dataScope", "imported");
    let resolveExistingStores: ((items: Array<{ storeId: number; storeName: string }>) => void) | null = null;
    getStoresMock.mockImplementation((_useCached: boolean, scope: string) => {
      if (scope === "existing") {
        return new Promise((resolve) => {
          resolveExistingStores = resolve;
        });
      }
      return Promise.resolve([{ storeId: 11, storeName: "Imported store" }]);
    });

    render(<ProductDecisionCenterPage />);

    await waitFor(() => expect(getProductDecisionCenterMock.mock.calls.some(([options]) =>
      options?.dataScope === "imported" && options?.top === 500)).toBe(true));

    localStorage.setItem("trendplus:dataScope", "existing");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    await waitFor(() => expect(getStoresMock).toHaveBeenLastCalledWith(true, "existing"));

    localStorage.setItem("trendplus:dataScope", "imported");
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    await waitFor(() => expect(getStoresMock).toHaveBeenLastCalledWith(true, "imported"));
    await waitFor(() => expect(screen.getByRole("option", { name: "Imported store" })).toBeInTheDocument());

    await act(async () => {
      resolveExistingStores?.([{ storeId: 22, storeName: "Existing store" }]);
      await Promise.resolve();
    });

    expect(screen.getByRole("option", { name: "Imported store" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Existing store" })).not.toBeInTheDocument();
    expect(getProductDecisionCenterMock.mock.calls.at(-1)?.[0]).toEqual(
      expect.objectContaining({ dataScope: "imported", top: 500 }),
    );
  });

});
