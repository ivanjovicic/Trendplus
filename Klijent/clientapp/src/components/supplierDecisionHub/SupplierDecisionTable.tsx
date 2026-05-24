import type {
  RankingItem,
  SupplierDecisionHubSortField,
} from "../../services/supplierDecisionHubApi";
import AnalyticsTableToolbar from "../analytics/AnalyticsTableToolbar";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../../types/analyticsTable";
import {
  confidenceLabel,
  formatCurrency,
  formatInteger,
  formatRatioPercent,
  formatScore,
  getRecommendationMeta,
} from "./utils";

type SupplierDecisionTableProps = {
  items: RankingItem[];
  columns: AnalyticsTableColumn<RankingItem>[];
  analyticsFilters: AnalyticsNamedValue[];
  analyticsMetadata: AnalyticsNamedValue[];
  loading?: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  sortBy: SupplierDecisionHubSortField;
  sortDir: "asc" | "desc";
  onPageChange: (page: number) => void;
  onSortChange: (sortBy: SupplierDecisionHubSortField) => void;
  onSelectSupplier: (supplierId: number) => void;
  onOpenDetail: (item: RankingItem) => void;
};

const sortableColumns: Array<{
  key: SupplierDecisionHubSortField;
  label: string;
}> = [
  { key: "supplierName", label: "Dobavljač" },
  { key: "revenue", label: "Prihod" },
  { key: "units", label: "Komadi" },
  { key: "fullPriceRevenueShare", label: "Udeo bez sniženja" },
  { key: "fullPriceSellthrough", label: "Sell-through pre sniženja" },
  { key: "preMarkdownMarginPct", label: "Marža" },
  { key: "markdownRevenueShare", label: "Udeo sniženja" },
  { key: "deadStockRate", label: "Dead stock" },
  { key: "mlSupplierScore", label: "AI procena dobavljača" },
  { key: "supplierQualityIndex", label: "Indeks kvaliteta" },
  { key: "confidenceScore", label: "Pouzdanost" },
];

function sortIndicator(
  currentSortBy: SupplierDecisionHubSortField,
  currentSortDir: "asc" | "desc",
  nextKey: SupplierDecisionHubSortField
) {
  if (currentSortBy !== nextKey) return "↕";
  return currentSortDir === "asc" ? "↑" : "↓";
}

export default function SupplierDecisionTable({
  items,
  columns,
  analyticsFilters,
  analyticsMetadata,
  loading = false,
  page,
  pageSize,
  totalCount,
  sortBy,
  sortDir,
  onPageChange,
  onSortChange,
  onSelectSupplier,
  onOpenDetail,
}: SupplierDecisionTableProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="supplier-decision-panel">
      <div className="supplier-decision-panel-head">
        <div>
          <h2>Rang lista dobavljača</h2>
          <p>Sortiranje i paginacija ostaju na backendu. Klik na red otvara detalje.</p>
        </div>
        <div className="supplier-decision-table-summary">
          Prikazano: {items.length} / {totalCount}
        </div>
      </div>

      <div className="mb-3">
        <AnalyticsTableToolbar
          tableKey="supplier-decision-hub"
          tableTitle="Supplier Decision Hub - rangiranje dobavljača"
          columns={columns}
          rows={items}
          filters={analyticsFilters}
          metadata={analyticsMetadata}
          defaultOrientation="landscape"
        />
      </div>

      <div className="supplier-decision-table-wrap">
        <table className="supplier-decision-table">
          <thead>
            <tr>
              {sortableColumns.map((column) => (
                <th key={column.key}>
                  <button type="button" onClick={() => onSortChange(column.key)}>
                    <span>{column.label}</span>
                    <span>{sortIndicator(sortBy, sortDir, column.key)}</span>
                  </button>
                </th>
              ))}
              <th>Preporuka</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 8 }).map((_, index) => (
                <tr key={`loading-${index}`}>
                  <td colSpan={12}>
                    <div className="supplier-decision-skeleton-row" />
                  </td>
                </tr>
              ))
            ) : items.length === 0 ? (
              <tr>
                <td colSpan={12}>
                  <div className="supplier-decision-empty">Nema dobavljača za izabrane filtere.</div>
                </td>
              </tr>
            ) : (
              items.map((item) => {
                const recommendation = getRecommendationMeta(item.recommendationCode);
                return (
                  <tr
                    key={item.supplierId}
                    onClick={() => {
                      onSelectSupplier(item.supplierId);
                      onOpenDetail(item);
                    }}
                    className="supplier-decision-table-row"
                  >
                    <td>{item.supplierName}</td>
                    <td>{formatCurrency(item.revenue)}</td>
                    <td>{formatInteger(item.units)}</td>
                    <td>{formatRatioPercent(item.fullPriceRevenueShare)}</td>
                    <td>{formatRatioPercent(item.fullPriceSellthrough)}</td>
                    <td>{formatRatioPercent(item.preMarkdownMarginPct)}</td>
                    <td>{formatRatioPercent(item.markdownRevenueShare)}</td>
                    <td>{formatRatioPercent(item.deadStockRate)}</td>
                    <td>{formatScore(item.mlSupplierScore)}</td>
                    <td>{formatScore(item.supplierQualityIndex)}</td>
                    <td>{confidenceLabel(item.confidenceScore)}</td>
                    <td>
                      <span className={`supplier-decision-pill tone-${recommendation.ton}`}>
                        {recommendation.label}
                      </span>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="supplier-decision-pagination">
        <button type="button" onClick={() => onPageChange(page - 1)} disabled={page <= 1 || loading}>
          Prethodna
        </button>
        <span>
          Strana {page} od {totalPages}
        </span>
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages || loading}
        >
          Sledeća
        </button>
      </div>
    </div>
  );
}
