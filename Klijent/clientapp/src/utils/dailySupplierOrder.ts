import type { DailySalesSupplierHeader } from "../services/dailySalesStatsApi";

export type DailySupplierOrderResolution = {
  suppliers: DailySalesSupplierHeader[];
  warning: string | null;
};

function findDuplicateNames(names: string[]): string[] {
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  for (const name of names) {
    if (seen.has(name)) {
      duplicates.add(name);
      continue;
    }
    seen.add(name);
  }
  return [...duplicates];
}

export function resolveAuthoritativeTopSuppliers(
  topSuppliers: DailySalesSupplierHeader[] | null | undefined,
  topSuppliersOrder: string[] | null | undefined,
): DailySupplierOrderResolution {
  const suppliers = topSuppliers ?? [];
  const order = topSuppliersOrder ?? [];

  if (suppliers.length === 0 && order.length === 0) {
    return { suppliers: [], warning: null };
  }

  if (order.length === 0) {
    return {
      suppliers: [],
      warning: "Nedostaje autoritativan redosled dobavljaca (topSuppliersOrder).",
    };
  }

  const normalizedOrder = order.map((name) => name?.trim() ?? "");
  const duplicateOrderNames = findDuplicateNames(normalizedOrder.filter(Boolean));
  if (duplicateOrderNames.length > 0) {
    return {
      suppliers: [],
      warning: "Redosled dobavljaca sadrzi duplirana imena; koncentracija nije pouzdana.",
    };
  }

  const supplierByName = new Map<string, DailySalesSupplierHeader>();
  for (const supplier of suppliers) {
    const key = supplier.supplierName?.trim() ?? "";
    if (!key) {
      return {
        suppliers: [],
        warning: "Odgovor sadrzi dobavljaca bez imena; koncentracija nije pouzdana.",
      };
    }
    if (supplierByName.has(key)) {
      return {
        suppliers: [],
        warning: "Odgovor sadrzi vise dobavljaca sa istim imenom; koncentracija nije pouzdana.",
      };
    }
    supplierByName.set(key, supplier);
  }

  const ordered: DailySalesSupplierHeader[] = [];
  for (const name of normalizedOrder) {
    if (!name) {
      return {
        suppliers: [],
        warning: "Redosled dobavljaca sadrzi prazno ime; koncentracija nije pouzdana.",
      };
    }
    const supplier = supplierByName.get(name);
    if (!supplier) {
      return {
        suppliers: [],
        warning: `Redosled dobavljaca referencira nepoznatog dobavljaca "${name}".`,
      };
    }
    ordered.push(supplier);
  }

  if (ordered.length !== suppliers.length) {
    return {
      suppliers: [],
      warning: "Redosled dobavljaca nije uskladjen sa listom top dobavljaca.",
    };
  }

  return { suppliers: ordered, warning: null };
}
