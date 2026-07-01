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
  align?: "left" | "right";
}> = [
  { key: "supplierName", label: "Dobavljač" },
  { key: "revenue", label: "Prihod", align: "right" },
  { key: "units", label: "Komadi", align: "right" },
  { key: "fullPriceRevenueShare", label: "Udeo bez sniženja", align: "right" },
  { key: "fullPriceSellthrough", label: "Sell-through pre sniženja", align: "right" },
  { key: "preMarkdownMarginPct", label: "Marža", align: "right" },
  { key: "markdownRevenueShare", label: "Udeo sniženja", align: "right" },
  { key: "deadStockRate", label: "Dead stock", align: "right" },
  { key: "mlSupplierScore", label: "AI procena dobavljača", align: "right" },
  { key: "supplierQualityIndex", label: "Indeks kvaliteta", align: "right" },
  { key: "confidenceScore", label: "Pouzdanost", align: "right" },
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
    <div className="supplier-decision-panel rounded-[30px] border border-[var(--border-default)] bg-[linear-gradient(180deg,var(--surface-elevated)_0%,var(--surface-default)_100%)] p-5 shadow-[0_24px_70px_-56px_rgba(0,0,0,0.9)]">
      <div className="supplier-decision-panel-head flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="mb-1 text-[11px] font-extrabold uppercase tracking-[0.16em] text-muted">Supplier analytics</p>
          <h2 className="text-xl font-semibold tracking-[-0.03em] text-contrast">Rang lista dobavljača</h2>
          <p className="text-sm leading-relaxed text-secondary">Sortiranje i paginacija ostaju na backendu. Klik na red otvara detalje dobavljača i odluke.</p>
        </div>
        <div className="supplier-decision-table-summary inline-flex w-fit items-center gap-2 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] px-3 py-2 text-sm text-secondary">
          Prikazano <span className="font-semibold text-contrast">{items.length.toLocaleString("sr-RS")}</span> od <span className="font-semibold text-contrast">{totalCount.toLocaleString("sr-RS")}</span>
        </div>
      </div>

      <div className="my-4">
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

      <div className="supplier-decision-table-wrap overflow-hidden rounded-3xl border border-[var(--border-default)] bg-[var(--surface-darker)]">
        <div className="overflow-x-auto">
          <table className="supplier-decision-table min-w-full border-separate border-spacing-0 text-sm">
            <thead className="bg-[var(--surface-darker)] text-left text-secondary">
              <tr>
                {sortableColumns.map((column) => (
                  <th key={column.key} className={`px-4 py-3 text-xs font-black uppercase tracking-[0.08em] ${column.align === "right" ? "text-right" : ""}`}>
                    <button
                      type="button"
                      onClick={() => onSortChange(column.key)}
                      className={`inline-flex w-full items-center gap-1 text-xs font-black uppercase tracking-[0.08em] text-secondary transition hover:text-contrast ${column.align === "right" ? "justify-end text-right" : "justify-start text-left"}`}
                    >
                      <span>{column.label}</span>
                      <span aria-hidden="true">{sortIndicator(sortBy, sortDir, column.key)}</span>
                    </button>
                  </th>
                ))}
                <th className="px-4 py-3 text-xs font-black uppercase tracking-[0.08em]">Preporuka</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 8 }).map((_, index) => (
                  <tr key={`loading-${index}`}>
                    <td colSpan={12} className="border-t border-[var(--border-default)] px-4 py-3">
                      <div className="supplier-decision-skeleton-row h-5 rounded-full bg-[var(--surface-light)]" />
                    </td>
                  </tr>
                ))
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={12} className="border-t border-[var(--border-default)] px-4 py-12 text-center text-secondary">
                    Nema dobavljača za izabrane filtere.
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
                      className="supplier-decision-table-row cursor-pointer bg-[var(--surface-elevated)] text-[var(--text-primary)] transition hover:bg-[var(--surface-light)]"
                    >
                      <td className="border-t border-[var(--border-default)] px-4 py-3 font-semibold text-contrast">{item.supplierName}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatCurrency(item.revenue)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatInteger(item.units)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatRatioPercent(item.fullPriceRevenueShare)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatRatioPercent(item.fullPriceSellthrough)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatRatioPercent(item.preMarkdownMarginPct)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatRatioPercent(item.markdownRevenueShare)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatRatioPercent(item.deadStockRate)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatScore(item.mlSupplierScore)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{formatScore(item.supplierQualityIndex)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3 text-right text-secondary">{confidenceLabel(item.confidenceScore)}</td>
                      <td className="border-t border-[var(--border-default)] px-4 py-3">
                        <span className={`supplier-decision-pill tone-${recommendation.ton} inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold`}>
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
      </div>

      <div className="supplier-decision-pagination mt-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1 || loading}
          className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-secondary transition hover:border-[var(--info)] hover:bg-[var(--surface-light)] hover:text-contrast disabled:cursor-not-allowed disabled:opacity-50"
        >
          Prethodna
        </button>
        <span className="text-sm text-secondary">
          Strana <span className="font-semibold text-contrast">{page}</span> od <span className="font-semibold text-contrast">{totalPages}</span>
        </span>
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages || loading}
          className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-secondary transition hover:border-[var(--info)] hover:bg-[var(--surface-light)] hover:text-contrast disabled:cursor-not-allowed disabled:opacity-50"
        >
          Sledeća
        </button>
      </div>
    </div>
  );
}
