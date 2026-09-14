export type InventoryRatioInput = {
  totalSkuCount?: number | null;
  outOfStockCount?: number | null;
  lowStockCount?: number | null;
};

export type InventoryRatioState = {
  availablePct: number | null;
  redZonePct: number | null;
};

function finiteOrNull(value: number | null | undefined): number | null {
  return value != null && Number.isFinite(value) ? value : null;
}

function compatibleCount(value: number | null, total: number): number | null {
  return value != null && value >= 0 && value <= total ? value : null;
}

/**
 * Projects inventory counts into ratios without treating missing data as zero.
 * A ratio is only trustworthy when its denominator and numerator are finite,
 * positive-denominator compatible counts.
 */
export function projectInventoryRatios(input: InventoryRatioInput): InventoryRatioState {
  const total = finiteOrNull(input.totalSkuCount);
  const outOfStock = finiteOrNull(input.outOfStockCount);
  const lowStock = finiteOrNull(input.lowStockCount);

  if (total == null || total <= 0) {
    return { availablePct: null, redZonePct: null };
  }

  const compatibleOutOfStock = compatibleCount(outOfStock, total);
  const compatibleLowStock = compatibleCount(lowStock, total);

  return {
    availablePct:
      compatibleOutOfStock == null ? null : ((total - compatibleOutOfStock) / total) * 100,
    redZonePct: compatibleLowStock == null ? null : (compatibleLowStock / total) * 100,
  };
}
