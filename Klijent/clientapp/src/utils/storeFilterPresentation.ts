import type { StoreOption } from "../types/analytics";

export function getDuplicateStoreNames(stores: readonly StoreOption[]): ReadonlySet<string> {
  const counts = new Map<string, number>();
  stores.forEach((store) => {
    const name = store.storeName.trim();
    counts.set(name, (counts.get(name) ?? 0) + 1);
  });

  return new Set(
    [...counts.entries()]
      .filter(([, count]) => count > 1)
      .map(([name]) => name),
  );
}

export function buildStoreOptionLabel(
  store: StoreOption,
  duplicateNames: ReadonlySet<string> = new Set(),
): string {
  const name = store.storeName.trim() || `Nepoznat objekat (ID ${store.storeId})`;
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  const baseLabel = extras ? `${name} (${extras})` : name;
  return duplicateNames.has(name) ? `${baseLabel} [ID ${store.storeId}]` : baseLabel;
}
