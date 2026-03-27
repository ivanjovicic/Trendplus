import { Clock3 } from "lucide-react";
import type { InventoryInsightItem, InventoryInsights, StoreOption, SupplierFilterOption } from "../../types/analytics";
import { buildRowFromInsightItem, formatCurrency, formatNumber, formatPercent, getAbcTone, getAgingTone } from "./inventoryUtils";
import type { InventoryRow } from "./types";

type InventoryInsightPanelsProps = {
  insights: InventoryInsights | null;
  insightsLoading: boolean;
  stores: StoreOption[];
  suppliers: SupplierFilterOption[];
  rows: InventoryRow[];
  onOpenDetail: (row: InventoryRow) => void;
};

function resolveInsightRow(item: InventoryInsightItem, rows: InventoryRow[], stores: StoreOption[], suppliers: SupplierFilterOption[]) {
  return rows.find((row) => row.id === item.id) ?? buildRowFromInsightItem(item, stores, suppliers);
}

export function InventoryInsightPanels({
  insights,
  insightsLoading,
  stores,
  suppliers,
  rows,
  onOpenDetail,
}: InventoryInsightPanelsProps) {
  const agingBuckets = insights?.aging ?? [];
  const abcBuckets = insights?.abc ?? [];
  const agedItems = insights?.topAgedItems ?? [];
  const capitalLockedItems = insights?.topCapitalLockedItems ?? [];
  const staleBucket = agingBuckets.find((bucket) => bucket.bucketKey === "90+");
  const classABucket = abcBuckets.find((bucket) => bucket.bucketKey === "A");

  return (
    <section className="grid gap-5 xl:grid-cols-[1.05fr_0.95fr]">
      <div className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
        <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">Aging i obrt fonda robe</h2>
            <p className="text-sm text-[var(--text-primary)]">Dani bez kretanja su racunati po poslednjem movement-u, uz fallback na poslednje azuriranje artikla.</p>
          </div>
          <div className="rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-1 text-xs font-semibold text-[var(--text-primary)]">
            {insightsLoading ? "Ucitavanje aging analitike..." : `${formatNumber(staleBucket?.itemCount ?? 0)} artikala je u 90+ dana`}
          </div>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {agingBuckets.length === 0 ? (
            <div className="col-span-full rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">Aging analitika nije dostupna za trenutne filtere.</div>
          ) : agingBuckets.map((bucket) => (
            <article key={bucket.bucketKey} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
              <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAgingTone(bucket.bucketKey)}`}>{bucket.label}</div>
              <div className="mt-4 text-2xl font-semibold text-white">{formatNumber(bucket.itemCount)}</div>
              <div className="mt-2 text-sm text-[var(--text-primary)]">{formatNumber(bucket.totalUnits)} komada | {formatCurrency(bucket.estimatedValue)}</div>
            </article>
          ))}
        </div>

        <div className="mt-5 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
          <div className="flex items-center gap-2 text-sm font-semibold text-white">
            <Clock3 size={16} className="text-[var(--text-primary)]" />
            Najstariji artikli u filtriranom skupu
          </div>
          <div className="mt-3 space-y-3">
            {agedItems.length === 0 ? <div className="text-sm text-[var(--text-primary)]">Nema artikala za aging ranking.</div> : agedItems.map((item) => (
              <button key={`aged-${item.id}`} type="button" onClick={() => onOpenDetail(resolveInsightRow(item, rows, stores, suppliers))} className="flex w-full items-center justify-between rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-3 text-left transition hover:border-[var(--border-default)]">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-white">{item.naziv}</div>
                  <div className="truncate text-xs text-[var(--text-primary)]">{item.plu ?? "Bez PLU"} | {item.supplierName ?? "Nerasporedjen dobavljac"}</div>
                </div>
                <div className="text-right">
                  <div className="text-sm font-semibold text-[var(--text-primary)]">{formatNumber(item.daysSinceMovement)} dana</div>
                  <div className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[11px] font-semibold ${getAgingTone(item.agingBucket)}`}>{item.agingLabel}</div>
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="rounded-[28px] border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
        <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-white">ABC segmentacija kapitala</h2>
            <p className="text-sm text-[var(--text-primary)]">Klasa A predstavlja artikle koji nose najveci deo nabavne vrednosti filtrirane zalihe.</p>
          </div>
          <div className="rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-1 text-xs font-semibold text-[var(--text-primary)]">
            {insightsLoading ? "Ucitavanje ABC klase..." : `${formatNumber(classABucket?.itemCount ?? 0)} artikala u klasi A`}
          </div>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-3">
          {abcBuckets.length === 0 ? (
            <div className="col-span-full rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--text-primary)]">ABC raspodela nije dostupna za trenutne filtere.</div>
          ) : abcBuckets.map((bucket) => (
            <article key={bucket.bucketKey} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
              <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${getAbcTone(bucket.bucketKey)}`}>{bucket.label}</div>
              <div className="mt-4 text-2xl font-semibold text-white">{formatPercent(bucket.valueSharePct)}</div>
              <div className="mt-2 text-sm text-[var(--text-primary)]">{formatNumber(bucket.itemCount)} artikala | {formatCurrency(bucket.estimatedValue)}</div>
            </article>
          ))}
        </div>

        <div className="mt-5 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
          <div className="text-sm font-semibold text-white">Kapital najvise vezan u ovim artiklima</div>
          <div className="mt-3 space-y-3">
            {capitalLockedItems.length === 0 ? <div className="text-sm text-[var(--text-primary)]">Nema artikala za ABC ranking.</div> : capitalLockedItems.map((item) => (
              <button key={`capital-${item.id}`} type="button" onClick={() => onOpenDetail(resolveInsightRow(item, rows, stores, suppliers))} className="flex w-full items-center justify-between rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-3 text-left transition hover:border-[var(--border-default)]">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-white">{item.naziv}</div>
                  <div className="truncate text-xs text-[var(--text-primary)]">{item.storeName ?? "Sve lokacije"} | {item.quantity} kom</div>
                </div>
                <div className="text-right">
                  <div className="text-sm font-semibold text-[var(--text-primary)]">{formatCurrency(item.estimatedValue)}</div>
                  <div className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[11px] font-semibold ${getAbcTone(item.abcClass)}`}>Klasa {item.abcClass}</div>
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

