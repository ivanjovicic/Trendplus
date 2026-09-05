import type {
  DemandSignalItem,
  InventoryRiskSignalItem,
  PriceIntelligenceItem,
  TrendMomentumItem,
} from "./analyticsIntelligenceApi";
import type {
  AgingItem,
  AgingResult,
  CategoryIntelligence,
  CategoryStat,
  GenderStat,
  ReorderItem,
} from "./insightStudioApi";
import type {
  DepletionForecast,
  DepletionResult,
  PriceBand,
  PriceSensitivity,
  SmartReorderItem,
  SmartReorderResult,
} from "./insightStudioV2Api";

type AggregatedDemandSignal = {
  articleId: number;
  productName: string;
  category: string;
  supplierName: string;
  daysSinceLastSale: number | null;
  salesVelocity: number;
  demandAcceleration: number;
  storeCoverage: number;
};

type CategoryAggregate = {
  category: string;
  approxRevenue: number;
  units: number;
  marginSum: number;
  profitLiftSum: number;
  velocitySum: number;
  skuIds: Set<number>;
};

type DerivedAgingResult = Omit<AgingResult, "summary"> & {
  summary: Omit<AgingResult["summary"], "criticalStockValue"> & {
    criticalStockValue: number | null;
  };
};

type DerivedDepletionResult = Omit<DepletionResult, "totalAtRiskRevenue"> & {
  totalAtRiskRevenue: number | null;
};

function round(value: number, digits = 2) {
  if (!Number.isFinite(value)) return null;
  const rounded = Number(value.toFixed(digits));
  return Number.isFinite(rounded) ? rounded : null;
}

function isFiniteNumber(value: number | null | undefined): value is number {
  return value != null && Number.isFinite(value);
}

function formatIsoDate(date: Date) {
  return date.toISOString().slice(0, 10);
}

function bucketUrgency(doh: number) {
  if (doh < 7) return "KRITICNO";
  if (doh < 14) return "HITNO";
  if (doh < 30) return "PREPORUCUJE_SE";
  return "OK";
}

function toLegacyUrgencyLabel(code: ReturnType<typeof bucketUrgency>): SmartReorderItem["urgency"] {
  switch (code) {
    case "KRITICNO":
      return "KRITIČNO";
    case "HITNO":
      return "HITNO";
    case "PREPORUCUJE_SE":
      return "PREPORUČUJE SE";
    default:
      return "OK";
  }
}

function toAgingCategory(daysWithoutSale: number): AgingItem["agingCategory"] {
  if (daysWithoutSale > 90) return "Kritično";
  if (daysWithoutSale > 60) return "Upozorenje";
  if (daysWithoutSale > 30) return "Pazi";
  return "Aktivno";
}

function toSeverity(daysOfCover: number): DepletionForecast["severity"] {
  if (daysOfCover <= 7) return "CRITICAL";
  if (daysOfCover <= 14) return "WARNING";
  if (daysOfCover <= 30) return "WATCH";
  return "OK";
}

function getBand(price: number) {
  if (price < 3000) return "0-3k";
  if (price < 6000) return "3-6k";
  if (price < 10000) return "6-10k";
  if (price < 15000) return "10-15k";
  return "15k+";
}

function aggregateDemandSignals(demand: DemandSignalItem[]) {
  const map = new Map<number, AggregatedDemandSignal>();

  for (const item of demand) {
    if (!isFiniteNumber(item.salesVelocity)
      || !isFiniteNumber(item.demandAcceleration)
      || !isFiniteNumber(item.storeCoverage)) continue;
    const existing = map.get(item.articleId);
    if (!existing) {
      map.set(item.articleId, {
        articleId: item.articleId,
        productName: item.productName,
        category: item.category,
        supplierName: item.supplierName,
        daysSinceLastSale: item.daysSinceLastSale,
        salesVelocity: item.salesVelocity,
        demandAcceleration: item.demandAcceleration,
        storeCoverage: item.storeCoverage,
      });
      continue;
    }

    existing.salesVelocity = Math.max(existing.salesVelocity, item.salesVelocity);
    existing.demandAcceleration = Math.max(existing.demandAcceleration, item.demandAcceleration);
    existing.storeCoverage = Math.max(existing.storeCoverage, item.storeCoverage);
    existing.daysSinceLastSale =
      existing.daysSinceLastSale == null
        ? item.daysSinceLastSale
        : item.daysSinceLastSale == null
          ? existing.daysSinceLastSale
          : Math.min(existing.daysSinceLastSale, item.daysSinceLastSale);
  }

  return map;
}

