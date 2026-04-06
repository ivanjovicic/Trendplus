import {
  CartesianGrid,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import type { QuadrantItem } from "../../services/supplierDecisionHubApi";
import {
  confidenceLabel,
  formatCurrency,
  formatPercentValue,
  formatRatioPercent,
  formatScore,
  getRecommendationMeta,
} from "./utils";

type SupplierDecisionQuadrantProps = {
  items: QuadrantItem[];
  loading?: boolean;
  onSelectSupplier: (supplierId: number) => void;
};

type QuadrantPoint = QuadrantItem & {
  fill: string;
};

const quadrantColors: Record<string, string> = {
  EXPAND: "var(--success, var(--theme-color-22c55e, #22c55e))",
  EXPAND_SELECTIVELY: "var(--success, var(--theme-color-84cc16, #84cc16))",
  HOLD: "var(--info, var(--theme-color-60a5fa, #60a5fa))",
  PRICE_NEGOTIATE: "var(--warning, var(--theme-color-f59e0b, #f59e0b))",
  ASSORTMENT_REDUCE: "var(--error, var(--theme-color-ef4444, #ef4444))",
  OOS_FALSE_NEGATIVE: "var(--error, var(--theme-color-fb7185, #fb7185))",
  REVIEW_QUALITY: "var(--warning, var(--theme-color-f97316, #f97316))",
};

function quadrantColor(code: string) {
  return quadrantColors[code] ?? "var(--text-muted, var(--theme-color-94a3b8, #94a3b8))";
}

export default function SupplierDecisionQuadrant({
  items,
  loading = false,
  onSelectSupplier,
}: SupplierDecisionQuadrantProps) {
  const data: QuadrantPoint[] = items.flatMap((item) => {
    const markdownDependency = Number(item.markdownDependency);
    const fullPriceSellthrough = Number(item.fullPriceSellthrough);
    const revenue = Number(item.revenue);

    if (
      !Number.isFinite(markdownDependency) ||
      !Number.isFinite(fullPriceSellthrough) ||
      !Number.isFinite(revenue)
    ) {
      return [];
    }

    return [
      {
        ...item,
        markdownDependency,
        fullPriceSellthrough,
        revenue,
        fill: quadrantColor(item.recommendationCode),
      },
    ];
  });

  return (
    <div className="supplier-decision-panel">
      <div className="supplier-decision-panel-head">
        <div>
          <h2>Kvadrant odluka</h2>
          <p>
            X osa: Zavisnost od sniženja. Y osa: Sell-through bez sniženja. Veličina
            kruga prati prihod.
          </p>
        </div>
        <div className="supplier-decision-inline-legend">
          {[
            "EXPAND",
            "EXPAND_SELECTIVELY",
            "PRICE_NEGOTIATE",
            "ASSORTMENT_REDUCE",
            "OOS_FALSE_NEGATIVE",
            "REVIEW_QUALITY",
            "HOLD",
          ].map((code) => (
            <span key={code} className="supplier-decision-legend-item">
              <span
                className="supplier-decision-legend-dot"
                style={{ backgroundColor: quadrantColor(code) }}
              />
              {getRecommendationMeta(code).label}
            </span>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="supplier-decision-empty">Učitavanje kvadranta...</div>
      ) : data.length === 0 ? (
        <div className="supplier-decision-empty">Nema dobavljača za izabrane filtere.</div>
      ) : (
        <div className="supplier-decision-chart-shell">
          <ResponsiveContainer width="100%" height={360}>
            <ScatterChart margin={{ top: 24, right: 24, bottom: 24, left: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--theme-color-rgba-148-163-184-0p16, var(--theme-color-rgba-148-163-184-0p16, var(--theme-color-rgba-148-163-184-0p16, rgba(148, 163, 184, 0.16))))" />
              <XAxis
                type="number"
                dataKey="markdownDependency"
                name="Zavisnost od sniženja"
                domain={[0, 100]}
                tickFormatter={(value) => formatPercentValue(Number(value), 0)}
                stroke="var(--border-muted, var(--theme-color-9fb4d8, #9fb4d8))"
              />
              <YAxis
                type="number"
                dataKey="fullPriceSellthrough"
                name="Sell-through bez sniženja"
                domain={[0, 1]}
                tickFormatter={(value) => formatRatioPercent(Number(value), 0)}
                stroke="var(--border-muted, var(--theme-color-9fb4d8, #9fb4d8))"
              />
              <ZAxis type="number" dataKey="revenue" range={[120, 900]} />
              <Tooltip
                cursor={{ strokeDasharray: "4 4" }}
                content={({ active, payload }) => {
                  const point = payload?.[0]?.payload as QuadrantPoint | undefined;
                  if (!active || !point) return null;
                  const recommendation = getRecommendationMeta(point.recommendationCode);
                  return (
                    <div className="supplier-decision-tooltip">
                      <strong>{point.supplierName}</strong>
                      <span>Prihod: {formatCurrency(point.revenue)}</span>
                      <span>
                        Zavisnost od sniženja:{" "}
                        {formatPercentValue(point.markdownDependency, 1)}
                      </span>
                      <span>
                        Sell-through bez sniženja:{" "}
                        {formatRatioPercent(point.fullPriceSellthrough, 1)}
                      </span>
                      <span>Indeks kvaliteta: {formatScore(point.supplierQualityIndex)}</span>
                      <span>
                        Preporuka: {recommendation.label} · {confidenceLabel(point.confidenceScore)}
                      </span>
                    </div>
                  );
                }}
              />
              <Scatter
                data={data}
                fill="var(--info, var(--theme-color-60a5fa, #60a5fa))"
                onClick={(event: unknown) => {
                  const point = (event as { payload?: QuadrantPoint } | null)?.payload;
                  if (point?.supplierId) {
                    onSelectSupplier(point.supplierId);
                  }
                }}
              />
            </ScatterChart>
          </ResponsiveContainer>
          <div className="supplier-decision-chart-note">
            Klik na krug otvara detalje dobavljača.
          </div>
        </div>
      )}
    </div>
  );
}
