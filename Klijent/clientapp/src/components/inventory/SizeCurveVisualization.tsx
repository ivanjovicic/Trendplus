import { Bar, CartesianGrid, Cell, ComposedChart, Legend, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { NameType, ValueType } from "recharts/types/component/DefaultTooltipContent";
import type { SizeCurvePointDto } from "../../types/analytics";
import { fmtPct, fmtPctFromRatio, fmtSignedPct } from "../../utils/analyticsFormatters";

type SizeCurveVisualizationProps = {
  items: SizeCurvePointDto[];
  cardLimit?: number;
};

export function SizeCurveVisualization({ items, cardLimit = 8 }: SizeCurveVisualizationProps) {
  const formatDeltaPct = (value: number | null | undefined, digits = 1) =>
    value == null || Number.isNaN(value) ? "N/A" : fmtSignedPct(value, digits).replace("%", "pp");
  const hasMissingEvidence = (item: SizeCurvePointDto) =>
    item.evidenceStatus === "missing"
    || item.evidenceStatus == null
    || item.actualSizeShare == null
    || item.idealSizeShare == null
    || item.deviationPct == null
    || item.isCoreSizeMissing == null
    || item.isDeadSize == null
    || item.brokenRun == null
    || item.curveConfidence == null;

  const missingCoreCount = items.filter((item) => item.isCoreSizeMissing === true).length;
  const deadSizeCount = items.filter((item) => item.isDeadSize === true).length;
  const hasBrokenRun = items.some((item) => item.brokenRun === true);
  const hasMissingEvidenceBadge = items.some(hasMissingEvidence);
  const chartData = items.map((item) => ({
    name: item.sizeCode,
    actual: item.actualSizeShare == null ? null : +(item.actualSizeShare * 100).toFixed(1),
    ideal: item.idealSizeShare == null ? null : +(item.idealSizeShare * 100).toFixed(1),
    deviation: item.deviationPct == null ? null : +(item.deviationPct * 100).toFixed(1),
    isDeadSize: item.isDeadSize === true,
    isCoreSizeMissing: item.isCoreSizeMissing === true,
    hasMissingEvidence: hasMissingEvidence(item),
  }));

  return (
    <div className="mt-4">
      <div className="mb-3 flex flex-wrap gap-2">
        {missingCoreCount > 0 ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">{missingCoreCount} core size nedostaju</span> : null}
        {deadSizeCount > 0 ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">{deadSizeCount} mrtve velicine</span> : null}
        {hasBrokenRun ? <span className="inline-flex rounded-full border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">Broken run detektovan</span> : null}
        {hasMissingEvidenceBadge ? <span className="inline-flex rounded-full border border-dashed border-[var(--border-default)] bg-[var(--surface-elevated)] px-2.5 py-1 text-xs font-semibold text-[var(--text-primary)]">Evidencija: nedostaje</span> : null}
      </div>

      <div className="h-[240px]">
        <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={180}>
          <ComposedChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke={"var(--border-default, var(--theme-color-233042, #233042))"} />
              <XAxis dataKey="name" tick={{ fill: "var(--text-muted, var(--theme-color-92a4bf, #92a4bf))", fontSize: 12 }} />
              <YAxis tick={{ fill: "var(--text-muted, var(--theme-color-92a4bf, #92a4bf))", fontSize: 12 }} unit="%" />
              <Tooltip
                contentStyle={{ background: "var(--surface-default)", border: "1px solid var(--border-default)", color: "var(--text-primary)" }}
                formatter={(value: ValueType | undefined, name: NameType | undefined) => {
                  if (Array.isArray(value) || value == null || Number.isNaN(Number(value))) {
                    return ["N/A", name];
                  }

                  return [fmtPct(Number(value), 1), name];
                }}
                labelFormatter={(label, payload) => {
                  const point = payload?.[0]?.payload as { actual?: number | null; ideal?: number | null; deviation?: number | null } | undefined;
                  return point ? `Vel. ${label} | stvarno ${fmtPct(point.actual)} | idealno ${fmtPct(point.ideal)} | odstupanje ${formatDeltaPct(point.deviation, 1)}` : `Vel. ${label}`;
                }}
              />
              <Legend wrapperStyle={{ color: "var(--text-primary, var(--theme-color-dbe6fb, #dbe6fb))", fontSize: 12, paddingTop: 12 }} />
            <Bar dataKey="actual" radius={[6, 6, 0, 0]} name="Stvarno">
              {chartData.map((item) => <Cell key={item.name} fill={item.isDeadSize ? "var(--error, var(--theme-color-ffb4c2, #ffb4c2))" : item.isCoreSizeMissing ? "var(--warning, var(--theme-color-ffd590, #ffd590))" : item.hasMissingEvidence ? "var(--border-default, var(--theme-color-233042, #233042))" : "var(--accent, var(--theme-color-44d0ff, #44d0ff))"} />)}
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
              <span className="text-xs text-[var(--text-primary)]">{formatDeltaPct(item.deviationPct == null ? null : item.deviationPct * 100, 0)}</span>
            </div>
            <div className="mt-2 grid grid-cols-2 gap-2 text-xs">
              <div><div className="text-[var(--text-primary)]">Stvarno</div><div className="font-semibold text-white">{fmtPctFromRatio(item.actualSizeShare)}</div></div>
              <div><div className="text-[var(--text-primary)]">Idealno</div><div className="font-semibold text-white">{fmtPctFromRatio(item.idealSizeShare)}</div></div>
            </div>
            {item.evidenceStatus === "missing" || item.actualSizeShare == null || item.idealSizeShare == null || item.deviationPct == null || item.isCoreSizeMissing == null || item.isDeadSize == null || item.brokenRun == null || item.curveConfidence == null ? (
              <div className="mt-2 text-[11px] font-medium text-warning">Evidencija: nedostaje</div>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  );
}