function priceByArticle(price: PriceIntelligenceItem[]) {
  return new Map(price.map((item) => [item.articleId, item] as const));
}

function trendByArticle(trend: TrendMomentumItem[]) {
  return new Map(trend.map((item) => [item.articleId, item] as const));
}

function mergeAsOfDate(...values: Array<string | null | undefined>) {
  return values.find((value) => value && value.trim().length > 0) ?? null;
}

export function buildCategoryIntelligenceFromSignals(
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  byGender: GenderStat[] = []
): CategoryIntelligence {
  const inventoryMap = new Map(inventory.map((item) => [item.articleId, item] as const));
  const buckets = new Map<string, CategoryAggregate>();

  for (const item of price) {
    const category = item.category || "Uncategorized";
    const inventorySignal = inventoryMap.get(item.articleId);
    const velocity = inventorySignal?.avgDailySales30d;
    if (!isFiniteNumber(velocity) || !isFiniteNumber(item.netPrice) || item.netPrice < 0
      || !isFiniteNumber(item.marginPct) || !isFiniteNumber(item.discountDepth)) continue;

    const approxUnits = velocity * 30;
    const approxRevenue = approxUnits * item.netPrice;
    if (!isFiniteNumber(approxUnits) || !isFiniteNumber(approxRevenue)) continue;
    const marginPct = item.marginPct;
    const discountDepth = item.discountDepth;

    const aggregate = buckets.get(category) ?? {
      category,
      approxRevenue: 0,
      units: 0,
      marginSum: 0,
      profitLiftSum: 0,
      velocitySum: 0,
      skuIds: new Set<number>(),
    };

    const nextApproxRevenue = aggregate.approxRevenue + approxRevenue;
    const nextUnits = aggregate.units + approxUnits;
    const nextMarginSum = aggregate.marginSum + marginPct * 100;
    const nextProfitLiftSum = aggregate.profitLiftSum + ((marginPct * 100) - (discountDepth * 100));
    const nextVelocitySum = aggregate.velocitySum + velocity;
    if (![nextApproxRevenue, nextUnits, nextMarginSum, nextProfitLiftSum, nextVelocitySum].every(Number.isFinite)) continue;

    aggregate.approxRevenue = nextApproxRevenue;
    aggregate.units = nextUnits;
    aggregate.marginSum = nextMarginSum;
    aggregate.profitLiftSum = nextProfitLiftSum;
    aggregate.velocitySum = nextVelocitySum;
    aggregate.skuIds.add(item.articleId);
    buckets.set(category, aggregate);
  }

  const totalRevenue = Array.from(buckets.values()).reduce((sum, bucket) => sum + bucket.approxRevenue, 0);

  const byCategory: CategoryStat[] = Array.from(buckets.values())
    .map((bucket) => {
      const denominator = bucket.skuIds.size;
      const totalRevenueValue = round(bucket.approxRevenue, 2);
      const totalUnits = Math.round(bucket.units);
      const marginPct = round(bucket.marginSum / denominator, 1);
      const profitLift = round(bucket.profitLiftSum / denominator, 1);
      const revShare = totalRevenue > 0 && Number.isFinite(totalRevenue)
        ? round((bucket.approxRevenue / totalRevenue) * 100, 2)
        : null;
      const velocity = round(bucket.velocitySum / denominator, 3);
      if (totalRevenueValue == null || !Number.isFinite(totalUnits) || marginPct == null
        || profitLift == null || velocity == null || (totalRevenue > 0 && revShare == null)) return null;

      return {
        kategorija: bucket.category,
        totalRevenue: totalRevenueValue,
        totalUnits,
        marginPct,
        profitLift,
        // CategoryStat.revShare contract: percent units (25 = 25%), matching legacy InsightStudioEndpoints.
        revShare,
        velocity,
        uniqueSKU: denominator,
      };
    })
    .filter((row): row is CategoryStat => row != null)
    .sort((a, b) => b.totalRevenue - a.totalRevenue);

  return { byCategory, byGender };
}

