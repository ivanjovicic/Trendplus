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

function round(value: number, digits = 2) {
  if (!Number.isFinite(value)) return 0;
  return Number(value.toFixed(digits));
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
  const demandMap = aggregateDemandSignals(demand);
  const buckets = new Map<string, CategoryAggregate>();

  for (const item of price) {
    const category = item.category || "Uncategorized";
    const inventorySignal = inventoryMap.get(item.articleId);
    const demandSignal = demandMap.get(item.articleId);
    const approxUnits = (inventorySignal?.avgDailySales30d ?? demandSignal?.salesVelocity ?? 0) * 30;
    const approxRevenue = approxUnits * item.netPrice;
    const marginPct = item.marginPct ?? 0;
    const discountDepth = item.discountDepth ?? 0;
    const velocity = inventorySignal?.avgDailySales30d ?? demandSignal?.salesVelocity ?? 0;

    const aggregate = buckets.get(category) ?? {
      category,
      approxRevenue: 0,
      units: 0,
      marginSum: 0,
      profitLiftSum: 0,
      velocitySum: 0,
      skuIds: new Set<number>(),
    };

    aggregate.approxRevenue += approxRevenue;
    aggregate.units += approxUnits;
    aggregate.marginSum += marginPct * 100;
    aggregate.profitLiftSum += ((marginPct * 100) - (discountDepth * 100));
    aggregate.velocitySum += velocity;
    aggregate.skuIds.add(item.articleId);
    buckets.set(category, aggregate);
  }

  const totalRevenue = Array.from(buckets.values()).reduce((sum, bucket) => sum + bucket.approxRevenue, 0);

  const byCategory: CategoryStat[] = Array.from(buckets.values())
    .map((bucket) => ({
      kategorija: bucket.category,
      totalRevenue: round(bucket.approxRevenue, 2),
      totalUnits: Math.round(bucket.units),
      marginPct: round(bucket.marginSum / Math.max(bucket.skuIds.size, 1), 1),
      profitLift: round(bucket.profitLiftSum / Math.max(bucket.skuIds.size, 1), 1),
      // CategoryStat.revShare contract: percent units (25 = 25%), matching legacy InsightStudioEndpoints.
      revShare: totalRevenue > 0 ? round((bucket.approxRevenue / totalRevenue) * 100, 2) : null,
      velocity: round(bucket.velocitySum / Math.max(bucket.skuIds.size, 1), 3),
      uniqueSKU: bucket.skuIds.size,
    }))
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
    const band = getBand(item.netPrice);
    const inventorySignal = inventoryMap.get(item.articleId);
    const velocity = inventorySignal?.avgDailySales30d ?? 0;
    const totalUnits = velocity * 30;
    const current = bands.get(band) ?? {
      prices: [],
      margins: [],
      velocities: [],
      skuCount: 0,
      totalUnits: 0,
      totalStock: 0,
      markdownCount: 0,
    };

    current.prices.push(item.netPrice);
    current.margins.push((item.marginPct ?? 0) * 100);
    current.velocities.push(velocity);
    current.skuCount += 1;
    current.totalUnits += totalUnits;
    current.totalStock += inventorySignal?.stockQty ?? 0;
    current.markdownCount += item.discountDepth >= 0.2 ? 1 : 0;
    bands.set(band, current);
  }

  const bandOrder = ["0-3k", "3-6k", "6-10k", "10-15k", "15k+"];
  const result: PriceBand[] = bandOrder
    .filter((band) => bands.has(band))
    .map((band) => {
      const current = bands.get(band)!;
      const avgVelocityPerSku = current.velocities.reduce((sum, value) => sum + value, 0) / Math.max(current.skuCount, 1);
      const avgDiscountDepth = current.markdownCount / Math.max(current.skuCount, 1);
      const elasticity =
        avgDiscountDepth >= 0.35 ? "Elastic" :
        avgVelocityPerSku >= 1.0 && avgDiscountDepth <= 0.15 ? "Inelastic" :
        "Balanced";

      return {
        priceBand: band,
        skuCount: current.skuCount,
        totalUnits: Math.round(current.totalUnits),
        avgVelocityPerSku: round(avgVelocityPerSku, 2),
        avgPrice: round(current.prices.reduce((sum, value) => sum + value, 0) / Math.max(current.skuCount, 1), 2),
        avgMarginPct: round(current.margins.reduce((sum, value) => sum + value, 0) / Math.max(current.skuCount, 1), 1),
        totalStock: Math.round(current.totalStock),
        markdownCount: current.markdownCount,
        elasticity,
      };
    });

  return { bands: result };
}

