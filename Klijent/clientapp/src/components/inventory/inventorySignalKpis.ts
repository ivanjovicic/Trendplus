import type { InventoryRow } from "./types";

export type InventorySignalKpiScope = "filter" | "page";

export type InventorySignalKpiSnapshot = {
  stockCoverRiskCount: number;
  lowCoverSkus: number;
  slowStockSkus: number;
  goodSellThroughSkus: number;
  scope: InventorySignalKpiScope;
};

export const INVENTORY_SIGNAL_KPI_PAGE_SCOPE_NOTE =
  "Signal KPI brojevi ispod odnose se samo na artikle prikazane na trenutnoj stranici.";

function normalizeStatus(value: string | null | undefined): string {
  return (value ?? "").trim().toLowerCase();
}

function isStockCoverRiskStatus(status: string): boolean {
  return status === "low_cover"
    || status === "low"
    || status === "out_of_stock_risk"
    || status === "insufficient_data";
}

function isLowCoverStatus(status: string): boolean {
  return status === "low_cover" || status === "low" || status === "out_of_stock_risk";
}

function isSlowStockStatus(status: string): boolean {
  return status === "slow_stock" || status === "slow" || status === "no_velocity";
}

export function computeInventorySignalKpis(
  rows: InventoryRow[],
  totalCount: number,
  pageSize: number,
): InventorySignalKpiSnapshot {
  const lowCoverSkus = rows.filter((row) => isLowCoverStatus(normalizeStatus(row.stockCoverStatus))).length;
  const slowStockSkus = rows.filter((row) => isSlowStockStatus(normalizeStatus(row.stockCoverStatus))).length;
  const goodSellThroughSkus = rows.filter((row) => normalizeStatus(row.sellThroughStatus) === "good").length;
  const stockCoverRiskCount = rows.filter((row) => isStockCoverRiskStatus(normalizeStatus(row.stockCoverStatus))).length;

  return {
    stockCoverRiskCount,
    lowCoverSkus,
    slowStockSkus,
    goodSellThroughSkus,
    scope: totalCount > pageSize ? "page" : "filter",
  };
}
