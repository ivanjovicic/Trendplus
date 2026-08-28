import { describe, expect, it, vi } from "vitest";
import {
  buildAnalyticsDetailSnapshot,
  formatDetailFieldValue,
  getAnalyticsDetailSnapshot,
  getBrowserPreviewPayload,
  getBrowserPreviewSnapshot,
  saveAnalyticsDetailSnapshot,
  saveBrowserPreviewPayload,
  resolveAnalyticsTablePayload,
  ANALYTICS_BROWSER_PREVIEW_TTL_MS,
} from "../analyticsTableState";
import type { AnalyticsTableColumn } from "../../types/analyticsTable";
import { fmtNumber, fmtPct, fmtRsd, formatDate, formatDateTime } from "../../utils/analyticsFormatters";

type Row = {
  supplier: string;
  revenue: number;
  active: boolean;
  optional?: string | null;
};

const columns: AnalyticsTableColumn<Row>[] = [
  { key: "supplier", header: "Dobavljač", dataType: "text" },
  { key: "revenue", header: "Prihod", dataType: "currency", getValue: (row) => row.revenue },
  { key: "active", header: "Aktivan", dataType: "text" },
  { key: "optional", header: "Napomena", dataType: "text" },
];

const row: Row = {
  supplier: "Dobavljač A",
  revenue: 120000,
  active: true,
  optional: null,
};