export function buildAgingResultFromSignals(
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[]
): AgingResult {
  const demandMap = aggregateDemandSignals(demand);
  const priceMap = priceByArticle(price);

  const items: AgingItem[] = inventory
    .map((item) => {
      const demandSignal = demandMap.get(item.articleId);
      const priceSignal = priceMap.get(item.articleId);
      const asOfDate = new Date(item.date);
      const daysWithoutSale = demandSignal?.daysSinceLastSale ?? 999;
      const lastSaleDate = Number.isFinite(daysWithoutSale)
        ? formatIsoDate(new Date(asOfDate.getTime() - Math.max(daysWithoutSale, 0) * 24 * 60 * 60 * 1000))
        : "n/a";
      const stockValue = (priceSignal?.cost ?? priceSignal?.netPrice ?? 0) * item.stockQty;
      return {
        id: item.articleId,
        naziv: item.productName,
        kategorija: item.category,
        pol: "N/A",
        kolicina: Math.round(item.stockQty),
        stockValue: round(stockValue, 2),
        dobavljacNaziv: item.supplierName,
        lastSaleDate,
        daysWithoutSale,
        agingCategory: toAgingCategory(daysWithoutSale),
      };
    })
    .sort((a, b) => b.daysWithoutSale - a.daysWithoutSale);

  const summary = {
    totalSKU: items.length,
    critical: items.filter((item) => item.agingCategory === "Kritično").length,
    warning: items.filter((item) => item.agingCategory === "Upozorenje").length,
    watch: items.filter((item) => item.agingCategory === "Pazi").length,
    active: items.filter((item) => item.agingCategory === "Aktivno").length,
    criticalStockValue: round(
      items
        .filter((item) => item.agingCategory === "Kritično")
        .reduce((sum, item) => sum + (item.stockValue ?? 0), 0),
      2
    ),
  };

  return { items, summary };
}

export function buildDepletionResultFromSignals(
  inventory: InventoryRiskSignalItem[],
  price: PriceIntelligenceItem[],
  asOfDate?: string | null
): DepletionResult {
  const priceMap = priceByArticle(price);
  const snapshotDate = asOfDate ? new Date(asOfDate) : new Date();

  const forecasts: DepletionForecast[] = inventory
    .filter((item) => (item.avgDailySales30d ?? 0) > 0)
    .map((item) => {
      const priceSignal = priceMap.get(item.articleId);
      const daysOfCover = item.daysOfCover ?? 999;
      const severity = toSeverity(daysOfCover);
      const finiteCover = Number.isFinite(daysOfCover) ? Math.max(0, daysOfCover) : 999;
      const depletionDate = finiteCover > 365
        ? ""
        : formatIsoDate(new Date(snapshotDate.getTime() + Math.ceil(finiteCover) * 24 * 60 * 60 * 1000));
      const atRiskUnits = finiteCover <= 14
        ? Math.max(0, 14 - finiteCover) * item.avgDailySales30d
        : 0;
      const atRiskRevenue = atRiskUnits * (priceSignal?.netPrice ?? 0);

      return {
        artikalId: item.articleId,
        naziv: item.productName,
        kategorija: item.category,
        currentStock: Math.round(item.stockQty),
        avgDailySales: round(item.avgDailySales30d, 2),
        daysUntilOOS: finiteCover > 365 ? 999 : Math.max(0, Math.ceil(finiteCover)),
        depletionDate,
        atRiskRevenue: round(atRiskRevenue, 2),
        marginPct: round(((priceSignal?.marginPct ?? 0) * 100), 1),
        severity,
      };
    })
    .sort((a, b) => a.daysUntilOOS - b.daysUntilOOS);

  return {
    forecasts,
    totalAtRiskRevenue: round(forecasts.reduce((sum, item) => sum + item.atRiskRevenue, 0), 2),
    criticalCount: forecasts.filter((item) => item.severity === "CRITICAL").length,
  };
}

