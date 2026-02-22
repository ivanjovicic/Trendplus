import { TrendDashboard } from "../components/TrendDashboard";

export default function TrendDashboardPage() {
    return (
        <div style={{ maxWidth: 1380, margin: "2rem auto", padding: "0 1rem", fontFamily: "system-ui, -apple-system, sans-serif" }}>
            <div style={{ marginBottom: 20 }}>
                <h1 style={{ fontSize: 26, fontWeight: 800, color: "#111827", margin: 0 }}>
                    📊 Trend Dashboard
                </h1>
                <p style={{ color: "#6b7280", marginTop: 4, marginBottom: 0, fontSize: 14 }}>
                    Momentum, score breakdown i historija rangova iz baze — po poslednjem scoring runu.
                </p>
            </div>
            <TrendDashboard />
        </div>
    );
}