export function buildPriceSensitivityFromSignals(
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[]
): PriceSensitivity {
  const inventoryMap = new Map(inventory.map((item) => [item.articleId, item] as const));
  const bands = new Map<string, {
    prices: number[];
    margins: number[];
    velocities: number[];
    skuCount: number;
    totalUnits: number;
    totalStock: number;
    markdownCount: number;
  }>();

  for (const item of price) {
    const inventorySignal = inventoryMap.get(item.articleId);
    const velocity = inventorySignal?.avgDailySales30d;
    if (!isFiniteNumber(velocity) || velocity < 0 || !isFiniteNumber(item.netPrice)
      || !isFiniteNumber(item.cost) || !isFiniteNumber(item.marginPct)
      || !isFiniteNumber(item.discountDepth) || !isFiniteNumber(inventorySignal?.stockQty)) continue;
    const totalUnits = velocity * 30;
    if (!isFiniteNumber(totalUnits) || inventorySignal.stockQty < 0) continue;
    const band = getBand(item.netPrice);
    const current = bands.get(band) ?? {
      prices: [],
      margins: [],
      velocities: [],
      skuCount: 0,
      totalUnits: 0,
      totalStock: 0,
      markdownCount: 0,
    };

    const nextTotalUnits = current.totalUnits + totalUnits;
    const nextTotalStock = current.totalStock + inventorySignal.stockQty;
    if (!Number.isFinite(nextTotalUnits) || !Number.isFinite(nextTotalStock)) continue;
    current.prices.push(item.netPrice);
    current.margins.push(item.marginPct * 100);
    current.velocities.push(velocity);
    current.skuCount += 1;
    current.totalUnits = nextTotalUnits;
    current.totalStock = nextTotalStock;
    current.markdownCount += item.discountDepth >= 0.2 ? 1 : 0;
    bands.set(band, current);
  }

  const bandOrder = ["0-3k", "3-6k", "6-10k", "10-15k", "15k+"];
  const result: PriceBand[] = bandOrder
    .filter((band) => bands.has(band))
    .map((band) => {
      const current = bands.get(band)!;
      const denominator = current.skuCount;
      const avgVelocityPerSku = current.velocities.reduce((sum, value) => sum + value, 0) / denominator;
      const avgDiscountDepth = current.markdownCount / denominator;
      const elasticity =
        avgDiscountDepth >= 0.35 ? "Elastic" :
        avgVelocityPerSku >= 1.0 && avgDiscountDepth <= 0.15 ? "Inelastic" :
        "Balanced";

      const roundedVelocity = round(avgVelocityPerSku, 2);
      const roundedPrice = round(current.prices.reduce((sum, value) => sum + value, 0) / denominator, 2);
      const roundedMargin = round(current.margins.reduce((sum, value) => sum + value, 0) / denominator, 1);
      if (roundedVelocity == null || roundedPrice == null || roundedMargin == null
        || !Number.isFinite(avgDiscountDepth)) return null;

      return {
        priceBand: band,
        skuCount: current.skuCount,
        totalUnits: Math.round(current.totalUnits),
        avgVelocityPerSku: roundedVelocity,
        avgPrice: roundedPrice,
        avgMarginPct: roundedMargin,
        totalStock: Math.round(current.totalStock),
        markdownCount: current.markdownCount,
        elasticity,
      };
    })
    .filter((row): row is PriceBand => row != null);

  return { bands: result };
}

