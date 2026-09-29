import type { AnalyticsResponseMeta, StoreOption } from "../types/analytics";
import { getAnalyticsMetaMessage } from "./analyticsResponseMeta";

export const STORE_FILTER_STALE_LIST_MESSAGE =
  "Lista prodavnica je zastarela za aktivni opseg podataka. Izbor pojedinačne prodavnice je blokiran dok se ne učita potvrđena lista.";

export const STORE_FILTER_LOAD_FAILED_MESSAGE =
  "Lista prodavnica nije osvežena jer zahtev nije uspeo.";

export const STORE_FILTER_SCOPE_LOADING_MESSAGE =
  "Lista prodavnica se osvežava za aktivni opseg podataka.";

export const STORE_SELECTION_CLEARED_MESSAGE =
  "Izabrana prodavnica nije dostupna u aktivnom opsegu i izbor je očišćen.";

export type StoreFilterFallbackResolution = {
  warning: string | null;
  isStale: boolean;
  shouldClearSelection: boolean;
  stores: StoreOption[];
  selectedStoreId: number | null;
};

type StoreFilterResponse = StoreOption[] & {
  meta?: AnalyticsResponseMeta | null;
};

export function resolveStoreFilterFallbackState(
  items: StoreFilterResponse,
  previousStores: StoreOption[] = [],
  selectedStoreId: number | null = null,
): StoreFilterFallbackResolution {
  const fallbackMessage = items.meta
    ? getAnalyticsMetaMessage(items.meta) ?? "Filteri prodavnica trenutno koriste pomoćni signal."
    : null;

  if (fallbackMessage) {
    return {
      warning: `${STORE_FILTER_STALE_LIST_MESSAGE} ${fallbackMessage}`,
      isStale: true,
      shouldClearSelection: selectedStoreId != null,
      stores: previousStores,
      selectedStoreId: null,
    };
  }

  const selectedStoreIsPresent = selectedStoreId == null
    || items.some((store) => store.storeId === selectedStoreId);

  return {
    warning: selectedStoreIsPresent ? null : STORE_SELECTION_CLEARED_MESSAGE,
    isStale: false,
    shouldClearSelection: !selectedStoreIsPresent,
    stores: [...items],
    selectedStoreId: selectedStoreIsPresent ? selectedStoreId : null,
  };
}

export function resolveStoreFilterLoadFailure(
  previousStores: StoreOption[] = [],
  selectedStoreId: number | null = null,
): StoreFilterFallbackResolution {
  return {
    warning: STORE_FILTER_LOAD_FAILED_MESSAGE,
    isStale: true,
    shouldClearSelection: selectedStoreId != null,
    stores: previousStores,
    selectedStoreId: null,
  };
}
