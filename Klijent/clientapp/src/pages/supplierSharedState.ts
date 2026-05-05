export type SupplierPeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";

export type SupplierCanonicalFilters = {
  periodPreset: SupplierPeriodPreset;
  fromDate: string;
  toDate: string;
  dataScope: string;
  storeId: number | null;
  supplierId: number | null;
};

export type SupplierEmbeddedPageProps = {
  embedded?: boolean;
  sharedFilters?: SupplierCanonicalFilters;
};
