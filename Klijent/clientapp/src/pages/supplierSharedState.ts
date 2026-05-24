export type SupplierPeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";

export type SupplierTab = "overview" | "scorecard" | "assortment";

export const SUPPLIER_TABS: SupplierTab[] = ["overview", "scorecard", "assortment"];

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
  onTrustMetadataChange?: (payload: SupplierTrustHeaderPayload | null) => void;
};

export type SupplierTrustHeaderPayload = {
  periodFrom?: string | null;
  periodTo?: string | null;
  lastRefreshAt?: string | null;
  dataFreshnessStatus?: "fresh" | "stale" | "critical" | "unknown" | string | null;
  refreshIsRunning?: boolean;
  refreshCurrentStep?: string | null;
  dataSource?: string | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  dataQualitySummary?: {
    missingSupplierCount?: number | null;
    missingCostCount?: number | null;
    missingCategoryCount?: number | null;
    insufficientSignalCount?: number | null;
    ignoredRowsCount?: number | null;
  };
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  recommendationAllowed?: boolean | null;
  recommendationNote?: string;
  emptyStateReason?: string | null;
};
