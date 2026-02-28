import argparse
import hashlib
import json
import os
import re
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

import numpy as np

try:
    import pandas as pd
except ImportError as e:  # pragma: no cover
    raise SystemExit("Missing dependency: pandas. Install Python/training/requirements.txt") from e

try:
    import psycopg2  # type: ignore
except ImportError as e:  # pragma: no cover
    raise SystemExit("Missing dependency: psycopg2-binary. Install Python/training/requirements.txt") from e

try:
    import lightgbm as lgb  # type: ignore
except ImportError as e:  # pragma: no cover
    raise SystemExit("Missing dependency: lightgbm. Install Python/training/requirements.txt") from e

from sklearn.metrics import mean_squared_error, roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.isotonic import IsotonicRegression

try:
    import shap  # type: ignore
except Exception:
    shap = None  # type: ignore

try:
    from onnxmltools.convert import convert_lightgbm  # type: ignore
    from skl2onnx.common.data_types import FloatTensorType  # type: ignore
except Exception as e:  # pragma: no cover
    raise SystemExit("Missing ONNX conversion deps (onnxmltools + skl2onnx). Install Python/training/requirements.txt") from e


FEATURE_VIEW_DEFAULT = "vw_product_training_export"


@dataclass(frozen=True)
class TrainArtifacts:
    model_onnx_path: str
    model_onnx_sha256: str
    feature_schema_path: str
    metrics_path: str
    calibration_path: str
    feature_importance_path: str
    shap_summary_path: Optional[str]
    min_feature_values_path: str
    max_feature_values_path: str


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def _write_json(path: str, obj: Any) -> None:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2, default=str)


def _parse_ado_net_connection_string(cs: str) -> Dict[str, str]:
    parts: Dict[str, str] = {}
    for seg in cs.split(";"):
        seg = seg.strip()
        if not seg:
            continue
        if "=" not in seg:
            continue
        k, v = seg.split("=", 1)
        parts[k.strip().lower()] = v.strip()

    def pick(*keys: str) -> Optional[str]:
        for k in keys:
            if k.lower() in parts and parts[k.lower()]:
                return parts[k.lower()]
        return None

    host = pick("host", "server")
    db = pick("database", "initial catalog")
    user = pick("username", "user id", "userid", "user")
    password = pick("password", "pwd")
    port = pick("port") or "5432"

    if not host or not db or not user:
        raise ValueError("ADO.NET connection string must contain Host, Database, Username (and Password if required).")

    out = {
        "host": host,
        "dbname": db,
        "user": user,
        "password": password or "",
        "port": port,
    }

    # optional SSL keywords
    sslmode = pick("sslmode", "ssl mode")
    if sslmode:
        out["sslmode"] = sslmode

    return out


def _connect(pg_url: Optional[str], ado_cs: Optional[str]):
    if pg_url:
        return psycopg2.connect(pg_url)
    if not ado_cs:
        raise ValueError("Provide either --pg-url or --ado-net-connection-string.")
    dsn = _parse_ado_net_connection_string(ado_cs)
    return psycopg2.connect(**dsn)


def _priors_level_code(x: Any) -> float:
    if x is None:
        return 0.0
    s = str(x).strip().lower()
    if s == "brand_category":
        return 3.0
    if s == "category":
        return 2.0
    if s == "brand":
        return 1.0
    return 0.0


def _select_features(df: "pd.DataFrame") -> Tuple["pd.DataFrame", List[str]]:
    # Numeric-only feature set (keeps runtime contract simple for .NET ONNX inference).
    features = [
        "price",
        "avg_rating",
        "review_count",
        "sentiment_score",
        "review_velocity_30d_proxy",
        "volatility_7d",
        "volatility_30d",
        "volatility_90d",
        "momentum_7d",
        "momentum_30d",
        "momentum_90d",
        "discount_freq_30d",
        "discount_freq_90d",
        "typical_change_rate_30d",
        "popularity_prior",
        "deal_score_prior",
        "typical_price_prior",
        "priors_level",
        "has_image_embedding",
        "image_cluster_id",
        "rs_sold_qty_30d",
        "rs_inflow_qty_30d",
        "sell_through_velocity_30d",
        "supply_demand_ratio_30d",
        "median_days_to_sale_proxy",
        "price_elasticity_90d",
    ]

    missing = [c for c in features if c not in df.columns]
    if missing:
        raise ValueError(f"Missing columns in export view: {missing}")

    out = df[features].copy()

    out["priors_level"] = out["priors_level"].apply(_priors_level_code).astype(np.float32)
    out["has_image_embedding"] = out["has_image_embedding"].fillna(False).astype(np.int32).astype(np.float32)
    out["image_cluster_id"] = out["image_cluster_id"].fillna(-1).astype(np.float32)

    # Robust numeric fill.
    for c in out.columns:
        out[c] = pd.to_numeric(out[c], errors="coerce")

    out = out.fillna(0.0).astype(np.float32)
    return out, features


