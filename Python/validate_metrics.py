"""
Validacija metrika i sanity-check za analitiku pre/posle nivelacije
Pokretanje: python validate_metrics.py
"""
import pandas as pd
import psycopg2
import os

DB_URL = os.getenv("ANALYTICS_DB_URL", "postgresql://trendplus:trendplus@localhost:5432/trendplus")

QUERIES = {
    "pre_post": "SELECT * FROM vw_vendor_sales_nivelacija LIMIT 1000;",
    "did": "SELECT * FROM vw_nivelacija_did LIMIT 1000;",
    "rolling": "SELECT * FROM vw_sales_rolling_7d LIMIT 1000;",
    "momentum": "SELECT * FROM vw_sales_momentum LIMIT 1000;",
    "stock": "SELECT * FROM vw_stock_red_zone LIMIT 1000;"
}

def fetch_df(query):
    with psycopg2.connect(DB_URL) as conn:
        return pd.read_sql(query, conn)

def check_pre_post():
    df = fetch_df(QUERIES["pre_post"])
    print("[pre/post] N =", len(df))
    assert (df.pre_qty >= 0).all() and (df.post_qty >= 0).all(), "Negative qty!"
    assert (df.pre_revenue >= 0).all() and (df.post_revenue >= 0).all(), "Negative revenue!"
    print("[pre/post] OK: No negative values.")

def check_did():
    df = fetch_df(QUERIES["did"])
    print("[DiD] N =", len(df))
    assert not df.did_revenue.isnull().all(), "All did_revenue null!"
    print("[DiD] min/max did_revenue:", df.did_revenue.min(), df.did_revenue.max())

def check_rolling():
    df = fetch_df(QUERIES["rolling"])
    print("[rolling] N =", len(df))
    assert (df.ma7_revenue >= 0).all(), "Negative MA7 revenue!"
    print("[rolling] OK: No negative MA7.")

def check_momentum():
    df = fetch_df(QUERIES["momentum"])
    print("[momentum] N =", len(df))
    print("[momentum] mean momentum:", df.momentum_revenue.mean())

def check_stock():
    df = fetch_df(QUERIES["stock"])
    print("[stock] N =", len(df))
    oos_pct = df.is_oos.mean() * 100
    low_stock_pct = df.is_low_stock.mean() * 100
    print(f"[stock] OOS: {oos_pct:.1f}%  Low stock: {low_stock_pct:.1f}%")

def main():
    check_pre_post()
    check_did()
    check_rolling()
    check_momentum()
    check_stock()
    print("Sanity-check completed.")

if __name__ == "__main__":
    main()
