import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { InventoryAlertListDto } from "../../types/analytics";
import { InventoryAlertsFeed } from "./InventoryAlertsFeed";

function buildAlerts(sizeCode: string | null, storeId = 7): InventoryAlertListDto {
  return {
    generatedAtUtc: "2026-09-23T12:00:00Z",
    totalCount: 1,
    returnedCount: 1,
    totalMatchingCount: 1,
    isTruncated: false,
    snapshotAvailable: true,
    snapshotFreshnessStatus: "unknown",
    warning: null,
    items: [{
      alertType: "inventory_missing",
      skuId: 101,
      storeId,
      sizeCode,
      severity: "warning",
      title: "Nedostajuća veličina",
      message: "Veličina nije dostupna.",
      confidenceScore: 0.8,
      actionability: {
        status: "actionable",
        recommendationAllowed: true,
        dataQualityStatus: "good",
        reasonLabel: "Dovoljno dokaza.",
      },
    }],
  };
}

function renderFeed(alerts: InventoryAlertListDto, onOpenSizeCurve: (skuId: number, storeId: number, sizeCode?: string | null) => void) {
  return render(
    <InventoryAlertsFeed
      alerts={alerts}
      alertsLoading={false}
      alertSeverityFilter=""
      onSeverityFilterChange={vi.fn()}
      displayCount={5}
      onOpenSizeCurve={onOpenSizeCurve}
      onOpenDetail={vi.fn()}
    />,
  );
}

describe("InventoryAlertsFeed size-curve identity", () => {
  it("forwards SKU, store and size identity from a size-specific alert", () => {
    const onOpenSizeCurve = vi.fn();
    renderFeed(buildAlerts("42"), onOpenSizeCurve);

    fireEvent.click(screen.getByRole("button", { name: /Otvori raspodelu veličina za SKU 101 veličinu 42 u prodavnici 7/i }));

    expect(onOpenSizeCurve).toHaveBeenCalledWith(101, 7, "42");
  });

  it("retains store identity and explicit aggregate size absence", () => {
    const onOpenSizeCurve = vi.fn();
    renderFeed(buildAlerts(null, 8), onOpenSizeCurve);

    fireEvent.click(screen.getByRole("button", { name: /Otvori raspodelu veličina za SKU 101 u prodavnici 8/i }));

    expect(onOpenSizeCurve).toHaveBeenCalledWith(101, 8, null);
  });

  it("renders server-filtered alert items and matching count badge", () => {
    renderFeed({
      ...buildAlerts("42"),
      returnedCount: 1,
      totalMatchingCount: 1,
    }, vi.fn());

    expect(screen.getByText("Prikazano 1 alerta")).toBeInTheDocument();
    expect(screen.getByText("Nedostajuća veličina")).toBeInTheDocument();
  });
});
