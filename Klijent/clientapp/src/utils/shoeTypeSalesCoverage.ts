export function resolveShoeTypeCoveragePct(
  numerator: number | null | undefined,
  denominator: number | null | undefined,
): number | null {
  if (
    typeof numerator !== "number" ||
    !Number.isFinite(numerator) ||
    numerator < 0 ||
    typeof denominator !== "number" ||
    !Number.isFinite(denominator) ||
    denominator <= 0
  ) {
    return null;
  }

  const coveragePct = (numerator / denominator) * 100;
  return Number.isFinite(coveragePct) ? coveragePct : null;
}
