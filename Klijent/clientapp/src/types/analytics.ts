export interface SalesSummary {
  totalRevenue: number;
  totalTransactions: number;
  totalUnits: number;
  avgBasketValue: number;
  avgItemPrice: number;
}

export interface TopProduct {
  productId: number;
  productName: string;
  totalRevenue: number;
  totalUnits: number;
  velicina?: string | null;  // Veli?ina cipela
  boja?: string | null;      // Boja cipela
}

export interface TopProductsResult {
  byRevenue: TopProduct[];
  byUnits: TopProduct[];
}

export interface InventoryStatus {
  totalSkuCount: number;
  totalOnHand: number;
  lowStockCount: number;
  outOfStockCount: number;
}