def _load_training_df(conn, feature_view: str, dataset_name: Optional[str], take: int) -> "pd.DataFrame":
    if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", feature_view):
        raise ValueError("feature_view must be a simple identifier (no schema/quotes).")

    sql = f"""
        SELECT *
        FROM {feature_view}
        WHERE sell_probability_rs_label IS NOT NULL
          AND (%(dataset_name)s IS NULL OR dataset_name = %(dataset_name)s)
        LIMIT %(take)s;
    """

    return pd.read_sql_query(sql, conn, params={"dataset_name": dataset_name, "take": take})


def _split_train_test(df: "pd.DataFrame", test_size: float = 0.2, seed: int = 42) -> Tuple["pd.DataFrame", "pd.DataFrame"]:
    if "dataset_split" in df.columns and df["dataset_split"].notna().any():
        test = df[df["dataset_split"].astype(str).str.lower().isin(["test", "val", "valid"])].copy()
        train = df[~df.index.isin(test.index)].copy()
        if len(train) >= 100 and len(test) >= 50:
            return train, test

    train_idx, test_idx = train_test_split(df.index.values, test_size=test_size, random_state=seed)
    return df.loc[train_idx].copy(), df.loc[test_idx].copy()


def _train_lgbm_regressor(X_train: np.ndarray, y_train: np.ndarray) -> "lgb.LGBMRegressor":
    model = lgb.LGBMRegressor(
        n_estimators=800,
        learning_rate=0.03,
        num_leaves=63,
        subsample=0.85,
        colsample_bytree=0.85,
        reg_alpha=0.0,
        reg_lambda=1.0,
        random_state=42,
        n_jobs=max(1, os.cpu_count() or 1),
    )
    model.fit(X_train, y_train)
    return model


def _compute_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> Dict[str, Any]:
    rmse = float(mean_squared_error(y_true, y_pred, squared=False))
    mse = float(mean_squared_error(y_true, y_pred, squared=True))

    # AUC proxy: binarize to "has demand" (label > 0).
    try:
        y_bin = (y_true > 0.0).astype(np.int32)
        if y_bin.min() != y_bin.max():
            auc = float(roc_auc_score(y_bin, y_pred))
        else:
            auc = None
    except Exception:
        auc = None

    return {
        "rmse": rmse,
        "mse_brier_proxy": mse,
        "auc_proxy": auc,
    }


def _fit_isotonic_calibration(y_true: np.ndarray, y_pred: np.ndarray) -> Dict[str, Any]:
    iso = IsotonicRegression(out_of_bounds="clip")
    iso.fit(y_pred, y_true)
    # thresholds are numpy arrays (sorted)
    xs = getattr(iso, "X_thresholds_", None)
    ys = getattr(iso, "y_thresholds_", None)
    if xs is None or ys is None:
        return {"method": "isotonic", "points": []}

    points = [{"x": float(x), "y": float(y)} for x, y in zip(xs, ys)]
    return {"method": "isotonic", "points": points}


def _feature_importance(model: "lgb.LGBMRegressor", feature_names: List[str]) -> List[Dict[str, Any]]:
    booster = model.booster_
    imp = booster.feature_importance(importance_type="gain")
    out = [{"feature": f, "gain": float(g)} for f, g in zip(feature_names, imp)]
    out.sort(key=lambda x: x["gain"], reverse=True)
    return out


def _shap_summary(model: "lgb.LGBMRegressor", X_sample: np.ndarray, feature_names: List[str]) -> Optional[Dict[str, Any]]:
    if shap is None:
        return None

    try:
        explainer = shap.TreeExplainer(model)
        shap_vals = explainer.shap_values(X_sample)
        # mean(|shap|) per feature
        mean_abs = np.mean(np.abs(shap_vals), axis=0)
        ranked = sorted(
            [{"feature": f, "mean_abs_shap": float(v)} for f, v in zip(feature_names, mean_abs)],
            key=lambda x: x["mean_abs_shap"],
            reverse=True,
        )
        return {"top": ranked[:30]}
    except Exception:
        return None


