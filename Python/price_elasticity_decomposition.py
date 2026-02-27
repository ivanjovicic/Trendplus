"""
Elasticnost cene i decomposition: price vs availability vs mix
Pokretanje: python price_elasticity_decomposition.py
"""
import pandas as pd
import psycopg2
import os
from statsmodels.formula.api import ols
import numpy as np
from scipy.stats import zscore
from statsmodels.regression.quantile_regression import QuantReg
from statsmodels.stats.weightstats import DescrStatsW
from statsmodels.stats.outliers_influence import variance_inflation_factor
from statsmodels.tools.tools import add_constant
from sklearn.preprocessing import RobustScaler

# Helper functions for robust transformations
def winsorize_series(series, lower=0.01, upper=0.99):
    return series.clip(lower=series.quantile(lower), upper=series.quantile(upper))

def robust_stock_correction(stock_ratio):
    return np.sqrt(stock_ratio.clip(0.5, 1.5))

def weighted_ols(formula, data, weights):
    return ols(formula, data=data, weights=weights).fit()

DB_URL = os.getenv("ANALYTICS_DB_URL", "postgresql://trendplus:trendplus@localhost:5432/trendplus")

QUERY = """
SELECT
    t.price_event_id, t.sku, t.article_name, t.category, t.vendor_name,
    t.pre_qty, t.pre_revenue, t.post_qty, t.post_revenue,
    t.pre_qty + t.post_qty AS total_qty,
    t.pre_revenue + t.post_revenue AS total_revenue,
    (ph."SourceDnevnikId") AS source_dnevnik_id, -- Fixed join to use SourceDnevnikId
    (ph."NewPrice" - ph."OldPrice")/NULLIF(ph."OldPrice",0) AS price_change_pct,
    COALESCE(SUM(ps."kolicina") / 30.0, 0) AS velocity_30,
    daily_stock_pre.stock AS pre_stock, -- Adjusted to use daily stock history
    daily_stock_post.stock AS post_stock
FROM vw_vendor_sales_nivelacija t
JOIN price_history ph ON ph."SourceDnevnikId" = t.price_event_id -- Corrected join
LEFT JOIN vw_daily_stock_history daily_stock_pre ON daily_stock_pre.article_id = ph."ArticleId" AND daily_stock_pre.date = t.event_date - INTERVAL '1 day'
LEFT JOIN vw_daily_stock_history daily_stock_post ON daily_stock_post.article_id = ph."ArticleId" AND daily_stock_post.date = t.event_date + INTERVAL '1 day'
LEFT JOIN "prodaja_stavke" ps ON ps."id_artikal" = t.sku
GROUP BY t.price_event_id, t.sku, ph."ArticleId";
"""

def fetch_df():
    with psycopg2.connect(DB_URL) as conn:
        return pd.read_sql(QUERY, conn)

def estimate_elasticity(df):
    df = df.copy()

    # Filter valid rows
    df = df[(df.pre_qty > 0) & (df.post_qty > 0) & (df.avg_price > 0)]

    # Winsorize quantities and prices to handle outliers
    df["pre_qty"] = winsorize_series(df["pre_qty"])
    df["post_qty"] = winsorize_series(df["post_qty"])
    df["avg_price"] = winsorize_series(df["avg_price"])

    # Log transformations with epsilon for stability
    epsilon = 1e-6
    df["log_qty"] = np.log(df.post_qty / df.pre_qty + epsilon)
    df["log_price"] = np.log1p(df.price_change_pct)

    # Robust stock correction
    df["stock_ratio"] = robust_stock_correction(df.post_stock / df.pre_stock.replace(0, 1))
    df["adj_log_qty"] = df["log_qty"] * df["stock_ratio"]

    # Add baseline velocity and category fixed effects if available
    if "baseline_velocity" in df.columns:
        df["baseline_velocity_z"] = zscore(df["baseline_velocity"])
    if "category" in df.columns:
        df = pd.get_dummies(df, columns=["category"], drop_first=True)

    # Weighted regression with sqrt of average quantities
    df["weights"] = np.sqrt((df.pre_qty + df.post_qty) / 2)
    model = weighted_ols("adj_log_qty ~ log_price", data=df, weights=df["weights"])

    # Add robust statistics
    df["elasticity_confidence"] = model.bse["log_price"]
    df["sample_size_effective"] = len(df)
    df["r2_signal"] = model.rsquared

    print(model.summary())
    return model

def main():
    df = fetch_df()
    print(f"Fetched {len(df)} rows.")
    model = estimate_elasticity(df)
    print("Elasticity (availability-adjusted):", model.params.get("log_price", None))

if __name__ == "__main__":
    main()
