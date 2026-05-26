import type { InventoryInsightItem, InventoryListItem, InventoryReportScheduleInput, StoreOption, SupplierFilterOption } from "../../types/analytics";
import type { InventoryRow } from "./types";
import { TONE, resolveTone } from "./toneMap";

type InventoryListItemWithSignals = InventoryListItem & {
  stockCoverDays?: number | null;
  stockCoverStatus?: string | null;
  sellThroughRatio?: number | null;
  sellThroughStatus?: string | null;
  signalConfidencePct?: number | null;
  dataQualityStatus?: string | null;
  reasonCodes?: string[] | null;
};

export const WEEKDAY_OPTIONS = [
  { value: 1, label: "Ponedeljak" },
  { value: 2, label: "Utorak" },
  { value: 3, label: "Sreda" },
  { value: 4, label: "Cetvrtak" },
  { value: 5, label: "Petak" },
  { value: 6, label: "Subota" },
  { value: 0, label: "Nedelja" },
];

export function formatNumber(value: number, digits = 0) {
  return value.toLocaleString("sr-RS", { maximumFractionDigits: digits });
}

export function formatCurrency(value: number) {
  return value.toLocaleString("sr-RS", { style: "currency", currency: "RSD", maximumFractionDigits: 0 });
}

export function formatPercent(value: number) {
  return `${value.toLocaleString("sr-RS", { maximumFractionDigits: 1 })}%`;
}

export function formatDateTime(value?: string | null) {
  if (!value) return "Nema podataka";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleString("sr-RS");
}

