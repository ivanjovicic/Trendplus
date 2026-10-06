import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { ReactNode } from "react";
import InfoTip from "../ui/InfoTip";
import {
  CHART_AXIS_TICK,
  CHART_GRID_STROKE,
  CHART_LEGEND_STYLE,
  CHART_METRIC_COLORS,
  CHART_SERIES_COLORS,
  CHART_TOOLTIP_STYLE,
} from "../../utils/chartTooltipStyle";
import type { CategoricalDimensionCoverage, DailySale } from "../../types/analytics";
import {
  dimensionNotPopulatedMessage,
  isDimensionNotPopulatedInSource,
} from "../../utils/categoricalDimensionCoverage";

type NamedValue = {
  name: string;
  value: number;
};

type NamedRevenue = {
  name: string;
  totalRevenue: number;
};

type WeekdayChartPoint = {
  dayName: string;
  totalRevenue: number;
};

type HourChartPoint = {
  label: string;
  totalRevenue: number;
};

type Props = {
  dailySales: DailySale[];
  categoryPieData: NamedValue[];
  genderPieData: NamedValue[];
  supplierBarData: NamedRevenue[];
  weekdayChartData: WeekdayChartPoint[];
  hourChartData: HourChartPoint[];
  paymentChartData: NamedRevenue[];
  dimensionCoverage?: Record<string, CategoricalDimensionCoverage>;
  formatCurrency: (value: number) => string;
  formatNumber: (value: number, digits?: number) => string;
};

const CHART_COLORS = CHART_SERIES_COLORS;
const CHART_TEXT_COLOR = CHART_AXIS_TICK.fill;
const CHART_TOOLTIP_CONTENT_STYLE = CHART_TOOLTIP_STYLE;

function formatChartValue(
  value: number | string | undefined,
  formatter: (value: number) => string,
): string {
  const numeric = typeof value === "number" ? value : value == null ? null : Number(value);
  return numeric == null || !Number.isFinite(numeric) ? "Nije dostupno" : formatter(numeric);
}

function renderDimensionUnavailable(
  dimensionKey: string,
  dimensionLabel: string,
  dimensionCoverage: Record<string, CategoricalDimensionCoverage> | undefined,
  fallbackEmpty: boolean,
  fallbackMessage: string,
  chart: ReactNode,
) {
  if (isDimensionNotPopulatedInSource(dimensionCoverage?.[dimensionKey])) {
    return (
      <div className="analytics-empty" role="status">
        {dimensionNotPopulatedMessage(dimensionLabel)}
      </div>
    );
  }

  if (fallbackEmpty) {
    return <div className="analytics-empty">{fallbackMessage}</div>;
  }

  return chart;
}

