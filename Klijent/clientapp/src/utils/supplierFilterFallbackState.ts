import type { AnalyticsResponseMeta, SupplierFilterOption } from "../types/analytics";
import { getAnalyticsMetaMessage } from "./analyticsResponseMeta";

export const SUPPLIER_FILTER_STALE_LIST_MESSAGE =
  "Lista dobavljača je zastarela za izabrani period i opseg. Izbor je blokiran dok se ne učita pouzdan odgovor.";

export const SUPPLIER_FILTER_LOAD_FAILED_MESSAGE =
  "Lista dobavljača nije osvežena jer zahtev nije uspeo.";

export type SupplierFilterFallbackResolution = {
  warning: string | null;
  isStale: boolean;
  shouldClearSelection: boolean;
  suppliers: SupplierFilterOption[];
};

type SupplierFilterResponse = SupplierFilterOption[] & {
  meta?: AnalyticsResponseMeta | null;
};

export function resolveSupplierFilterFallbackState(
  items: SupplierFilterResponse,
  previousSuppliers: SupplierFilterOption[] = [],
): SupplierFilterFallbackResolution {
  const fallbackWarning = items.meta
    ? getAnalyticsMetaMessage(items.meta) ?? "Filteri dobavljača trenutno koriste pomoćni signal."
    : null;

  if (fallbackWarning) {
    return {
      warning: fallbackWarning,
      isStale: true,
      shouldClearSelection: true,
      suppliers: previousSuppliers,
    };
  }

  return {
    warning: null,
    isStale: false,
    shouldClearSelection: false,
    suppliers: [...items],
  };
}
