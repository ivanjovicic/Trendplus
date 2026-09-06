import type { InventoryActionSuggestion, InventoryInsightItem, InventoryListItem, InventoryReportScheduleInput, StoreOption, SupplierFilterOption } from "../../types/analytics";
import type { InventoryRow } from "./types";
import { TONE, resolveTone } from "./toneMap";

type InventoryListItemWithSignals = InventoryListItem & {
  stockCoverDays?: number | null;
  stockCoverStatus?: string | null;
  stockCoverStatusLabel?: string | null;
  sellThroughRatio?: number | null;
  sellThroughStatus?: string | null;
  sellThroughStatusLabel?: string | null;
  signalConfidencePct?: number | null;
  recommendationAllowed?: boolean | null;
  dataQualityStatus?: string | null;
  reasonCodes?: string[] | null;
  contextStatus?: "loadingContext" | "contextMissing" | null;
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

export function formatNumber(value: number | null | undefined, digits = 0) {
  if (value == null || Number.isNaN(value)) return "Nije dostupno";
  return value.toLocaleString("sr-RS", { maximumFractionDigits: digits });
}

export function formatCurrency(value: number | null | undefined, fallback = "Nije dostupno") {
  if (value == null || Number.isNaN(value)) return fallback;
  return value.toLocaleString("sr-RS", { style: "currency", currency: "RSD", maximumFractionDigits: 0 });
}

export function formatPercent(value: number | null | undefined, fallback = "Nije dostupno") {
  if (value == null || Number.isNaN(value)) return fallback;
  return `${value.toLocaleString("sr-RS", { maximumFractionDigits: 1 })}%`;
}

export function formatSignalCountBadge(
  returnedCount: number | null | undefined,
  totalMatchingCount: number | null | undefined,
  unitLabel: string,
  isTruncated?: boolean | null,
) {
  const returned = returnedCount ?? 0;

  if (totalMatchingCount == null) {
    return isTruncated ? `Prikazano do ${formatNumber(returned)} ${unitLabel}` : `Prikazano ${formatNumber(returned)} ${unitLabel}`;
  }

  if (isTruncated || totalMatchingCount > returned) {
    return `Prikazano ${formatNumber(returned)} od ${formatNumber(totalMatchingCount)} ${unitLabel}`;
  }

  return `Prikazano ${formatNumber(returned)} ${unitLabel}`;
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

/** Minimal row shape for screen CSV export (order must match the visible table). */
export type InventoryScreenCsvRow = {
  plu?: string | null;
  naziv: string;
  supplierName: string;
  storeName: string;
  stockStateLabel: string;
  quantity: number | null;
  minimum: number | null;
  reorderGap: number | null;
  unitCost: number | null;
  estimatedValueAmount: number | null;
};

export function buildInventoryScreenCsvLines(rows: InventoryScreenCsvRow[]): string[] {
  return [
    ["PLU", "Naziv", "Dobavljač", "Prodavnica", "Status", "Kolicina", "Minimum", "Gap", "NabavnaCena", "Vrednost"].join(";"),
    ...rows.map((row) =>
      [
        csvEscape(row.plu ?? ""),
        csvEscape(row.naziv),
        csvEscape(row.supplierName),
        csvEscape(row.storeName),
        csvEscape(row.stockStateLabel),
        row.quantity == null ? "" : row.quantity,
        row.minimum == null ? "" : row.minimum,
        row.reorderGap == null ? "" : row.reorderGap,
        row.unitCost == null ? "" : row.unitCost.toFixed(2),
        row.estimatedValueAmount == null ? "" : row.estimatedValueAmount.toFixed(2),
      ].join(";"),
    ),
  ];
}

export function buildInventoryScreenCsvFilename(pageNumber: number, sortBy: string): string {
  const sortToken = isInventoryPageLocalRiskSort(sortBy)
    ? sortBy
    : sortBy && sortBy !== "kolicina"
      ? sortBy.replace(/[^a-zA-Z0-9_-]/g, "")
      : "";
  const sortSuffix = sortToken ? `-${sortToken}` : "";
  return `bilans-stanja-strana-${pageNumber}${sortSuffix}.csv`;
}

export function getStockState(quantity: number | null | undefined, minimum: number | null | undefined) {
  if (quantity == null || !Number.isFinite(quantity)) {
    return {
      key: "unknown" as const,
      label: "Nepoznata zaliha",
      badge: TONE.stock.warning,
      panel: TONE.stockPanel.warning,
    };
  }

  if (quantity <= 0) {
    return {
      key: "critical" as const,
      label: "Bez zaliha",
      badge: TONE.stock.critical,
      panel: TONE.stockPanel.critical,
    };
  }

  if (minimum == null || !Number.isFinite(minimum)) {
    return {
      key: "unknown" as const,
      label: "Bez praga",
      badge: TONE.stock.warning,
      panel: TONE.stockPanel.warning,
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
    case "low_cover":
    case "low":
      return "Niska pokrivenost";
    case "healthy":
      return "Zdrava pokrivenost";
    case "overstock":
    case "high":
      return "Prekomerna zaliha";
    case "slow_stock":
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

export function buildSignalText(stockCoverStatus: string, sellThroughStatus: string, recommendationAllowed?: boolean | null): string {
  const normalizedStockCover = (stockCoverStatus ?? "").trim().toLowerCase();
  const normalizedSellThrough = (sellThroughStatus ?? "").trim().toLowerCase();

  if (recommendationAllowed !== true) {
    return "Nedovoljno podataka";
  }

  if (normalizedStockCover === "insufficient_data" || normalizedSellThrough === "insufficient_data") {
    return "Nedovoljno podataka";
  }

  if (normalizedStockCover === "out_of_stock_risk" || normalizedStockCover === "low_cover" || normalizedStockCover === "low") {
    return "Dopuni";
  }

  if (normalizedStockCover === "slow_stock" || normalizedStockCover === "slow" || normalizedStockCover === "no_velocity") {
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
  const quantity =
    item.kolicina == null || !Number.isFinite(item.kolicina) ? null : item.kolicina;
  const minimum =
    item.minimalnaKolicina == null || !Number.isFinite(item.minimalnaKolicina)
      ? null
      : item.minimalnaKolicina;
  const supplierName = suppliers.find((entry) => entry.supplierId === item.idDobavljac)?.supplierName ?? (item.idDobavljac != null ? `Dobavljac #${item.idDobavljac}` : "Nerasporedjen");
  const storeName = stores.find((entry) => entry.storeId === item.idObjekat)?.storeName ?? (item.idObjekat != null ? `Objekat #${item.idObjekat}` : "Sve lokacije");
  const unitCost = item.nabavnaCena ?? null;
  // Missing cost + missing backend estimate must stay unknown (not fake zero capital),
  // except when on-hand quantity is already a measured zero (true zero capital).
  const estimatedValueAmount =
    item.estimatedValue != null
      ? item.estimatedValue
      : quantity == null
        ? null
        : unitCost != null
          ? unitCost * Math.max(quantity, 0)
          : quantity === 0
            ? 0
            : null;
  const coverageRatio =
    quantity != null && minimum != null && minimum > 0 ? quantity / minimum : null;
  const stock = getStockState(quantity, minimum);
  const stockCoverStatus = item.stockCoverStatus ?? "insufficient_data";
  const sellThroughStatus = item.sellThroughStatus ?? "insufficient_data";
  const stockCoverStatusLabelValue = item.stockCoverStatusLabel ?? stockCoverStatusLabel(stockCoverStatus);
  const sellThroughStatusLabelValue = item.sellThroughStatusLabel ?? sellThroughStatusLabel(sellThroughStatus);
  const reorderGap =
    quantity != null && minimum != null ? Math.max(minimum - quantity, 0) : null;

  return {
    ...item,
    supplierName,
    storeName,
    quantity,
    minimum,
    reorderGap,
    stockState: stock.key,
    stockStateLabel: stock.label,
    estimatedValueAmount,
    unitCost,
    coverageRatio,
    stockCoverDays: item.stockCoverDays ?? null,
    stockCoverStatus,
    stockCoverStatusLabel: stockCoverStatusLabelValue,
    sellThroughRatio: item.sellThroughRatio ?? null,
    sellThroughStatus,
    sellThroughStatusLabel: sellThroughStatusLabelValue,
    signalConfidencePct: item.signalConfidencePct ?? null,
    recommendationAllowed:
      quantity == null || minimum == null
        ? false
        : (item.recommendationAllowed ?? null),
    signalText: buildSignalText(stockCoverStatus, sellThroughStatus, item.recommendationAllowed),
    dataQualityStatus:
      quantity == null
        ? "insufficient_data"
        : (item.dataQualityStatus ?? "insufficient_data"),
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
    stockCoverDays: item.stockCoverDays ?? null,
    stockCoverStatus: item.stockCoverStatus,
    stockCoverStatusLabel: item.stockCoverStatusLabel,
    sellThroughRatio: item.sellThroughRatio ?? null,
    sellThroughStatus: item.sellThroughStatus,
    sellThroughStatusLabel: item.sellThroughStatusLabel,
    signalConfidencePct: item.signalConfidencePct,
    recommendationAllowed: item.recommendationAllowed,
    dataQualityStatus: item.dataQualityStatus,
    reasonCodes: item.reasonCodes,
  }, stores, suppliers);
}

export type ForecastRestockSignal = {
  skuId: number;
  storeId: number;
  sizeCode: string;
  forecast7d: number | null;
  probabilityOfOOSIn7d: number | null;
};

export function buildForecastRestockSuggestion(
  row: InventoryRow,
  signal: ForecastRestockSignal,
  stores: StoreOption[],
  daysSinceMovement = 0,
): InventoryActionSuggestion {
  const forecast7d = signal.forecast7d ?? 0;
  const probabilityOfOOSIn7d = signal.probabilityOfOOSIn7d ?? 0;
  const suggestedQty = Math.max(1, Math.ceil(forecast7d));
  const unitCost = row.unitCost;
  const costMissing = row.unitCost == null || row.unitCost <= 0;

  return {
    suggestionKey: `forecast-${signal.skuId}-${signal.storeId}-${signal.sizeCode}`,
    actionType: "dopuna",
    priority: probabilityOfOOSIn7d > 0.7 ? "critical" : "high",
    label: `Predlozena dopuna za ${row.naziv}`,
    reason: `Forecast 7d je ${forecast7d.toFixed(1)} kom, a OOS rizik ${Math.round(probabilityOfOOSIn7d * 100)}%.`,
    status: "pending",
    artikalId: signal.skuId,
    plu: row.plu,
    naziv: row.naziv,
    fromStoreName: null,
    toStoreName: stores.find((store) => store.storeId === signal.storeId)?.storeName ?? row.storeName,
    suggestedQty,
    forecastDemandQty: suggestedQty,
    estimatedValue: costMissing || unitCost == null ? null : unitCost * suggestedQty,
    costMissing,
    daysSinceMovement,
    note: `Automatski dodat iz forecast sekcije za velicinu ${signal.sizeCode} kao signal prognozirane potraznje.`,
    updatedAtUtc: new Date().toISOString(),
  };
}

export function getCoverageText(row: InventoryRow) {
  if (row.coverageRatio == null) return "Bez minimuma";
  if (row.coverageRatio >= 2) return "Komforna zaliha";
  if (row.coverageRatio >= 1) return "Na minimumu";
  return "Ispod minimuma";
}

export function formatStockCoverDays(value: number | null | undefined, status?: string | null): string {
  if (value == null || Number.isNaN(value)) {
    return (status ?? "").trim().toLowerCase() === "insufficient_data"
      ? "Nedovoljno podataka"
      : "Nije dostupno";
  }
  return `${formatNumber(value, 1)} dana`;
}

export function formatSellThroughRatio(value: number | null | undefined, status?: string | null): string {
  if (value == null || Number.isNaN(value)) {
    return (status ?? "").trim().toLowerCase() === "insufficient_data"
      ? "Nedovoljno podataka"
      : "Nije dostupno";
  }
  return formatPercent(value * 100);
}

export function getRecommendation(row: InventoryRow) {
  if (row.stockState === "unknown" || row.quantity == null || row.minimum == null) {
    return "Nedostaje dokaz o količini ili minimalnoj zalihi; ne klasifikuj kao stabilno ili OOS.";
  }
  if (row.stockState === "critical") return "Hitno proveriti dopunu ili redistribuciju iz druge lokacije.";
  if (row.stockState === "warning") {
    return `Planirati dopunu od najmanje ${formatNumber(Math.max(row.reorderGap ?? 0, 1))} komada.`;
  }
  if (row.quantity >= Math.max(row.minimum * 3, 15)) {
    return "Zaliha je komforna; proveri da li je kapital previse vezan u robi.";
  }
  return "Zaliha je stabilna i ne zahteva hitnu akciju.";
}

export function getHistoryDirection(quantity?: number | null) {
  if ((quantity ?? 0) > 0) return "Ulaz";
  if ((quantity ?? 0) < 0) return "Izlaz";
  return "Promena";
}

export function buildSupplierChart(rows: InventoryRow[]) {
  const totals = new Map<string, number>();
  for (const row of rows) {
    if (row.estimatedValueAmount == null) continue;
    totals.set(row.supplierName, (totals.get(row.supplierName) ?? 0) + row.estimatedValueAmount);
  }
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

/** OOS/overstock risk sort is applied only to the currently loaded inventory page. */
export function isInventoryPageLocalRiskSort(sortBy: string): boolean {
  return sortBy === "oosRisk" || sortBy === "overstockRisk";
}

export function inventoryRiskSortScopeWarning(
  sortBy: string,
  options: { pageSize: number; totalPages: number; totalCount: number },
): string | null {
  if (!isInventoryPageLocalRiskSort(sortBy)) return null;

  const sortLabel = sortBy === "oosRisk" ? "OOS rizik" : "Overstock rizik";
  const base =
    `Sortiranje po ${sortLabel} važi samo za artikle na trenutnoj strani (${options.pageSize} redova). ` +
    "Server i dalje sortira po količini; UI zatim preraspoređuje samo učitanu stranicu.";

  if (options.totalPages > 1) {
    return (
      `${base} Postoji ${options.totalPages} strana (${options.totalCount} artikala) - ` +
      "artikli sa višim rizikom mogu biti na drugim stranama. Za globalni prioritet koristite forecast / risk panele."
    );
  }

  return `${base} Trenutno je učitana cela filtrirana lista na jednoj strani.`;
}
