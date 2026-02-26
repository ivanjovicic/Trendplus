"""
Generiši actionable insights iz analitike pre/posle i DiD
Pokretanje: python actionable_insights.py
"""
import pandas as pd
import psycopg2
import os

DB_URL = os.getenv("ANALYTICS_DB_URL", "postgresql://trendplus:trendplus@localhost:5432/trendplus")

QUERY = "SELECT * FROM vw_nivelacija_did LIMIT 1000;"

ICONS = {
    "profit_up": "💰",
    "profit_down": "📉",
    "oos": "🚨",
    "momentum": "⚡",
    "conversion": "🔄",
    "stock": "📦",
    "elasticity": "📈",
    "control": "🟦",
    "test": "🟥"
}

def fetch_df():
    with psycopg2.connect(DB_URL) as conn:
        return pd.read_sql(QUERY, conn)

def generate_insights(df):
    insights = []
    for _, row in df.iterrows():
        # Primer: profit up
        if row.did_revenue > 1000:
            insights.append({
                "icon": ICONS["profit_up"],
                "title": "Profit značajno porastao",
                "signal": f"+{row.did_revenue:.0f} RSD",
                "impact": "Povećan promet i profit nakon nivelacije",
                "action": "Razmotriti širenje strategije na slične artikle"
            })
        # Primer: profit down
        elif row.did_revenue < -1000:
            insights.append({
                "icon": ICONS["profit_down"],
                "title": "Pad profita nakon nivelacije",
                "signal": f"{row.did_revenue:.0f} RSD",
                "impact": "Smanjen promet/profit, mogući pogrešan signal cene",
                "action": "Analizirati uzrok, proveriti dostupnost i konkurenciju"
            })
        # OOS
        if getattr(row, "control_pre_qty", 0) > 0 and getattr(row, "control_post_qty", 0) == 0:
            insights.append({
                "icon": ICONS["oos"],
                "title": "Izgubljena prodaja (OOS)",
                "signal": "0 prodaja nakon nivelacije",
                "impact": "Mogući out-of-stock, izgubljena prodaja",
                "action": "Proveriti zalihe i replenishment"
            })
        # Momentum
        if getattr(row, "post_qty", 0) > getattr(row, "pre_qty", 0) * 1.5:
            insights.append({
                "icon": ICONS["momentum"],
                "title": "Momentum rasta",
                "signal": f"{row.post_qty} jedinica (+{row.post_qty - row.pre_qty})",
                "impact": "Brzi rast nakon promene cene",
                "action": "Pratiti trend, razmotriti dodatne akcije"
            })
    return insights

def main():
    df = fetch_df()
    insights = generate_insights(df)
    for ins in insights[:12]:
        print(f"{ins['icon']} {ins['title']} | {ins['signal']} | {ins['impact']} | {ins['action']}")

if __name__ == "__main__":
    main()