describe("analyticsTableState", () => {
  it("resolves table payload using only declared columns and getValue functions", () => {
    const payload = resolveAnalyticsTablePayload({
      tableKey: "supplier-sales",
      tableTitle: "Supplier sales",
      columns,
      rows: [{ ...row, ignored: "not exported" } as Row & { ignored: string }],
      filters: [{ key: "period", label: "Period", value: "30d" }],
      metadata: [{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }],
      locale: "sr-RS",
      documentType: "analytics-report",
      templateName: "premium-table",
      templateVersion: 2,
    });

    expect(payload).toEqual({
      tableKey: "supplier-sales",
      tableTitle: "Supplier sales",
      columns: [
        { key: "supplier", header: "Dobavljač", dataType: "text", formatHint: undefined },
        { key: "revenue", header: "Prihod", dataType: "currency", formatHint: undefined },
        { key: "active", header: "Aktivan", dataType: "text", formatHint: undefined },
        { key: "optional", header: "Napomena", dataType: "text", formatHint: undefined },
      ],
      rows: [{ supplier: "Dobavljač A", revenue: 120000, active: true, optional: null }],
      filters: [{ key: "period", label: "Period", value: "30d" }],
      metadata: [{ key: "generatedAt", label: "Generisano", value: "2026-07-01" }],
      methodologyMetricKeys: undefined,
      locale: "sr-RS",
      documentType: "analytics-report",
      templateName: "premium-table",
      templateVersion: 2,
    });
  });

  it("builds detail snapshots with highlighted numeric fields and stringified booleans/nulls", () => {
    const snapshot = buildAnalyticsDetailSnapshot({
      table: "supplier-sales",
      recordId: "7",
      title: "Dobavljač A",
      subtitle: "Detalj dobavljača",
      columns,
      row,
      metadata: [{ key: "period", label: "Period", value: "30d" }],
    });

    expect(snapshot.fields).toEqual([
      expect.objectContaining({ key: "supplier", label: "Dobavljač", value: "Dobavljač A", highlight: false }),
      expect.objectContaining({ key: "revenue", label: "Prihod", value: "120.000 RSD", highlight: true }),
      expect.objectContaining({ key: "active", label: "Aktivan", value: "Da", highlight: false }),
      expect.objectContaining({ key: "optional", label: "Napomena", value: "", highlight: false }),
    ]);
    expect(snapshot.metadata).toEqual([
      expect.objectContaining({ key: "period", label: "Period", value: "30d", highlight: false }),
    ]);
  });

  it("persists and expires browser preview payloads by TTL", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-07-01T10:00:00Z"));

    const payload = resolveAnalyticsTablePayload({
      tableKey: "supplier-sales",
      tableTitle: "Supplier sales",
      columns,
      rows: [row],
    });
    const key = saveBrowserPreviewPayload(payload);
    const snapshot = getBrowserPreviewSnapshot(key);

    expect(getBrowserPreviewPayload(key)).toEqual(payload);
    expect(snapshot?.savedAtUtc).toBe("2026-07-01T10:00:00.000Z");
    expect(snapshot?.ttlMs).toBe(ANALYTICS_BROWSER_PREVIEW_TTL_MS);
    expect(snapshot?.expiresAtUtc).toBe("2026-07-01T10:10:00.000Z");

    vi.setSystemTime(new Date("2026-07-01T10:11:00Z"));
    expect(getBrowserPreviewPayload(key)).toBeNull();
    expect(getBrowserPreviewSnapshot(key)).toBeNull();
    expect(localStorage.getItem(key)).toBeNull();

    vi.useRealTimers();
  });

  it("stores and reads detail snapshots using table and record id", () => {
    const snapshot = buildAnalyticsDetailSnapshot({
      table: "supplier-sales",
      recordId: "7",
      title: "Dobavljač A",
      columns,
      row,
    });

    saveAnalyticsDetailSnapshot(snapshot);

    expect(getAnalyticsDetailSnapshot("supplier-sales", "7")).toEqual(snapshot);
    expect(getAnalyticsDetailSnapshot("supplier-sales", "8")).toBeNull();
  });

  it("returns null for malformed stored detail JSON instead of throwing", () => {
    sessionStorage.setItem("analytics-detail:supplier-sales:bad", "not-json");

    expect(getAnalyticsDetailSnapshot("supplier-sales", "bad")).toBeNull();
  });

  it("formats percent detail fields with percent units via fmtPct", () => {
    type PctRow = { name: string; sharePct: number };
    const pctColumns: AnalyticsTableColumn<PctRow>[] = [
      { key: "name", header: "Naziv", dataType: "text" },
      { key: "sharePct", header: "Udeo %", dataType: "percent" },
    ];

    const snapshot = buildAnalyticsDetailSnapshot({
      table: "pct-fixture",
      recordId: "1",
      title: "A",
      columns: pctColumns,
      row: { name: "A", sharePct: 35 },
    });

    expect(snapshot.fields.find((field) => field.key === "sharePct")).toEqual(
      expect.objectContaining({
        key: "sharePct",
        value: "35,00%",
        dataType: "percent",
        highlight: true,
      }),
    );
  });

  it("formats currency, number, date, datetime and boolean like table display", () => {
    type MixedRow = {
      revenue: number;
      units: number;
      sharePct: number;
      asOf: string;
      refreshedAt: string;
      active: boolean;
    };

    const mixedColumns: AnalyticsTableColumn<MixedRow>[] = [
      { key: "revenue", header: "Prihod", dataType: "currency" },
      { key: "units", header: "Kom", dataType: "number" },
      { key: "sharePct", header: "Udeo %", dataType: "percent" },
      { key: "asOf", header: "Datum", dataType: "date" },
      { key: "refreshedAt", header: "Osveženo", dataType: "datetime" },
      { key: "active", header: "Aktivan", dataType: "text" },
    ];

    const snapshot = buildAnalyticsDetailSnapshot({
      table: "mixed-fixture",
      recordId: "1",
      title: "Mixed",
      columns: mixedColumns,
      row: {
        revenue: 1250.5,
        units: 12,
        sharePct: 35,
        asOf: "2026-03-18",
        refreshedAt: "2026-03-18T10:15:00Z",
        active: false,
      },
    });

    expect(formatDetailFieldValue(1250.5, "currency")).toBe(fmtRsd(1250.5, 0));
    expect(snapshot.fields.find((f) => f.key === "revenue")?.value).toBe(fmtRsd(1250.5, 0));
    expect(snapshot.fields.find((f) => f.key === "units")?.value).toBe(fmtNumber(12, 0));
    expect(snapshot.fields.find((f) => f.key === "sharePct")?.value).toBe(fmtPct(35, 2));
    expect(snapshot.fields.find((f) => f.key === "asOf")?.value).toBe(formatDate("2026-03-18"));
    expect(snapshot.fields.find((f) => f.key === "refreshedAt")?.value).toBe(formatDateTime("2026-03-18T10:15:00Z"));
    expect(snapshot.fields.find((f) => f.key === "active")?.value).toBe("Ne");
    // Must not silently treat ratio 0.35 as percent units for a raw percent column value of 0.35
    expect(formatDetailFieldValue(0.35, "percent")).toBe(fmtPct(0.35, 2));
    expect(formatDetailFieldValue(0.35, "percent")).not.toBe(fmtPct(35, 2));
  });
});
