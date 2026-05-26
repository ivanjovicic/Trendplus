import { Bar, CartesianGrid, Cell, ComposedChart, Legend, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { SizeCurvePointDto } from "../../types/analytics";

type SizeCurveVisualizationProps = {
  items: SizeCurvePointDto[];
  cardLimit?: number;
};

export function SizeCurveVisualization({ items, cardLimit = 8 }: SizeCurveVisualizationProps) {
  const missingCoreCount = items.filter((item) => item.isCoreSizeMissing).length;
  const deadSizeCount = items.filter((item) => item.isDeadSize).length;
  const hasBrokenRun = items.some((item) => item.brokenRun);
  const chartData = items.map((item) => ({ name: item.sizeCode, actual: +(item.actualSizeShare * 100).toFixed(1), ideal: +(item.idealSizeShare * 100).toFixed(1), deviation: +(item.deviationPct * 100).toFixed(1), isDeadSize: item.isDeadSize, isCoreSizeMissing: item.isCoreSizeMissing }));

  return (
    <div className="mt-4">
      <div className="mb-3 flex flex-wrap gap-2">
        {missingCoreCount > 0 ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">{missingCoreCount} core size nedostaju</span> : null}
        {deadSizeCount > 0 ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">{deadSizeCount} mrtve velicine</span> : null}
        {hasBrokenRun ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">Broken run detektovan</span> : null}
      </div>

      <div className="h-[240px]">
        <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={180}>
          <ComposedChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke={"var(--border-default, var(--theme-color-233042, #233042))"} />
              <XAxis dataKey="name" tick={{ fill: "var(--text-muted, var(--theme-color-92a4bf, #92a4bf))", fontSize: 12 }} />
              <YAxis tick={{ fill: "var(--text-muted, var(--theme-color-92a4bf, #92a4bf))", fontSize: 12 }} unit="%" />
              <Tooltip
                contentStyle={{ background: "var(--surface-default)", border: "1px solid var(--border-default)", color: "var(--text-primary)" }}
                formatter={(value: number | string | undefined) => `${value ?? 0}%`}
                labelFormatter={(label, payload) => {
                  const point = payload?.[0]?.payload as { actual?: number; ideal?: number; deviation?: number } | undefined;
                  return point ? `Vel. ${label} | stvarno ${point.actual ?? 0}% | idealno ${point.ideal ?? 0}% | odstupanje ${point.deviation ?? 0}pp` : `Vel. ${label}`;
                }}
              />
              <Legend wrapperStyle={{ color: "var(--text-primary, var(--theme-color-dbe6fb, #dbe6fb))", fontSize: 12, paddingTop: 12 }} />
            <Bar dataKey="actual" radius={[6, 6, 0, 0]} name="Stvarno">
              {chartData.map((item) => <Cell key={item.name} fill={item.isDeadSize ? "var(--error, var(--theme-color-ffb4c2, #ffb4c2))" : item.isCoreSizeMissing ? "var(--warning, var(--theme-color-ffd590, #ffd590))" : "var(--accent, var(--theme-color-44d0ff, #44d0ff))"} />)}
            </Bar>
              <Line type="monotone" dataKey="ideal" stroke={"var(--warning, var(--theme-color-ffd590, #ffd590))"} strokeWidth={2} dot={false} name="Idealno" />
              <ReferenceLine y={0} stroke={"var(--border-hover, var(--theme-color-334055, #334055))"} />
          </ComposedChart>
        </ResponsiveContainer>
      </div>

      <div className="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
        {items.slice(0, cardLimit).map((item) => (
          <div key={item.sizeCode} className={`rounded-2xl border p-3 ${item.isCoreSizeMissing ? "border-[var(--border-default)] bg-[var(--surface-elevated)]" : item.isDeadSize ? "border-[var(--border-default)] bg-[var(--surface-elevated)]" : "border-[var(--border-default)] bg-[var(--surface-elevated)]"}`}>
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-white">vel. {item.sizeCode}</span>
              <span className="text-xs text-[var(--text-primary)]">{(item.deviationPct * 100).toFixed(0)}pp</span>
            </div>
            <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
              <div><div className="text-[var(--text-primary)]">Stvarno</div><div className="font-semibold text-white">{(item.actualSizeShare * 100).toFixed(1)}%</div></div>
              <div><div className="text-[var(--text-primary)]">Idealno</div><div className="font-semibold text-white">{(item.idealSizeShare * 100).toFixed(1)}%</div></div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