export default function AnalyticsDashboardCharts(props: Props) {
  const {
    dailySales,
    categoryPieData,
    genderPieData,
    supplierBarData,
    weekdayChartData,
    hourChartData,
    paymentChartData,
    dimensionCoverage,
    formatCurrency,
    formatNumber,
  } = props;

  return (
    <>
      {dailySales.length > 0 && (
        <section className="analytics-panel">
          <h3 className="with-tip"><span>Dnevni trend prodaje</span><InfoTip text="Linijski grafikon pokazuje kretanje prometa i broja prodajnih dokumenata po danima." /></h3>
          <p className="section-note">Koristite ovaj grafikon da brzo uocite dane pada, rasta i nestabilnosti.</p>
          <div className="chart-wrap">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={dailySales}>
                <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                <XAxis dataKey="date" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                <Tooltip
                  contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                  formatter={(value: number | string | undefined, name?: string) => [
                    name === "totalRevenue" ? formatChartValue(value, formatCurrency) : formatChartValue(value, formatNumber),
                    name === "totalRevenue" ? "Promet" : "Prodajni dokumenti",
                  ]}
                />
                <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                <Line type="monotone" dataKey="totalRevenue" stroke={CHART_METRIC_COLORS.revenue} strokeWidth={2.5} dot={false} name="Promet" />
                <Line type="monotone" dataKey="transactionCount" stroke={CHART_METRIC_COLORS.documents} strokeWidth={2} dot={false} name="Prodajni dokumenti" />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </section>
      )}

      <div className="analytics-chart-grid">
        <section className="analytics-panel">
          <h3>Prodaja po kategorijama</h3>
          <p className="section-note">Raspodela prihoda po kategorijama artikala.</p>
          {renderDimensionUnavailable(
            "category",
            "Kategorija",
            dimensionCoverage,
            categoryPieData.length === 0,
            "Nema podataka za kategorije.",
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={categoryPieData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={105} innerRadius={48} stroke="transparent">
                    {categoryPieData.map((entry, index) => <Cell key={entry.name} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
                  </Pie>
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                </PieChart>
              </ResponsiveContainer>
            </div>,
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po polu</h3>
          <p className="section-note">Donut prikaz pokazuje kome je prodaja najviše usmerena.</p>
          {renderDimensionUnavailable(
            "gender",
            "Pol",
            dimensionCoverage,
            genderPieData.length === 0,
            "Nema podataka za pol.",
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={genderPieData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={102} innerRadius={58} stroke="transparent">
                    {genderPieData.map((entry, index) => <Cell key={entry.name} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
                  </Pie>
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Legend wrapperStyle={CHART_LEGEND_STYLE} />
                </PieChart>
              </ResponsiveContainer>
            </div>,
          )}
        </section>

        <section className="analytics-panel">
          <h3>Top dobavljači po prometu</h3>
          <p className="section-note">Horizontalni pregled top 10 dobavljača po prihodu.</p>
          {supplierBarData.length === 0 ? <div className="analytics-empty">Nema podataka za dobavljače.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={supplierBarData} layout="vertical" margin={{ left: 12, right: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis type="number" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis type="category" dataKey="name" width={150} tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" radius={[0, 8, 8, 0]} fill={CHART_METRIC_COLORS.revenue} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po danima u nedelji</h3>
          <p className="section-note">Koji dan u nedelji pravi najviše prihoda.</p>
          {weekdayChartData.every((item) => item.totalRevenue === 0) ? <div className="analytics-empty">Nema podataka po danima.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={weekdayChartData} layout="vertical" margin={{ left: 12, right: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis type="number" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis type="category" dataKey="dayName" width={110} tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" radius={[0, 8, 8, 0]} fill={CHART_METRIC_COLORS.revenue} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po satima</h3>
          <p className="section-note">Prodajni ritam tokom dana od 00 do 23h.</p>
          {renderDimensionUnavailable(
            "hour",
            "Sat prodaje",
            dimensionCoverage,
            hourChartData.every((item) => item.totalRevenue === 0),
            "Nema podataka po satima.",
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={hourChartData}>
                  <defs>
                    <linearGradient id="hourGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor={CHART_METRIC_COLORS.revenue} stopOpacity={0.85} />
                      <stop offset="95%" stopColor={CHART_METRIC_COLORS.revenue} stopOpacity={0.05} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis dataKey="label" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} interval={1} />
                  <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Area type="monotone" dataKey="totalRevenue" stroke={CHART_METRIC_COLORS.revenue} fill="url(#hourGradient)" strokeWidth={2.2} />
                </AreaChart>
              </ResponsiveContainer>
            </div>,
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po nacinu placanja</h3>
          <p className="section-note">Brz pregled gotovine, kartice i ostalih nacina placanja.</p>
          {renderDimensionUnavailable(
            "payment",
            "Način plaćanja",
            dimensionCoverage,
            paymentChartData.length === 0,
            "Nema podataka po nacinu placanja.",
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={paymentChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis dataKey="name" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" fill={CHART_METRIC_COLORS.revenue} radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>,
          )}
        </section>
      </div>
    </>
  );
}


