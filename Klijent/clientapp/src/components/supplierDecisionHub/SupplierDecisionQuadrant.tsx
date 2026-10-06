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
import { AnalyticsChartAccessibility, describeChartProjection } from "../analytics/AnalyticsChartAccessibility";
import type { QuadrantItem } from "../../services/supplierDecisionHubApi";
import { dataQualityStatusLabel, formatReliability } from "../../utils/analyticsQuality";
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
          <AnalyticsChartAccessibility title="Kvadrant dobavljača" summary={describeChartProjection(data, "po dobavljačima", ['zavisnost od sniženja', 'sell-through bez sniženja', 'prihod'])} tableTargetId="supplier-decision-quadrant-table">
          <ResponsiveContainer width="100%" height={360}>
            <ScatterChart margin={{ top: 24, right: 24, bottom: 24, left: 8 }} accessibilityLayer>
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
                      <span>Pouzdanost signala: {formatReliability(point.reliabilityPct, 0)}</span>
                      <span>Kvalitet podataka: {dataQualityStatusLabel(point.dataQualityStatus)}</span>
                      <span>{point.statusReason || "Nema dodatnog obrazloženja."}</span>
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
          </ResponsiveContainer></AnalyticsChartAccessibility>
          <details className="mt-3 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3 text-[var(--text-primary)]">
            <summary className="cursor-pointer font-medium focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2">Tabelarni prikaz dobavljača u kvadrantu</summary>
            <div className="mt-3 overflow-x-auto">
              <table id="supplier-decision-quadrant-table" className="min-w-full border-collapse text-sm">
                <caption className="sr-only">Vrednosti istih dobavljača prikazanih u kvadrantu odluka</caption>
                <thead>
                  <tr>
                    <th scope="col" className="px-3 py-2 text-left">Dobavljač</th>
                    <th scope="col" className="px-3 py-2 text-left">Prihod</th>
                    <th scope="col" className="px-3 py-2 text-left">Zavisnost od sniženja</th>
                    <th scope="col" className="px-3 py-2 text-left">Sell-through bez sniženja</th>
                    <th scope="col" className="px-3 py-2 text-left">Preporuka</th>
                    <th scope="col" className="px-3 py-2 text-left">Pouzdanost</th>
                    <th scope="col" className="px-3 py-2 text-left">Kvalitet podataka</th>
                    <th scope="col" className="px-3 py-2 text-left">Razlog</th>
                  </tr>
                </thead>
                <tbody>
                  {data.map((point) => {
                    const recommendation = getRecommendationMeta(point.recommendationCode);
                    return (
                      <tr key={point.supplierId}>
                        <th scope="row" className="px-3 py-2 text-left">
                          <button type="button" className="underline decoration-dotted underline-offset-2 focus-visible:outline focus-visible:outline-2" onClick={() => onSelectSupplier(point.supplierId)}>
                            Otvori detalj za {point.supplierName || "nepoznatog dobavljača"}
                          </button>
                        </th>
                        <td className="px-3 py-2">{formatCurrency(point.revenue)}</td>
                        <td className="px-3 py-2">{formatPercentValue(point.markdownDependency, 1)}</td>
                        <td className="px-3 py-2">{formatRatioPercent(point.fullPriceSellthrough, 1)}</td>
                        <td className="px-3 py-2">{recommendation.label} · {confidenceLabel(point.confidenceScore)}</td>
                        <td className="px-3 py-2">{formatReliability(point.reliabilityPct, 0)}</td>
                        <td className="px-3 py-2">{dataQualityStatusLabel(point.dataQualityStatus)}</td>
                        <td className="px-3 py-2">{point.statusReason || "Nema dodatnog obrazloženja."}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </details>
          <div className="supplier-decision-chart-note">
            Klik na krug otvara detalje dobavljača.
          </div>
        </div>
      )}
    </div>
  );
}
