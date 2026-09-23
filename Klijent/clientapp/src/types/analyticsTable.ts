export type AnalyticsScalar = string | number | boolean | null | undefined;
export type AnalyticsDataType = "text" | "number" | "currency" | "percent" | "date" | "datetime";

export interface AnalyticsNamedValue {
  key: string;
  label: string;
  value: AnalyticsScalar;
}

export interface AnalyticsTableColumn<Row> {
  key: string;
  header: string;
  dataType?: AnalyticsDataType;
  formatHint?: string;
  getValue?: (row: Row) => AnalyticsScalar;
  detailLabel?: string;
}

export interface ResolvedAnalyticsTableColumn {
  key: string;
  header: string;
  dataType?: string;
  formatHint?: string;
}

export interface ResolvedAnalyticsTablePayload {
  tableKey: string;
  tableTitle: string;
  columns: ResolvedAnalyticsTableColumn[];
  rows: Array<Record<string, AnalyticsScalar>>;
  filters: AnalyticsNamedValue[];
  metadata: AnalyticsNamedValue[];
  methodologyMetricKeys?: string[];
  locale?: string;
  documentType?: string;
  templateName?: string;
  templateVersion?: number;
}

export interface AnalyticsDetailField {
  key: string;
  label: string;
  value?: string | null;
  dataType?: string | null;
  highlight?: boolean;
}

export interface AnalyticsDetailResponse {
  table: string;
  recordId: string;
  title: string;
  subtitle?: string | null;
  fields: AnalyticsDetailField[];
  metadata: AnalyticsDetailField[];
  recommendation?: AnalyticsDetailRecommendation | null;
  provenance?: AnalyticsDetailProvenance | null;
}

export interface AnalyticsDetailRecommendation {
  status: string;
  label: string;
  summary: string;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  dataQualityStatus: string;
  recommendationAllowed: boolean;
  reasonCodes: string[];
}

export interface AnalyticsDetailProvenance {
  requestedFromUtc?: string | null;
  requestedToUtc?: string | null;
  effectiveFromUtc?: string | null;
  effectiveToUtc?: string | null;
  season?: string | null;
  storeId?: number | null;
  dataScope: string;
  generatedAtUtc: string;
  freshness: string;
  dataQualityStatus: string;
  snapshotActive: boolean;
  snapshotGeneratedAtUtc?: string | null;
  fallbackApplied: boolean;
  recommendationAllowed: boolean;
  sourceFamily?: string | null;
  sourceLabel?: string | null;
  sourceTables?: string | null;
  observedPopulation?: string | null;
  costPolicy?: string | null;
  prePostPolicy?: string | null;
}
