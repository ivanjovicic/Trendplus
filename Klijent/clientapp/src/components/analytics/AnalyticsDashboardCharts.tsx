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
import type { CSSProperties } from "react";
import InfoTip from "../ui/InfoTip";
import type { DailySale } from "../../types/analytics";

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
  formatCurrency: (value: number) => string;
  formatNumber: (value: number, digits?: number) => string;
};

const CHART_COLORS = ["var(--success)", "var(--info)", "var(--warning)", "var(--error)", "var(--accent-info)", "var(--primary)", "var(--accent-success)", "var(--accent-warning)"];
const CHART_GRID_STROKE = "rgba(var(--text-muted-rgb, 154, 164, 199), 0.2)";
const CHART_TEXT_COLOR = "var(--text-muted)";
const CHART_TOOLTIP_CONTENT_STYLE: CSSProperties = {
  background: "var(--surface-elevated)",
  border: "1px solid var(--muted)",
  color: "var(--contrast)",
  borderRadius: "8px",
  boxShadow: "var(--tooltip-box-shadow)",
};

function formatChartValue(
  value: number | string | undefined,
  formatter: (value: number) => string,
): string {
  const numeric = typeof value === "number" ? value : value == null ? null : Number(value);
  return numeric == null || !Number.isFinite(numeric) ? "Nije dostupno" : formatter(numeric);
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
    formatCurrency,
    formatNumber,
  } = props;

  return (
    <>
      {dailySales.length > 0 && (
        <section className="analytics-panel">
          <h3 className="with-tip"><span>Dnevni trend prodaje</span><InfoTip text="Linijski grafikon pokazuje kretanje prometa i transakcija po danima." /></h3>
          <p className="section-note">Koristite ovaj grafikon da brzo uocite dane pada, rasta i nestabilnosti.</p>
          <div className="chart-wrap">
            <ResponsiveContainer width="100%" height={320}>
              <LineChart data={dailySales}>
                <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                <XAxis dataKey="date" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                <Tooltip
                  contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                  formatter={(value: number | string | undefined, name?: string) => [
                    name === "totalRevenue" ? formatChartValue(value, formatCurrency) : formatChartValue(value, formatNumber),
                    name === "totalRevenue" ? "Promet" : "Transakcije",
                  ]}
                />
                <Legend />
                <Line type="monotone" dataKey="totalRevenue" stroke="var(--success)" strokeWidth={2.5} dot={false} name="Promet" />
                <Line type="monotone" dataKey="transactionCount" stroke="var(--info)" strokeWidth={2} dot={false} name="Transakcije" />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </section>
      )}

      <div className="analytics-chart-grid">
        <section className="analytics-panel">
          <h3>Prodaja po kategorijama</h3>
          <p className="section-note">Raspodela prihoda po kategorijama artikala.</p>
          {categoryPieData.length === 0 ? <div className="analytics-empty">Nema podataka za kategorije.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={320}>
                <PieChart>
                  <Pie data={categoryPieData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={105} innerRadius={48} stroke="transparent">
                    {categoryPieData.map((entry, index) => <Cell key={entry.name} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
                  </Pie>
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po polu</h3>
          <p className="section-note">Donut prikaz pokazuje kome je prodaja najviše usmerena.</p>
          {genderPieData.length === 0 ? <div className="analytics-empty">Nema podataka za pol.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={320}>
                <PieChart>
                  <Pie data={genderPieData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={102} innerRadius={58} stroke="transparent">
                    {genderPieData.map((entry, index) => <Cell key={entry.name} fill={CHART_COLORS[index % CHART_COLORS.length]} />)}
                  </Pie>
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Top dobavljači po prometu</h3>
          <p className="section-note">Horizontalni pregled top 10 dobavljača po prihodu.</p>
          {supplierBarData.length === 0 ? <div className="analytics-empty">Nema podataka za dobavljače.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={340}>
                <BarChart data={supplierBarData} layout="vertical" margin={{ left: 12, right: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis type="number" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis type="category" dataKey="name" width={150} tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" radius={[0, 8, 8, 0]} fill="var(--info)" />
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
              <ResponsiveContainer width="100%" height={340}>
                <BarChart data={weekdayChartData} layout="vertical" margin={{ left: 12, right: 12 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis type="number" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis type="category" dataKey="dayName" width={110} tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" radius={[0, 8, 8, 0]} fill="var(--success)" />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po satima</h3>
          <p className="section-note">Prodajni ritam tokom dana od 00 do 23h.</p>
          {hourChartData.every((item) => item.totalRevenue === 0) ? <div className="analytics-empty">Nema podataka po satima.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={320}>
                <AreaChart data={hourChartData}>
                  <defs>
                    <linearGradient id="hourGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--accent-info)" stopOpacity={0.85} />
                      <stop offset="95%" stopColor="var(--accent-info)" stopOpacity={0.05} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis dataKey="label" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} interval={1} />
                  <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Area type="monotone" dataKey="totalRevenue" stroke="var(--accent-info)" fill="url(#hourGradient)" strokeWidth={2.2} />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>

        <section className="analytics-panel">
          <h3>Prodaja po nacinu placanja</h3>
          <p className="section-note">Brz pregled gotovine, kartice i ostalih nacina placanja.</p>
          {paymentChartData.length === 0 ? <div className="analytics-empty">Nema podataka po nacinu placanja.</div> : (
            <div className="chart-wrap">
              <ResponsiveContainer width="100%" height={320}>
                <BarChart data={paymentChartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke={CHART_GRID_STROKE} />
                  <XAxis dataKey="name" tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <YAxis tick={{ fill: CHART_TEXT_COLOR, fontSize: 12 }} />
                  <Tooltip
                    contentStyle={CHART_TOOLTIP_CONTENT_STYLE}
                    formatter={(value: number | string | undefined) => formatChartValue(value, formatCurrency)}
                  />
                  <Bar dataKey="totalRevenue" fill="var(--warning)" radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </section>
      </div>
    </>
  );
}