export function buildAgingResultFromSignals(
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[]
): DerivedAgingResult {
  const demandMap = aggregateDemandSignals(demand);
  const priceMap = priceByArticle(price);

  const items: AgingItem[] = inventory
    .flatMap((item) => {
      const demandSignal = demandMap.get(item.articleId);
      const priceSignal = priceMap.get(item.articleId);
      const asOfDate = new Date(item.date);
      const daysWithoutSale = demandSignal?.daysSinceLastSale;
      const unitCost = isFiniteNumber(priceSignal?.cost) && priceSignal.cost >= 0 ? priceSignal.cost : null;
      if (!isFiniteNumber(daysWithoutSale) || daysWithoutSale < 0
        || !Number.isFinite(asOfDate.getTime()) || !isFiniteNumber(item.stockQty) || item.stockQty < 0) return [];
      const lastSaleTimestamp = asOfDate.getTime() - daysWithoutSale * 24 * 60 * 60 * 1000;
      if (!Number.isFinite(lastSaleTimestamp)) return [];
      const lastSaleDateValue = new Date(lastSaleTimestamp);
      if (!Number.isFinite(lastSaleDateValue.getTime())) return [];
      const lastSaleDate = formatIsoDate(lastSaleDateValue);
      const stockValue = unitCost == null ? null : unitCost * item.stockQty;
      if (stockValue != null && !Number.isFinite(stockValue)) return [];
      return [{
        id: item.articleId,
        naziv: item.productName,
        kategorija: item.category,
        pol: "N/A",
        kolicina: Math.round(item.stockQty),
        stockValue: stockValue == null ? null : round(stockValue, 2),
        dobavljacNaziv: item.supplierName,
        lastSaleDate,
        daysWithoutSale,
        agingCategory: toAgingCategory(daysWithoutSale),
      }];
    })
    .sort((a, b) => b.daysWithoutSale - a.daysWithoutSale);

  const criticalItems = items.filter((item) => item.agingCategory === "Kritično");
  const criticalStockValue = criticalItems.some((item) => item.stockValue == null)
    ? null
    : round(criticalItems.reduce((sum, item) => sum + item.stockValue!, 0), 2);
  const summary = {
    totalSKU: items.length,
    critical: items.filter((item) => item.agingCategory === "Kritično").length,
    warning: items.filter((item) => item.agingCategory === "Upozorenje").length,
    watch: items.filter((item) => item.agingCategory === "Pazi").length,
    active: items.filter((item) => item.agingCategory === "Aktivno").length,
    criticalStockValue,
  };

  return { items, summary };
}

