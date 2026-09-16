export function isValidShoeTypePercentValue(value: number): boolean {
  return Number.isFinite(value) && value >= 0 && value <= 100;
}

export function resolveShoeTypePercentValue(value: number | null | undefined): number | null {
  if (typeof value !== "number" || !isValidShoeTypePercentValue(value)) {
    return null;
  }

  return value;
}

export function resolveShoeTypePartSharePct(
  numerator: number | null | undefined,
  denominator: number | null | undefined,
): number | null {
  if (
    typeof numerator !== "number" ||
    !Number.isFinite(numerator) ||
    numerator < 0 ||
    typeof denominator !== "number" ||
    !Number.isFinite(denominator) ||
    denominator <= 0 ||
    numerator > denominator
  ) {
    return null;
  }

  const sharePct = (numerator / denominator) * 100;
  return resolveShoeTypePercentValue(sharePct);
}

export function resolveShoeTypeRevenueSharePct(
  revenue: number | null | undefined,
  totalRevenue: number | null | undefined,
): number | null {
  return resolveShoeTypePartSharePct(revenue, totalRevenue);
}

export function resolveShoeTypeQuantitySharePct(
  quantity: number | null | undefined,
  totalQuantity: number | null | undefined,
): number | null {
  return resolveShoeTypePartSharePct(quantity, totalQuantity);
}

export function resolveShoeTypeComplementPercent(
  value: number | null | undefined,
): number | null {
  const normalized = resolveShoeTypePercentValue(value);
  if (normalized == null) {
    return null;
  }

  return resolveShoeTypePercentValue(100 - normalized);
}
