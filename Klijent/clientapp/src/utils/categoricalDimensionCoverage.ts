export type CategoricalDimensionCoverage = {
  knownCoveragePct?: number | null;
  dimensionCoverageState?: string | null;
};

export const DIMENSION_NOT_POPULATED_IN_SOURCE = "not_populated_in_source";

export function isDimensionNotPopulatedInSource(
  coverage?: CategoricalDimensionCoverage | null,
): boolean {
  return coverage?.dimensionCoverageState === DIMENSION_NOT_POPULATED_IN_SOURCE;
}

export function dimensionNotPopulatedMessage(dimensionLabel: string): string {
  return `${dimensionLabel} nije popunjena u izvoru podataka (0% prometa sa poznatom vrednošću).`;
}