export function buildSmartReorderFromSignals(
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[],
  trend: TrendMomentumItem[]
): SmartReorderResult {
  const demandMap = aggregateDemandSignals(demand);
  const priceMap = priceByArticle(price);
  const trendMap = trendByArticle(trend);

  const items: SmartReorderItem[] = inventory
    .map((item) => {
      const demandSignal = demandMap.get(item.articleId);
      const priceSignal = priceMap.get(item.articleId);
      const trendSignal = trendMap.get(item.articleId);
      const avgDailySales = item.avgDailySales30d ?? 0;
      const currentStock = Math.max(0, Math.round(item.stockQty));
      const totalSold = Math.round(avgDailySales * 30);
      const doh = item.daysOfCover ?? 999;
      const rop = avgDailySales * 14 * 1.25;
      const needsReorder = avgDailySales > 0 && currentStock <= rop;
      const recommendedQty = needsReorder ? Math.max(0, Math.ceil(avgDailySales * 30 - currentStock)) : 0;
      const urgencyCode = bucketUrgency(doh);
      const urgency = toLegacyUrgencyLabel(urgencyCode);
      const marginPct = (priceSignal?.marginPct ?? 0) * 100;
      const reorderCost = recommendedQty * (priceSignal?.cost ?? 0);
      const expectedRevenue = recommendedQty * (priceSignal?.netPrice ?? 0);
      const expectedProfit = expectedRevenue - reorderCost;
      const demandScore = Math.min(20, Math.max(0, (demandSignal?.demandAcceleration ?? 0) * 20));
      const trendScore = Math.min(20, Math.max(0, (trendSignal?.externalTrendScore ?? 0) / 5));
      const marginScore = Math.min(20, Math.max(0, marginPct / 2));
      const stockPressureScore =
        urgencyCode === "KRITICNO" ? 40 :
        urgencyCode === "HITNO" ? 30 :
        urgencyCode === "PREPORUCUJE_SE" ? 20 :
        5;
      const reorderProbability = Math.min(100, round(stockPressureScore + demandScore + trendScore + marginScore, 1));

      return {
        artikalId: item.articleId,
        naziv: item.productName,
        kategorija: item.category,
        pol: "N/A",
        dobavljacNaziv: item.supplierName,
        currentStock,
        totalSold,
        avgDailySales: round(avgDailySales, 2),
        doh: round(doh, 2),
        rop: round(rop, 2),
        needsReorder,
        recommendedQty,
        urgency,
        marginPct: round(marginPct, 1),
        reorderCost: round(reorderCost, 2),
        expectedRevenue: round(expectedRevenue, 2),
        expectedProfit: round(expectedProfit, 2),
        reorderProbability,
        prodajnaCena: priceSignal?.netPrice ?? null,
      };
    })
    .filter((item) => item.avgDailySales > 0 || item.currentStock > 0)
    .sort((a, b) => {
      if (a.needsReorder !== b.needsReorder) return Number(b.needsReorder) - Number(a.needsReorder);
      return b.reorderProbability - a.reorderProbability;
    });

  const byCategoryPlan = Array.from(
    items.reduce((map, item) => {
      const current = map.get(item.kategorija) ?? {
        kategorija: item.kategorija,
        totalItems: 0,
        criticalCount: 0,
        urgentCount: 0,
        totalReorderCost: 0,
        expectedRevenue: 0,
        avgMargin: 0,
        marginCount: 0,
      };

      current.totalItems += 1;
      current.criticalCount += item.urgency === "KRITIČNO" ? 1 : 0;
      current.urgentCount += item.urgency === "HITNO" ? 1 : 0;
      current.totalReorderCost += item.reorderCost;
      current.expectedRevenue += item.expectedRevenue;
      current.avgMargin += item.marginPct;
      current.marginCount += 1;
      map.set(item.kategorija, current);
      return map;
    }, new Map<string, {
      kategorija: string;
      totalItems: number;
      criticalCount: number;
      urgentCount: number;
      totalReorderCost: number;
      expectedRevenue: number;
      avgMargin: number;
      marginCount: number;
    }>())
  ).map(([, value]) => ({
    kategorija: value.kategorija,
    totalItems: value.totalItems,
    criticalCount: value.criticalCount,
    urgentCount: value.urgentCount,
    totalReorderCost: round(value.totalReorderCost, 2),
    expectedRevenue: round(value.expectedRevenue, 2),
    avgMargin: round(value.avgMargin / Math.max(value.marginCount, 1), 1),
  })).sort((a, b) => b.criticalCount - a.criticalCount || b.totalReorderCost - a.totalReorderCost);

  const bySupplierPlan = Array.from(
    items.reduce((map, item) => {
      const current = map.get(item.dobavljacNaziv) ?? {
        dobavljac: item.dobavljacNaziv,
        totalItems: 0,
        criticalCount: 0,
        totalReorderCost: 0,
        reorderProbability: 0,
      };

      current.totalItems += 1;
      current.criticalCount += item.urgency === "KRITIČNO" ? 1 : 0;
      current.totalReorderCost += item.reorderCost;
      current.reorderProbability += item.reorderProbability;
      map.set(item.dobavljacNaziv, current);
      return map;
    }, new Map<string, {
      dobavljac: string;
      totalItems: number;
      criticalCount: number;
      totalReorderCost: number;
      reorderProbability: number;
    }>())
  ).map(([, value]) => ({
    dobavljac: value.dobavljac,
    totalItems: value.totalItems,
    criticalCount: value.criticalCount,
    totalReorderCost: round(value.totalReorderCost, 2),
    avgReorderProbability: round(value.reorderProbability / Math.max(value.totalItems, 1), 1),
  })).sort((a, b) => b.criticalCount - a.criticalCount || b.totalReorderCost - a.totalReorderCost);

  const summary = {
    criticalCount: items.filter((item) => item.urgency === "KRITIČNO").length,
    urgentCount: items.filter((item) => item.urgency === "HITNO").length,
    recommendedCount: items.filter((item) => item.urgency === "PREPORUČUJE SE").length,
    totalReorderCost: round(items.reduce((sum, item) => sum + item.reorderCost, 0), 2),
    expectedRevenueFromReorder: round(items.reduce((sum, item) => sum + item.expectedRevenue, 0), 2),
    expectedProfitFromReorder: round(items.reduce((sum, item) => sum + item.expectedProfit, 0), 2),
  };

  return {
    items,
    byCategoryPlan,
    bySupplierPlan,
    summary,
  };
}