export function buildDepletionResultFromSignals(
  inventory: InventoryRiskSignalItem[],
  price: PriceIntelligenceItem[],
  asOfDate?: string | null
): DerivedDepletionResult {
  const priceMap = priceByArticle(price);
  const snapshotDate = asOfDate ? new Date(asOfDate) : new Date();
  const hasValidSnapshotDate = Number.isFinite(snapshotDate.getTime());

  const forecasts: DepletionForecast[] = inventory
    .flatMap((item) => {
      const priceSignal = priceMap.get(item.articleId);
      const daysOfCover = item.daysOfCover;
      if (!isFiniteNumber(item.avgDailySales30d) || item.avgDailySales30d <= 0
        || !isFiniteNumber(daysOfCover) || daysOfCover < 0 || !isFiniteNumber(priceSignal?.netPrice)
        || priceSignal.netPrice < 0 || !isFiniteNumber(priceSignal?.marginPct)
        || !isFiniteNumber(item.stockQty) || item.stockQty < 0) return [];
      const severity = toSeverity(daysOfCover);
      const depletionDate = !hasValidSnapshotDate || daysOfCover > 365
        ? ""
        : formatIsoDate(new Date(snapshotDate.getTime() + Math.ceil(daysOfCover) * 24 * 60 * 60 * 1000));
      const atRiskUnits = daysOfCover <= 14
        ? (14 - daysOfCover) * item.avgDailySales30d
        : 0;
      const atRiskRevenue = atRiskUnits * priceSignal.netPrice;
      const avgDailySales = round(item.avgDailySales30d, 2);
      const roundedAtRiskRevenue = round(atRiskRevenue, 2);
      const marginPct = round(priceSignal.marginPct * 100, 1);
      if (avgDailySales == null || roundedAtRiskRevenue == null || marginPct == null) return [];

      return [{
        artikalId: item.articleId,
        naziv: item.productName,
        kategorija: item.category,
        currentStock: Math.round(item.stockQty),
        avgDailySales,
        daysUntilOOS: Math.ceil(daysOfCover),
        depletionDate,
        atRiskRevenue: roundedAtRiskRevenue,
        marginPct,
        severity,
      }];
    })
    .sort((a, b) => a.daysUntilOOS - b.daysUntilOOS);

  return {
    forecasts,
    totalAtRiskRevenue: forecasts.length === 0
      ? null
      : round(forecasts.reduce((sum, item) => sum + item.atRiskRevenue, 0), 2),
    criticalCount: forecasts.filter((item) => item.severity === "CRITICAL").length,
  };
}

export function buildSmartReorderFromSignals(
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[],
  trend: TrendMomentumItem[]
): SmartReorderResult {
  return {
    items: [],
    byCategoryPlan: [],
    bySupplierPlan: [],
    summary: {
      criticalCount: 0,
      urgentCount: 0,
      recommendedCount: 0,
      totalReorderCost: 0,
      expectedRevenueFromReorder: 0,
      expectedProfitFromReorder: 0,
    },
  };
}

export function mergeCategorySignalsAsPrimary(
  legacy: CategoryIntelligence | null,
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[]
): CategoryIntelligence | null {
  return legacy;
}

export function mergePriceSensitivityAsPrimary(
  legacy: PriceSensitivity | null,
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[]
): PriceSensitivity | null {
  return legacy;
}

export function mergeAgingAsPrimary(
  legacy: AgingResult | null,
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[]
): AgingResult | null {
  return legacy;
}

export function mergeDepletionAsPrimary(
  legacy: DepletionResult | null,
  inventory: InventoryRiskSignalItem[],
  price: PriceIntelligenceItem[],
  asOfDate?: string | null
): DepletionResult | null {
  return legacy;
}

export function mergeSmartReorderAsPrimary(
  legacy: SmartReorderResult | null,
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[],
  trend: TrendMomentumItem[]
): SmartReorderResult | null {
  return legacy;
}

export function buildLegacyReorderFallbackFromSignals(
  smart: SmartReorderResult | null
): { items: ReorderItem[]; summary: { criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number } } | null {
  if (!smart) return null;

  return {
    items: smart.items.map((item) => ({
      artikalId: item.artikalId,
      naziv: item.naziv,
      kategorija: item.kategorija,
      pol: item.pol,
      dobavljacNaziv: item.dobavljacNaziv,
      currentStock: item.currentStock,
      totalSold: item.totalSold,
      avgDailySales: item.avgDailySales,
      doh: item.doh,
      rop: item.rop,
      needsReorder: item.needsReorder,
      recommendedQty: item.recommendedQty,
      urgency: item.urgency,
      prodajnaCena: item.prodajnaCena,
    })),
    summary: {
      criticalCount: smart.summary.criticalCount,
      urgentCount: smart.summary.urgentCount,
      recommendedCount: smart.summary.recommendedCount,
      totalReorderValue: smart.summary.totalReorderCost,
    },
  };
}
