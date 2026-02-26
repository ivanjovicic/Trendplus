"""
Elasticnost cene i decomposition: price vs availability vs mix
Pokretanje: python price_elasticity_decomposition.py
"""
import pandas as pd
import psycopg2
import os
from statsmodels.formula.api import ols

DB_URL = os.getenv("ANALYTICS_DB_URL", "postgresql://trendplus:trendplus@localhost:5432/trendplus")

QUERY = """
SELECT
    t.price_event_id, t.sku, t.article_name, t.category, t.vendor_name,
    t.pre_qty, t.pre_revenue, t.post_qty, t.post_revenue,
    t.pre_qty + t.post_qty AS total_qty,
    t.pre_revenue + t.post_revenue AS total_revenue,
    (ph."OldPrice" + ph."NewPrice")/2 AS avg_price,
    (ph."NewPrice" - ph."OldPrice")/NULLIF(ph."OldPrice",0) AS price_change_pct,
    s_pre.stock AS pre_stock, s_post.stock AS post_stock
FROM vw_vendor_sales_nivelacija t
JOIN price_history ph ON ph."Id" = t.price_event_id
LEFT JOIN vw_stock_red_zone s_pre ON s_pre.article_id = ph."ArticleId"
LEFT JOIN vw_stock_red_zone s_post ON s_post.article_id = ph."ArticleId"
LIMIT 1000;
"""

def fetch_df():
    with psycopg2.connect(DB_URL) as conn:
        return pd.read_sql(QUERY, conn)

def estimate_elasticity(df):
    df = df.copy()
    df = df[(df.pre_qty > 0) & (df.post_qty > 0) & (df.avg_price > 0)]
    df["log_qty"] = (df.post_qty / df.pre_qty).apply(lambda x: np.log(x) if x > 0 else 0)
    df["log_price"] = df.price_change_pct.apply(lambda x: np.log(1 + x) if x is not None else 0)
    # Availability adjustment: penalize if post_stock << pre_stock
    df["stock_ratio"] = df.post_stock / df.pre_stock.replace(0, 1)
    df["adj_log_qty"] = df["log_qty"] * df["stock_ratio"].clip(0.5, 1.5)
    model = ols("adj_log_qty ~ log_price", data=df).fit()
    print(model.summary())
    return model

def main():
    df = fetch_df()
    print(f"Fetched {len(df)} rows.")
    model = estimate_elasticity(df)
    print("Elasticity (availability-adjusted):", model.params.get("log_price", None))

if __name__ == "__main__":
    import numpy as np
    main()