export function mergeCategorySignalsAsPrimary(
  legacy: CategoryIntelligence | null,
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[]
): CategoryIntelligence | null {
  if (price.length === 0 && inventory.length === 0 && demand.length === 0) {
    return legacy;
  }

  return buildCategoryIntelligenceFromSignals(price, inventory, demand, legacy?.byGender ?? []);
}

export function mergePriceSensitivityAsPrimary(
  legacy: PriceSensitivity | null,
  price: PriceIntelligenceItem[],
  inventory: InventoryRiskSignalItem[]
): PriceSensitivity | null {
  if (price.length === 0 && inventory.length === 0) {
    return legacy;
  }

  return buildPriceSensitivityFromSignals(price, inventory);
}

export function mergeAgingAsPrimary(
  legacy: AgingResult | null,
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[]
): AgingResult | null {
  if (inventory.length === 0) {
    return legacy;
  }

  return buildAgingResultFromSignals(inventory, demand, price);
}

export function mergeDepletionAsPrimary(
  legacy: DepletionResult | null,
  inventory: InventoryRiskSignalItem[],
  price: PriceIntelligenceItem[],
  asOfDate?: string | null
): DepletionResult | null {
  if (inventory.length === 0) {
    return legacy;
  }

  return buildDepletionResultFromSignals(inventory, price, mergeAsOfDate(asOfDate, inventory[0]?.date));
}

export function mergeSmartReorderAsPrimary(
  legacy: SmartReorderResult | null,
  inventory: InventoryRiskSignalItem[],
  demand: DemandSignalItem[],
  price: PriceIntelligenceItem[],
  trend: TrendMomentumItem[]
): SmartReorderResult | null {
  if (inventory.length === 0) {
    return legacy;
  }

  return buildSmartReorderFromSignals(inventory, demand, price, trend);
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
