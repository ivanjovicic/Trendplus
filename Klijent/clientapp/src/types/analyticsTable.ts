export type AnalyticsScalar = string | number | boolean | null | undefined;

export interface AnalyticsNamedValue {
  key: string;
  label: string;
  value: AnalyticsScalar;
}

export interface AnalyticsTableColumn<Row> {
  key: string;
  header: string;
  dataType?: "text" | "number" | "currency" | "percent" | "date" | "datetime";
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
}
