import type { InventoryInsightItem, InventoryListItem, StoreOption, SupplierFilterOption } from "../../types/analytics";

export type InventoryRow = InventoryListItem & {
  supplierName: string;
  storeName: string;
  quantity: number;
  minimum: number;
  reorderGap: number;
  stockState: "critical" | "warning" | "healthy";
  stockStateLabel: string;
  estimatedValueAmount: number;
  unitCost: number;
  coverageRatio: number | null;
};

export type BuildInventoryRowFn = (item: InventoryListItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) => InventoryRow;
export type BuildRowFromInsightItemFn = (item: InventoryInsightItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) => InventoryRow;