export function csvEscape(value: string | number | null | undefined) {
  const raw = value == null ? "" : String(value);
  return /[",\n;]/.test(raw) ? `"${raw.replace(/"/g, "\"\"")}"` : raw;
}

export function getStockState(quantity: number, minimum: number) {
  if (quantity <= 0) {
    return {
      key: "critical" as const,
      label: "Bez zaliha",
      badge: TONE.stock.critical,
      panel: TONE.stockPanel.critical,
    };
  }
  if (quantity <= minimum) {
    return {
      key: "warning" as const,
      label: "Niska zaliha",
      badge: TONE.stock.warning,
      panel: TONE.stockPanel.warning,
    };
  }
  return {
    key: "healthy" as const,
    label: "Stabilno",
    badge: TONE.stock.healthy,
    panel: TONE.stockPanel.healthy,
  };
}

export function stockCoverStatusLabel(status: string): string {
  switch ((status ?? "").trim().toLowerCase()) {
    case "low":
      return "Niska pokrivenost";
    case "healthy":
      return "Zdrava pokrivenost";
    case "high":
      return "Visoka pokrivenost";
    case "slow":
      return "Spor obrt";
    case "no_velocity":
      return "Bez rotacije";
    case "out_of_stock_risk":
      return "Rizik rasprodaje";
    default:
      return "Nedovoljno podataka";
  }
}

export function sellThroughStatusLabel(status: string): string {
  switch ((status ?? "").trim().toLowerCase()) {
    case "good":
      return "Dobar sell-through";
    case "warning":
      return "Sell-through upozorenje";
    case "critical":
      return "Kritičan sell-through";
    default:
      return "Nedovoljno podataka";
  }
}

export function buildSignalText(stockCoverStatus: string, sellThroughStatus: string): string {
  const normalizedStockCover = (stockCoverStatus ?? "").trim().toLowerCase();
  const normalizedSellThrough = (sellThroughStatus ?? "").trim().toLowerCase();

  if (normalizedStockCover === "insufficient_data" || normalizedSellThrough === "insufficient_data") {
    return "Nedovoljno podataka";
  }

  if (normalizedStockCover === "out_of_stock_risk" || normalizedStockCover === "low") {
    return "Dopuni";
  }

  if (normalizedStockCover === "slow" || normalizedStockCover === "no_velocity") {
    return "Spor obrt";
  }

  if (normalizedSellThrough === "critical") {
    return "Kritičan signal";
  }

  if (normalizedSellThrough === "warning") {
    return "Prati signal";
  }

  if (normalizedSellThrough === "good") {
    return "Stabilan signal";
  }

  return "Nedovoljno podataka";
}

export function buildInventoryRow(item: InventoryListItemWithSignals, stores: StoreOption[], suppliers: SupplierFilterOption[]): InventoryRow {
  const quantity = item.kolicina ?? 0;
  const minimum = item.minimalnaKolicina ?? 0;
  const supplierName = suppliers.find((entry) => entry.supplierId === item.idDobavljac)?.supplierName ?? (item.idDobavljac != null ? `Dobavljac #${item.idDobavljac}` : "Nerasporedjen");
  const storeName = stores.find((entry) => entry.storeId === item.idObjekat)?.storeName ?? (item.idObjekat != null ? `Objekat #${item.idObjekat}` : "Sve lokacije");
  const unitCost = item.nabavnaCena ?? 0;
  const positiveQuantity = Math.max(quantity, 0);
  const estimatedValueAmount = item.estimatedValue ?? unitCost * positiveQuantity;
  const coverageRatio = minimum > 0 ? quantity / minimum : null;
  const stock = getStockState(quantity, minimum);
  const stockCoverStatus = item.stockCoverStatus ?? "insufficient_data";
  const sellThroughStatus = item.sellThroughStatus ?? "insufficient_data";

  return {
    ...item,
    supplierName,
    storeName,
    quantity,
    minimum,
    reorderGap: Math.max(minimum - quantity, 0),
    stockState: stock.key,
    stockStateLabel: stock.label,
    estimatedValueAmount,
    unitCost,
    coverageRatio,
    stockCoverDays: item.stockCoverDays ?? null,
    stockCoverStatus,
    stockCoverStatusLabel: stockCoverStatusLabel(stockCoverStatus),
    sellThroughRatio: item.sellThroughRatio ?? null,
    sellThroughStatus,
    sellThroughStatusLabel: sellThroughStatusLabel(sellThroughStatus),
    signalConfidencePct: item.signalConfidencePct ?? null,
    signalText: buildSignalText(stockCoverStatus, sellThroughStatus),
    dataQualityStatus: item.dataQualityStatus ?? "insufficient_data",
    reasonCodes: item.reasonCodes ?? [],
  };
}

export function buildRowFromInsightItem(item: InventoryInsightItem, stores: StoreOption[], suppliers: SupplierFilterOption[]) {
  return buildInventoryRow({
    id: item.id,
    plu: item.plu,
    naziv: item.naziv,
    kolicina: item.quantity,
    minimalnaKolicina: item.minimum,
    nabavnaCena: item.estimatedValue > 0 && item.quantity > 0 ? item.estimatedValue / item.quantity : 0,
    estimatedValue: item.estimatedValue,
    idObjekat: stores.find((store) => store.storeName === item.storeName)?.storeId ?? null,
    idDobavljac: suppliers.find((supplier) => supplier.supplierName === item.supplierName)?.supplierId ?? null,
  }, stores, suppliers);
}

export function getCoverageText(row: InventoryRow) {
  if (row.coverageRatio == null) return "Bez minimuma";
  if (row.coverageRatio >= 2) return "Komforna zaliha";
  if (row.coverageRatio >= 1) return "Na minimumu";
  return "Ispod minimuma";
}

export function formatStockCoverDays(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "Nedovoljno podataka";
  return `${formatNumber(value, 1)} dana`;
}

export function formatSellThroughRatio(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "Nedovoljno podataka";
  return formatPercent(value * 100);
}

export function getRecommendation(row: InventoryRow) {
  if (row.stockState === "critical") return "Hitno proveriti dopunu ili redistribuciju iz druge lokacije.";
  if (row.stockState === "warning") return `Planirati dopunu od najmanje ${formatNumber(Math.max(row.reorderGap, 1))} komada.`;
  if (row.quantity >= Math.max(row.minimum * 3, 15)) return "Zaliha je komforna; proveri da li je kapital previse vezan u robi.";
  return "Zaliha je stabilna i ne zahteva hitnu akciju.";
}

export function getHistoryDirection(quantity?: number | null) {
  if ((quantity ?? 0) > 0) return "Ulaz";
  if ((quantity ?? 0) < 0) return "Izlaz";
  return "Promena";
}

export function buildSupplierChart(rows: InventoryRow[]) {
  const totals = new Map<string, number>();
  for (const row of rows) totals.set(row.supplierName, (totals.get(row.supplierName) ?? 0) + row.estimatedValueAmount);
  return Array.from(totals.entries()).map(([supplierName, totalValue]) => ({ supplierName, totalValue }));
}

export function buildStoreLabel(store: StoreOption) {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

export function createScheduleDraft(): InventoryReportScheduleInput {
  return {
    name: "",
    isEnabled: true,
    frequency: "daily",
    dayOfWeek: 1,
    runAtLocalTime: "08:00",
    timeZoneId: "Europe/Belgrade",
    format: "pdf",
    orientation: "landscape",
    includeFiltersAndMetadata: true,
    recipientsCsv: "",
    subject: "",
    search: "",
    storeId: null,
    supplierId: null,
    sortBy: "kolicina",
  };
}

export function getActionTypeTone(actionType: string) {
  return resolveTone(TONE.actionType, actionType, TONE.actionType.clearance);
}

export function getActionStatusTone(status: string) {
  return resolveTone(TONE.actionStatus, status, TONE.actionStatus.pending);
}

export function getPriorityTone(priority: string) {
  return resolveTone(TONE.priority, priority, TONE.priority.low);
}

export function getAlertSeverityTone(severity: string) {
  return resolveTone(TONE.severity, severity, TONE.severity.info);
}

export function getRebalanceUrgencyTone(urgency: string) {
  return resolveTone(TONE.urgency, urgency, TONE.urgency.optional);
}

export function getAgingTone(bucket: string) {
  return resolveTone(TONE.aging, bucket, TONE.aging["90+"]);
}

export function getAbcTone(bucket: string) {
  return resolveTone(TONE.abc, bucket, TONE.abc.C);
}
