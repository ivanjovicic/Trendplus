import type { VendorSalesNivelacijaDataQuality } from "../services/vendorSalesNivelacijaApi";

export type VendorSalesDataQualityProjection = {
  rawRows: number | null;
  deduplicatedRows: number | null;
  duplicateRowsRemoved: number | null;
  cohortRows: number | null;
  cohortRowsExcluded: number | null;
  returnedRows: number | null;
  truncatedRows: number | null;
  comparableRows: number | null;
  comparableSharePercent: number | null;
  isDetailTruncated: boolean | null;
  cohortPolicy: string | null;
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
  const cohortRows = finiteQualityValue(dataQuality?.cohortRows);
  const cohortRowsExcluded = finiteQualityValue(dataQuality?.cohortRowsExcluded);
  const returnedRows = finiteQualityValue(dataQuality?.returnedRows);
  const truncatedRows = finiteQualityValue(dataQuality?.truncatedRows);
  const comparableRows = finiteQualityValue(dataQuality?.comparableRows);
  const comparableSharePercent = finiteQualityValue(dataQuality?.comparableSharePercent);
  const isDetailTruncated = dataQuality?.isDetailTruncated === true ? true : dataQuality?.isDetailTruncated === false ? false : null;
  const cohortPolicy = typeof dataQuality?.cohortPolicy === "string" ? dataQuality.cohortPolicy : null;
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
  const optionalCohortIsCompatible =
    (dataQuality?.cohortRows == null || (cohortRows != null && cohortRows >= 0)) &&
    (dataQuality?.cohortRowsExcluded == null || (cohortRowsExcluded != null && cohortRowsExcluded >= 0)) &&
    (dataQuality?.returnedRows == null || (returnedRows != null && returnedRows >= 0)) &&
    (dataQuality?.truncatedRows == null || (truncatedRows != null && truncatedRows >= 0)) &&
    (dataQuality?.comparableRows == null || (comparableRows != null && comparableRows >= 0)) &&
    (dataQuality?.comparableSharePercent == null || (comparableSharePercent != null && comparableSharePercent >= 0 && comparableSharePercent <= 100)) &&
    (dataQuality?.isDetailTruncated == null || isDetailTruncated != null) &&
    (dataQuality?.cohortPolicy == null || cohortPolicy != null);

  return {
    rawRows,
    deduplicatedRows,
    duplicateRowsRemoved,
    cohortRows,
    cohortRowsExcluded,
    returnedRows,
    truncatedRows,
    comparableRows,
    comparableSharePercent,
    isDetailTruncated,
    cohortPolicy,
    inactiveRows,
    unchangedPriceRows,
    analyzedRows,
    analyzedSharePercent,
    lowPostCoverageRows,
    avgCoveragePre30,
    avgCoveragePost30,
    isComplete: dataQuality != null && countsAreCompatible && shareIsCompatible && optionalCoverageIsCompatible && optionalCohortIsCompatible,
  };
}
