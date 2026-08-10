import type { InventoryInsightItem, InventoryListItem, StoreOption, SupplierFilterOption } from "../../types/analytics";

export type InventoryRow = InventoryListItem & {
  supplierName: string;
  storeName: string;
  quantity: number;
  minimum: number;
  reorderGap: number;
  stockState: "critical" | "warning" | "healthy";
  stockStateLabel: string;
  estimatedValueAmount: number | null;
  unitCost: number | null;
  coverageRatio: number | null;
  stockCoverDays: number | null;
  stockCoverStatus: string;
  stockCoverStatusLabel: string;
  sellThroughRatio: number | null;
  sellThroughStatus: string;
  sellThroughStatusLabel: string;
  signalConfidencePct: number | null;
  recommendationAllowed: boolean | null;
  signalText: string;
  dataQualityStatus: string;
  reasonCodes: string[];
  contextStatus?: "loadingContext" | "contextMissing" | null;
};

export type BuildInventoryRowFn = (item: InventoryListItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) => InventoryRow;
export type BuildRowFromInsightItemFn = (item: InventoryInsightItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) => InventoryRow;
