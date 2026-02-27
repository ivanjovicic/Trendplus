import { TrendDashboard } from "../components/TrendDashboard";

const PAL = {
    bg:            "#0D0F14",
    textPrimary:   "#E8ECF4",
    textSecondary: "#8A95B0",
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
                <h1 style={{ fontSize: 24, fontWeight: 800, color: PAL.textPrimary, margin: 0, display: "flex", alignItems: "center", gap: 10 }}>
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

