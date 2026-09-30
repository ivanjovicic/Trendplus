export const PRODUCT_DECISION_ACTION_STATUS_BATCH_LIMIT = 1000;

export type ProductDecisionActionStatusLookup = {
  sourceType: string;
  sourceKey: string;
};

export function chunkProductDecisionActionStatusLookups<T>(
  items: readonly T[],
  batchSize = PRODUCT_DECISION_ACTION_STATUS_BATCH_LIMIT,
): T[][] {
  if (batchSize < 1) {
    throw new Error("batchSize must be positive");
  }

  const chunks: T[][] = [];
  for (let index = 0; index < items.length; index += batchSize) {
    chunks.push(items.slice(index, index + batchSize));
  }

  return chunks;
}

export function productDecisionActionStatusLookupSignature(
  items: readonly ProductDecisionActionStatusLookup[],
): string {
  return [...items]
    .map((item) => `${item.sourceType}::${item.sourceKey}`)
    .sort()
    .join("|");
}
