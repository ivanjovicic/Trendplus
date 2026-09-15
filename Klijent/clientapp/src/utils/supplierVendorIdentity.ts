export type SupplierVendorIdentitySource = {
  vendorId: number | null;
  vendorName: string;
};

export function normalizeSupplierVendorName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

export function buildSupplierVendorKey(
  vendor: SupplierVendorIdentitySource,
  rowIndex: number,
): string {
  if (vendor.vendorId != null) return `id:${vendor.vendorId}`;
  return `row:${rowIndex}`;
}

export function buildSupplierVendorDetailRecordId(
  vendor: SupplierVendorIdentitySource,
  vendorRowKey: string,
): string {
  if (vendor.vendorId != null) return String(vendor.vendorId);
  return vendorRowKey;
}

export function resolveSupplierArticleVendorKey(
  article: SupplierVendorIdentitySource,
  articleIndex: number,
  vendorStats: SupplierVendorIdentitySource[],
): string {
  if (article.vendorId != null) return `id:${article.vendorId}`;

  const normalizedName = normalizeSupplierVendorName(article.vendorName);
  const matchingIndices = vendorStats
    .map((vendor, index) => ({ vendor, index }))
    .filter(({ vendor }) => vendor.vendorId == null && normalizeSupplierVendorName(vendor.vendorName) === normalizedName)
    .map(({ index }) => index);

  if (matchingIndices.length === 1) {
    return buildSupplierVendorKey(vendorStats[matchingIndices[0]], matchingIndices[0]);
  }

  return `article:${articleIndex}`;
}
