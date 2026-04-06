import { TrendDashboard } from "../components/TrendDashboard";

const PAL = {
    bg:            "var(--c-0d0f14, var(--theme-color-0d0f14, #0D0F14))",
    textPrimary:   "var(--c-e8ecf4, var(--theme-color-e8ecf4, #E8ECF4))",
    textSecondary: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))",
};

export default function TrendDashboardPage() {
    return (
        <div style={{
            maxWidth: 1380,
            margin: "2rem auto",
            padding: "0 1.25rem 3rem",
            fontFamily: "system-ui, -apple-system, sans-serif",
        }}>
            <div style={{ marginBottom: 24 }}>
                    <h1 className="text-2xl font-extrabold text-contrast m-0 flex items-center gap-2">
                    📊 Trend Dashboard
                </h1>
                <p style={{ color: PAL.textSecondary, marginTop: 5, marginBottom: 0, fontSize: 14 }}>
                    Momentum, score breakdown i historija rangova iz baze — po poslednjem scoring runu.
                </p>
            </div>
            <TrendDashboard />
        </div>
    );
}

