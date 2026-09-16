export function isValidColorPercentValue(value: number): boolean {
  return Number.isFinite(value) && value >= 0 && value <= 100;
}

export function resolveColorPercentValue(value: number | null | undefined): number | null {
  if (typeof value !== "number" || !isValidColorPercentValue(value)) {
    return null;
  }

  return value;
}

export function resolveColorPartSharePct(
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
  return resolveColorPercentValue(sharePct);
}

export function resolveColorRevenueSharePct(
  revenue: number | null | undefined,
  totalRevenue: number | null | undefined,
): number | null {
  return resolveColorPartSharePct(revenue, totalRevenue);
}

export function resolveColorComplementPercent(
  value: number | null | undefined,
): number | null {
  const normalized = resolveColorPercentValue(value);
  if (normalized == null) {
    return null;
  }

  return resolveColorPercentValue(100 - normalized);
}

export function resolveColorCountValue(value: number | null | undefined): number | null {
  if (typeof value !== "number" || !Number.isFinite(value) || value < 0) {
    return null;
  }

  return value;
}
