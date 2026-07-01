import { describe, expect, it, vi } from "vitest";
import {
  buildAnalyticsDetailSnapshot,
  getAnalyticsDetailSnapshot,
  getPrintPayload,
  resolveAnalyticsTablePayload,
  saveAnalyticsDetailSnapshot,
  savePrintPayload,
} from "../analyticsTableState";

type Row = {
  supplier: string;
  revenue: number;
  active: boolean;
  optional?: string | null;
};

const columns = [
  { key: "supplier", header: "Dobavljač", dataType: "text" as const },
  { key: "revenue", header: "Prihod", dataType: "currency" as const, getValue: (row: Row) => row.revenue },
  { key: "active", header: "Aktivan", dataType: "text" as const },
  { key: "optional", header: "Napomena", dataType: "text" as const },
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
      expect.objectContaining({ key: "revenue", label: "Prihod", value: "120000", highlight: true }),
      expect.objectContaining({ key: "active", label: "Aktivan", value: "Da", highlight: false }),
      expect.objectContaining({ key: "optional", label: "Napomena", value: "", highlight: false }),
    ]);
    expect(snapshot.metadata).toEqual([
      expect.objectContaining({ key: "period", label: "Period", value: "30d", highlight: false }),
    ]);
  });

  it("persists and expires print payloads by TTL", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-07-01T10:00:00Z"));

    const payload = resolveAnalyticsTablePayload({
      tableKey: "supplier-sales",
      tableTitle: "Supplier sales",
      columns,
      rows: [row],
    });
    const key = savePrintPayload(payload);

    expect(getPrintPayload(key)).toEqual(payload);

    vi.setSystemTime(new Date("2026-07-01T10:11:00Z"));
    expect(getPrintPayload(key)).toBeNull();
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
});
