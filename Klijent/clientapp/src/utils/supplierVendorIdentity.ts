export type SupplierVendorIdentitySource = {
  vendorId: number | null;
  vendorName: string;
};

export function normalizeSupplierVendorName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

export function buildSupplierVendorKeys(vendors: SupplierVendorIdentitySource[]): string[] {
  const idCounts = new Map<number, number>();
  for (const vendor of vendors) {
    if (vendor.vendorId != null) {
      idCounts.set(vendor.vendorId, (idCounts.get(vendor.vendorId) ?? 0) + 1);
    }
  }

  return vendors.map((vendor, rowIndex) => {
    if (vendor.vendorId != null && idCounts.get(vendor.vendorId) === 1) {
      return `id:${vendor.vendorId}`;
    }
    return `row:${rowIndex}`;
  });
}

export function buildSupplierVendorKey(
  vendor: SupplierVendorIdentitySource,
  rowIndex: number,
  vendors: SupplierVendorIdentitySource[] = [],
): string {
  if (vendors.length > 0) {
    return buildSupplierVendorKeys(vendors)[rowIndex] ?? `row:${rowIndex}`;
  }
  if (vendor.vendorId != null) return `id:${vendor.vendorId}`;
  return `row:${rowIndex}`;
}

export function buildSupplierVendorDetailRecordId(
  vendor: SupplierVendorIdentitySource,
  vendorRowKey: string,
): string {
  if (vendor.vendorId != null && vendorRowKey === `id:${vendor.vendorId}`) {
    return String(vendor.vendorId);
  }
  return vendorRowKey;
}

export function resolveSupplierArticleVendorKey(
  article: SupplierVendorIdentitySource,
  articleIndex: number,
  vendorStats: SupplierVendorIdentitySource[],
  vendorKeys: string[] = buildSupplierVendorKeys(vendorStats),
): string {
  if (article.vendorId != null) {
    const matchingIndices = vendorStats
      .map((vendor, index) => ({ vendor, index }))
      .filter(({ vendor }) => vendor.vendorId === article.vendorId)
      .map(({ index }) => index);

    if (matchingIndices.length === 1) {
      return vendorKeys[matchingIndices[0]] ?? `id:${article.vendorId}`;
    }
    if (matchingIndices.length === 0) {
      return `id:${article.vendorId}`;
    }
    return `article:${articleIndex}`;
  }

  const normalizedName = normalizeSupplierVendorName(article.vendorName);
  const matchingIndices = vendorStats
    .map((vendor, index) => ({ vendor, index }))
    .filter(({ vendor }) => vendor.vendorId == null && normalizeSupplierVendorName(vendor.vendorName) === normalizedName)
    .map(({ index }) => index);

  if (matchingIndices.length === 1) {
    return vendorKeys[matchingIndices[0]] ?? `row:${matchingIndices[0]}`;
  }

  return `article:${articleIndex}`;
}
