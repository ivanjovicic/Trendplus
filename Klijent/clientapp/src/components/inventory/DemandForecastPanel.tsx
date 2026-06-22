import { TrendingDown, TrendingUp } from "lucide-react";
import type { ForecastDto, ForecastRowDto, StoreOption } from "../../types/analytics";
import { TONE } from "./toneMap";
import type { InventoryRow } from "./types";

type DemandForecastPanelProps = {
  forecast: ForecastDto | null;
  forecastLoading: boolean;
  forecastError: string | null;
  rows: InventoryRow[];
  stores: StoreOption[];
  oosThreshold: number;
  overstockThreshold: number;
  oosDisplayCount: number;
  overstockDisplayCount: number;
  onSuggestRestock: (item: ForecastRowDto) => void;
};

export function DemandForecastPanel({
  forecast,
  forecastLoading,
  forecastError,
  rows,
  stores,
  oosThreshold,
  overstockThreshold,
  oosDisplayCount,
  overstockDisplayCount,
  onSuggestRestock,
}: DemandForecastPanelProps) {
  const highOosItems = (forecast?.items ?? [])
    .filter((item) => item.probabilityOfOOSIn7d > oosThreshold)
    .sort((left, right) => right.probabilityOfOOSIn7d - left.probabilityOfOOSIn7d)
    .slice(0, oosDisplayCount);

  const overstockItems = (forecast?.items ?? [])
    .filter((item) => item.overstockRisk > overstockThreshold)
    .sort((left, right) => right.overstockRisk - left.overstockRisk)
    .slice(0, overstockDisplayCount);

  const warningText = forecast?.warning;

  return (
    <section className="rounded-[28px] border border-border bg-surface p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border p-2.5 bg-surface-elevated text-info">
            <TrendingDown size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-foreground">Procena potražnje i signal rizika nestanka zaliha</h2>
            <p className="text-sm text-muted">Procena potražnje po SKU i veličini. OOS i višak zalihe su signalni indikatori, ne automatski nalozi za naručivanje.</p>
          </div>
        </div>
        <div className="rounded-full border border-border bg-surface px-3 py-1 text-xs font-semibold text-muted">
          {forecastLoading ? "Učitavam..." : `${forecast?.totalCount ?? 0} SKU u prognozi`}
        </div>
      </div>

      {forecastError ? (
        <div className="mt-4 rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]">
          {forecastError}
        </div>
      ) : !forecast?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          {forecastLoading ? "Učitavam prognozu..." : "Prognoza trenutno nije dostupna. Snapshot tabela je prazna."}
          {warningText ? <div className="mt-2 text-xs text-warning">{warningText}</div> : null}
        </div>
      ) : (forecast.items ?? []).length === 0 ? (
        <div className="mt-4 rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Nema podataka za prognozu potražnje za trenutne filtere.
        </div>
      ) : (
        <div className="mt-4 grid gap-5 xl:grid-cols-2">
          <div className="rounded-2xl border border-border bg-surface p-4">
            <h3 className="flex items-center gap-2 text-sm font-semibold text-foreground">
              <TrendingDown size={14} className="text-warning" />
              Najveći rizik nestanka zaliha u 7 dana
            </h3>
            <div className="mt-3 space-y-2">
              {highOosItems.map((item) => {
                const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                const store = stores.find((entry) => entry.storeId === item.storeId)?.storeName ?? `Objekat #${item.storeId}`;
                const tone = item.probabilityOfOOSIn7d > 0.7 ? TONE.severity.critical : item.probabilityOfOOSIn7d > 0.4 ? TONE.severity.warning : TONE.severity.info;

                return (
                  <div key={`${item.skuId}-${item.storeId}-${item.sizeCode}`} className="flex items-start justify-between gap-3 rounded-xl border border-border bg-surface p-3">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-foreground">{name}</div>
                      <div className="truncate text-xs text-muted">{store} | vel. {item.sizeCode}</div>
                      <div className="mt-1 text-xs text-muted">{item.explanation}</div>
                    </div>
                    <div className="shrink-0 text-right">
                      <div className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${tone}`}>
                        {Math.round(item.probabilityOfOOSIn7d * 100)}% OOS
                      </div>
                      <div className="mt-1 text-[11px] font-medium text-muted">
                        {item.probabilityOfOOSIn7d > 0.7 ? "Status: kritično" : item.probabilityOfOOSIn7d > 0.4 ? "Status: upozorenje" : "Status: stabilno"}
                      </div>
                      <div className="mt-2 h-1.5 w-24 overflow-hidden rounded-full bg-surface-light">
                        <div className="h-full rounded-full bg-gradient-to-r from-success via-warning to-danger" style={{ width: `${Math.max(0, Math.min(100, item.probabilityOfOOSIn7d * 100))}%` }} />
                      </div>
                      <div className="mt-1 text-xs text-muted">7d: {item.forecast7d.toFixed(1)}</div>
                      <button type="button" aria-label={`Predloži signal dopune za SKU ${item.skuId} veličinu ${item.sizeCode}`} onClick={() => onSuggestRestock(item)} className="mt-2 rounded-lg border px-2.5 py-1 text-[11px] font-semibold text-success transition">
                        Predlog dopune (signal)
                      </button>
                    </div>
                  </div>
                );
              })}
              {highOosItems.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-6 text-center text-sm text-[var(--text-primary)]">Nema visokog OOS rizika za trenutne filtere.</div>
              ) : null}
            </div>
          </div>

          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
            <h3 className="flex items-center gap-2 text-sm font-semibold text-white">
              <TrendingUp size={14} className="text-[var(--text-primary)]" />
              Rizik prevelike zalihe (28 dana)
            </h3>
            <div className="mt-3 space-y-2">
              {overstockItems.map((item) => {
                const name = rows.find((row) => row.id === item.skuId)?.naziv ?? `SKU #${item.skuId}`;
                const store = stores.find((entry) => entry.storeId === item.storeId)?.storeName ?? `Objekat #${item.storeId}`;

                return (
                  <div key={`${item.skuId}-${item.storeId}-${item.sizeCode}`} className="flex items-start justify-between gap-3 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2">
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold text-foreground">{name}</div>
                      <div className="truncate text-xs text-[var(--text-primary)]">{store} | vel. {item.sizeCode}</div>
                    </div>
                    <div className="shrink-0 text-right">
                      <div className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">
                        {Math.round(item.overstockRisk * 100)}% višak
                      </div>
                      <div className="mt-1 text-[11px] font-medium text-muted">
                        {item.overstockRisk > 0.7 ? "Status: kritično" : item.overstockRisk > 0.4 ? "Status: upozorenje" : "Status: stabilno"}
                      </div>
                      <div className="mt-1 text-xs text-[var(--text-primary)]">28d: {item.forecast28d.toFixed(1)}</div>
                    </div>
                  </div>
                );
              })}
              {overstockItems.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-6 text-center text-sm text-[var(--text-primary)]">Nema signala za višak zaliha za trenutne filtere.</div>
              ) : null}
            </div>
          </div>
        </div>
      )}
      <p className="mt-3 text-xs text-muted">Predlozi dopune su procene zasnovane na forecast signalu. Potvrdite stock baseline i operativni kontekst pre naručivanja.</p>
      {warningText ? <p className="mt-3 text-xs text-warning">Napomena: {warningText}</p> : null}
    </section>
  );
}