def _export_onnx(model: "lgb.LGBMRegressor", feature_count: int, output_path: str) -> None:
    initial_types = [("X", FloatTensorType([None, feature_count]))]
    onnx_model = convert_lightgbm(model, initial_types=initial_types)
    with open(output_path, "wb") as f:
        f.write(onnx_model.SerializeToString())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--pg-url", default=None, help="postgresql:// URL")
    ap.add_argument("--ado-net-connection-string", default=None, help="ADO.NET connection string (Host=...;Database=...;Username=...;Password=...;)")
    ap.add_argument("--feature-view", default=FEATURE_VIEW_DEFAULT)
    ap.add_argument("--dataset-name", default=None)
    ap.add_argument("--output-dir", required=True)
    ap.add_argument("--take", type=int, default=500000)
    args = ap.parse_args()

    _ensure_dir(args.output_dir)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    run_dir = os.path.join(args.output_dir, f"run_{stamp}")
    _ensure_dir(run_dir)

    conn = _connect(args.pg_url, args.ado_net_connection_string)
    try:
        df = _load_training_df(conn, args.feature_view, args.dataset_name, int(args.take))
        if df.empty:
            raise SystemExit("No rows returned from feature view (missing labels?). Run /api/training/recompute-labels first.")

        # Target
        y = pd.to_numeric(df["sell_probability_rs_label"], errors="coerce").fillna(0.0).astype(np.float32).values

        train_df, test_df = _split_train_test(df)
        X_train_df, feature_names = _select_features(train_df)
        X_test_df, _ = _select_features(test_df)

        X_train = X_train_df.values
        X_test = X_test_df.values
        y_train = pd.to_numeric(train_df["sell_probability_rs_label"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_test = pd.to_numeric(test_df["sell_probability_rs_label"], errors="coerce").fillna(0.0).astype(np.float32).values

        model = _train_lgbm_regressor(X_train, y_train)

        pred_train = model.predict(X_train).astype(np.float32)
        pred_test = model.predict(X_test).astype(np.float32)

        # Clamp to [0,1] for reporting.
        pred_train_c = np.clip(pred_train, 0.0, 1.0)
        pred_test_c = np.clip(pred_test, 0.0, 1.0)

        metrics = {
            "model_type": "sell_probability_rs",
            "trained_at_utc": _utc_now_iso(),
            "rows_total": int(len(df)),
            "rows_train": int(len(train_df)),
            "rows_test": int(len(test_df)),
            "train": _compute_metrics(y_train, pred_train_c),
            "test": _compute_metrics(y_test, pred_test_c),
        }

        calib = _fit_isotonic_calibration(y_train, pred_train_c)
        fi = _feature_importance(model, feature_names)

        shap_out: Optional[Dict[str, Any]] = None
        shap_path: Optional[str] = None
        if shap is not None:
            sample_n = min(2048, X_train.shape[0])
            if sample_n >= 32:
                idx = np.random.RandomState(42).choice(X_train.shape[0], size=sample_n, replace=False)
                shap_out = _shap_summary(model, X_train[idx], feature_names)
                if shap_out is not None:
                    shap_path = os.path.join(run_dir, "shap_summary.json")
                    _write_json(shap_path, shap_out)

        # Save min/max per feature (helps runtime validation & clipping).
        minv = {f: float(X_train_df[f].min()) for f in feature_names}
        maxv = {f: float(X_train_df[f].max()) for f in feature_names}

        # Export ONNX
        onnx_path = os.path.join(run_dir, "model.onnx")
        _export_onnx(model, feature_count=len(feature_names), output_path=onnx_path)
        onnx_sha = _sha256_file(onnx_path)

        # Write artifacts
        feature_schema = [{"name": f, "dtype": "float32"} for f in feature_names]
        feature_schema_path = os.path.join(run_dir, "feature_schema.json")
        metrics_path = os.path.join(run_dir, "metrics.json")
        calib_path = os.path.join(run_dir, "calibration.json")
        fi_path = os.path.join(run_dir, "feature_importance.json")
        min_path = os.path.join(run_dir, "min_feature_values.json")
        max_path = os.path.join(run_dir, "max_feature_values.json")

        _write_json(feature_schema_path, feature_schema)
        _write_json(metrics_path, metrics)
        _write_json(calib_path, calib)
        _write_json(fi_path, fi)
        _write_json(min_path, minv)
        _write_json(max_path, maxv)

        artifacts = TrainArtifacts(
            model_onnx_path=onnx_path,
            model_onnx_sha256=onnx_sha,
            feature_schema_path=feature_schema_path,
            metrics_path=metrics_path,
            calibration_path=calib_path,
            feature_importance_path=fi_path,
            shap_summary_path=shap_path,
            min_feature_values_path=min_path,
            max_feature_values_path=max_path,
        )

        # Machine-readable summary for the .NET worker.
        print(json.dumps({"ok": True, "artifacts": asdict(artifacts)}, ensure_ascii=False))
        return 0
    finally:
        try:
            conn.close()
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())

