import type { VendorSalesNivelacijaDataQuality } from "../services/vendorSalesNivelacijaApi";

export type VendorSalesDataQualityProjection = {
  rawRows: number | null;
  deduplicatedRows: number | null;
  duplicateRowsRemoved: number | null;
  inactiveRows: number | null;
  unchangedPriceRows: number | null;
  analyzedRows: number | null;
  analyzedSharePercent: number | null;
  lowPostCoverageRows: number | null;
  avgCoveragePre30: number | null;
  avgCoveragePost30: number | null;
  isComplete: boolean;
};

function finiteQualityValue(value: number | null | undefined): number | null {
  return value != null && Number.isFinite(value) ? value : null;
}

function isOptionalCoverageCompatible(value: number | null | undefined): boolean {
  return value == null || (Number.isFinite(value) && value >= 0 && value <= 1);
}

export function projectVendorSalesDataQuality(
  dataQuality: VendorSalesNivelacijaDataQuality | null | undefined,
): VendorSalesDataQualityProjection {
  const rawRows = finiteQualityValue(dataQuality?.rawRows);
  const deduplicatedRows = finiteQualityValue(dataQuality?.deduplicatedRows);
  const duplicateRowsRemoved = finiteQualityValue(dataQuality?.duplicateRowsRemoved);
  const inactiveRows = finiteQualityValue(dataQuality?.inactiveRows);
  const unchangedPriceRows = finiteQualityValue(dataQuality?.unchangedPriceRows);
  const analyzedRows = finiteQualityValue(dataQuality?.analyzedRows);
  const analyzedSharePercent = finiteQualityValue(dataQuality?.analyzedSharePercent);
  const lowPostCoverageRows = finiteQualityValue(dataQuality?.lowPostCoverageRows);
  const avgCoveragePre30 = finiteQualityValue(dataQuality?.avgCoveragePre30);
  const avgCoveragePost30 = finiteQualityValue(dataQuality?.avgCoveragePost30);
  const requiredCounts = [
    rawRows,
    deduplicatedRows,
    duplicateRowsRemoved,
    inactiveRows,
    unchangedPriceRows,
    analyzedRows,
    lowPostCoverageRows,
  ];
  const countsAreCompatible = requiredCounts.every((value) => value != null && value >= 0);
  const shareIsCompatible = analyzedSharePercent != null && analyzedSharePercent >= 0 && analyzedSharePercent <= 100;
  const optionalCoverageIsCompatible =
    isOptionalCoverageCompatible(dataQuality?.avgCoveragePre30) &&
    isOptionalCoverageCompatible(dataQuality?.avgCoveragePost30);

  return {
    rawRows,
    deduplicatedRows,
    duplicateRowsRemoved,
    inactiveRows,
    unchangedPriceRows,
    analyzedRows,
    analyzedSharePercent,
    lowPostCoverageRows,
    avgCoveragePre30,
    avgCoveragePost30,
    isComplete: dataQuality != null && countsAreCompatible && shareIsCompatible && optionalCoverageIsCompatible,
  };
}
