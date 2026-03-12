import argparse
import hashlib
import json
import os
import re
import zipfile
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Tuple

import joblib
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

from sklearn.dummy import DummyClassifier, DummyRegressor
from sklearn.metrics import mean_squared_error, roc_auc_score


FEATURE_VIEW_DEFAULT = "supplier_training_dataset_v1"
PREDICTION_VIEW_DEFAULT = "vw_supplier_ranking_inference_v1"
MODEL_TYPE = "supplier_ranking_v1"


FEATURE_COLUMNS = [
    "articles_count",
    "revenue_30d",
    "units_30d",
    "sales_velocity",
    "fullprice_sellthrough",
    "markdown_dependency",
    "price_change_pct",
    "discount_frequency",
    "dead_stock_rate",
    "unsold_stock_value",
    "days_of_cover",
    "repeat_winner_rate",
    "return_rate",
    "category_focus_score",
    "trend_score",
    "trend_momentum",
    "signal_quality_share",
]


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
    predictions_path: str


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def _write_json(path: str, obj: Any) -> None:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=2, default=str)


def _sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def _parse_ado_net_connection_string(cs: str) -> Dict[str, str]:
    parts: Dict[str, str] = {}
    for seg in cs.split(";"):
        seg = seg.strip()
        if not seg or "=" not in seg:
            continue
        k, v = seg.split("=", 1)
        parts[k.strip().lower()] = v.strip()

    def pick(*keys: str) -> Optional[str]:
        for key in keys:
            value = parts.get(key.lower())
            if value:
                return value
        return None

    host = pick("host", "server")
    db = pick("database", "initial catalog")
    user = pick("username", "user id", "userid", "user")
    password = pick("password", "pwd") or ""
    port = pick("port") or "5432"

    if not host or not db or not user:
        raise ValueError("ADO.NET connection string must contain Host, Database, Username.")

    out = {
        "host": host,
        "dbname": db,
        "user": user,
        "password": password,
        "port": port,
    }

    sslmode = pick("sslmode", "ssl mode")
    if sslmode:
        out["sslmode"] = sslmode

    return out


def _connect(pg_url: Optional[str], ado_cs: Optional[str]):
    if pg_url:
        return psycopg2.connect(pg_url)
    if not ado_cs:
        raise ValueError("Provide either --pg-url or --ado-net-connection-string.")
    return psycopg2.connect(**_parse_ado_net_connection_string(ado_cs))


def _safe_ident(name: str) -> str:
    if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
        raise ValueError(f"Unsafe SQL identifier: {name}")
    return name


def _load_df(conn, view_name: str, take: int, require_labels: bool) -> "pd.DataFrame":
    relation = _safe_ident(view_name)
    label_clause = "WHERE success_label IS NOT NULL" if require_labels else ""
    sql = f"""
        SELECT *
        FROM {relation}
        {label_clause}
        ORDER BY snapshot_date, supplier_id
        LIMIT %(take)s;
    """
    return pd.read_sql_query(sql, conn, params={"take": int(max(1, take))})


def _prepare_features(df: "pd.DataFrame") -> Tuple["pd.DataFrame", List[str]]:
    missing = [col for col in FEATURE_COLUMNS if col not in df.columns]
    if missing:
        raise ValueError(f"Missing supplier ranking feature columns: {missing}")

    out = df[FEATURE_COLUMNS].copy()
    for col in FEATURE_COLUMNS:
        out[col] = pd.to_numeric(out[col], errors="coerce")
    out = out.fillna(0.0).astype(np.float32)
    return out, FEATURE_COLUMNS


def _split_train_test(df: "pd.DataFrame") -> Tuple["pd.DataFrame", "pd.DataFrame"]:
    if "snapshot_date" not in df.columns:
        raise ValueError("supplier_training_dataset_v1 must contain snapshot_date.")

    working = df.copy()
    working["snapshot_date"] = pd.to_datetime(working["snapshot_date"], errors="coerce")
    working = working.dropna(subset=["snapshot_date"])
    if len(working) < 20:
        raise ValueError("Need at least 20 labeled rows to train supplier ranking.")

    unique_dates = sorted(working["snapshot_date"].dt.date.unique())
    if len(unique_dates) >= 5:
        split_index = max(1, int(len(unique_dates) * 0.8))
        cutoff = unique_dates[min(split_index, len(unique_dates) - 1)]
        train = working[working["snapshot_date"].dt.date < cutoff].copy()
        test = working[working["snapshot_date"].dt.date >= cutoff].copy()
        if len(train) >= 12 and len(test) >= 5:
            return train, test

    split = int(len(working) * 0.8)
    train = working.iloc[:split].copy()
    test = working.iloc[split:].copy()
    if train.empty or test.empty:
        raise ValueError("Supplier ranking dataset split produced an empty train/test partition.")
    return train, test


def _train_classifier(X_train: np.ndarray, y_train: np.ndarray):
    unique_classes = np.unique(y_train)
    if unique_classes.size < 2:
        model = DummyClassifier(strategy="constant", constant=int(unique_classes[0]) if unique_classes.size == 1 else 0)
        model.fit(X_train, y_train)
        return model

    model = lgb.LGBMClassifier(
        n_estimators=500,
        learning_rate=0.04,
        num_leaves=31,
        subsample=0.9,
        colsample_bytree=0.9,
        random_state=42,
        n_jobs=max(1, os.cpu_count() or 1),
    )
    model.fit(X_train, y_train)
    return model


def _train_regressor(X_train: np.ndarray, y_train: np.ndarray):
    if np.nanstd(y_train) < 1e-9:
        model = DummyRegressor(strategy="mean")
        model.fit(X_train, y_train)
        return model

    model = lgb.LGBMRegressor(
        n_estimators=450,
        learning_rate=0.04,
        num_leaves=31,
        subsample=0.9,
        colsample_bytree=0.9,
        random_state=42,
        n_jobs=max(1, os.cpu_count() or 1),
    )
    model.fit(X_train, y_train)
    return model


def _predict_proba(model, X: np.ndarray) -> np.ndarray:
    if hasattr(model, "predict_proba"):
        pred = model.predict_proba(X)
        if pred.ndim == 2 and pred.shape[1] > 1:
            return pred[:, 1].astype(np.float32)
    pred = model.predict(X)
    return np.clip(np.asarray(pred, dtype=np.float32), 0.0, 1.0)


def _compute_classifier_metrics(y_true: np.ndarray, y_prob: np.ndarray) -> Dict[str, Any]:
    out: Dict[str, Any] = {}
    try:
        if np.unique(y_true).size > 1:
            out["auc"] = float(roc_auc_score(y_true, y_prob))
        else:
            out["auc"] = None
    except Exception:
        out["auc"] = None
    out["avg_score"] = float(np.mean(y_prob)) if y_prob.size else 0.0
    out["positive_rate"] = float(np.mean(y_true)) if y_true.size else 0.0
    return out


def _compute_reg_metrics(y_true: np.ndarray, y_pred: np.ndarray) -> Dict[str, Any]:
    return {
        "rmse": float(mean_squared_error(y_true, y_pred, squared=False)),
        "mean_target": float(np.mean(y_true)) if y_true.size else 0.0,
    }


def _feature_importance(model, feature_names: List[str]) -> List[Dict[str, Any]]:
    if hasattr(model, "booster_"):
        imp = model.booster_.feature_importance(importance_type="gain")
    elif hasattr(model, "feature_importances_"):
        imp = np.asarray(model.feature_importances_, dtype=np.float64)
    else:
        imp = np.zeros(len(feature_names), dtype=np.float64)
    rows = [{"feature": name, "gain": float(value)} for name, value in zip(feature_names, imp)]
    rows.sort(key=lambda item: item["gain"], reverse=True)
    return rows


def _top_feature_contributions(model, X: np.ndarray, feature_names: List[str]) -> List[List[Tuple[str, float]]]:
    if hasattr(model, "booster_"):
        contrib = model.booster_.predict(X, pred_contrib=True)
        if isinstance(contrib, list):
            contrib = contrib[0]
        contrib = np.asarray(contrib)
        if contrib.ndim == 2 and contrib.shape[1] >= len(feature_names):
            contrib = contrib[:, : len(feature_names)]
            rows: List[List[Tuple[str, float]]] = []
            for idx in range(contrib.shape[0]):
                ranked = sorted(
                    [(feature_names[j], float(contrib[idx, j])) for j in range(contrib.shape[1])],
                    key=lambda item: abs(item[1]),
                    reverse=True,
                )
                rows.append(ranked[:3])
            return rows
    global_top = [(name, 0.0) for name in feature_names[:3]]
    return [global_top for _ in range(X.shape[0])]


def _feature_phrase(name: str, value: float, contribution: float) -> str:
    positive = contribution >= 0
    if name == "fullprice_sellthrough":
        return "visok sell-through bez snizenja" if positive else "slab sell-through bez snizenja"
    if name == "revenue_30d":
        return "jak prihod u poslednjih 30 dana" if positive else "slab prihod u poslednjih 30 dana"
    if name == "units_30d":
        return "jak promet u komadima" if positive else "slab promet u komadima"
    if name == "sales_velocity":
        return "brza prodaja" if positive else "spora prodaja"
    if name == "markdown_dependency":
        if positive and value < 0.35:
            return "niska zavisnost od snizenja"
        return "visoka zavisnost od snizenja"
    if name == "price_change_pct":
        return "umerene korekcije cene" if positive else "agresivne korekcije cene"
    if name == "discount_frequency":
        return "retke akcijske intervencije" if positive else "ceste akcijske intervencije"
    if name == "dead_stock_rate":
        return "nizak dead stock" if positive else "visok dead stock"
    if name == "unsold_stock_value":
        return "kontrolisana vezana zaliha" if positive else "visok kapital u zalihama"
    if name == "days_of_cover":
        return "zdrava pokrivenost zalihom" if positive else "rizicna pokrivenost zalihom"
    if name == "repeat_winner_rate":
        return "veliki udeo pobednickih artikala" if positive else "malo pobednickih artikala"
    if name == "return_rate":
        return "nizak povracaj" if positive else "visok povracaj"
    if name == "category_focus_score":
        return "jasna snaga u kljucnoj kategoriji" if positive else "rasut fokus po kategorijama"
    if name == "trend_score":
        return "jak trend signal" if positive else "slab trend signal"
    if name == "trend_momentum":
        return "pozitivan trend momentum" if positive else "negativan trend momentum"
    if name == "signal_quality_share":
        return "visok kvalitet analitickog signala" if positive else "slab kvalitet analitickog signala"
    return name.replace("_", " ")


def _build_explanation(top_items: List[Tuple[str, float]], row: "pd.Series") -> Tuple[str, str, str, str]:
    normalized = []
    for feature_name, contribution in top_items:
        value = float(row.get(feature_name, 0.0))
        normalized.append((feature_name, _feature_phrase(feature_name, value, contribution)))

    top_names = [item[0] for item in normalized]
    top_names += ["", "", ""]
    phrases = [item[1] for item in normalized if item[1]]
    if not phrases:
        explanation = "AI procena je neutralna jer trenutno nema dovoljno jakih signala."
    elif len(phrases) == 1:
        explanation = phrases[0][:1].upper() + phrases[0][1:] + "."
    else:
        explanation = f"{phrases[0][:1].upper() + phrases[0][1:]} i {phrases[1]}."
    return top_names[0], top_names[1], top_names[2], explanation


def _zip_bundle(paths: List[str], output_path: str) -> None:
    with zipfile.ZipFile(output_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for path in paths:
            if os.path.exists(path):
                zf.write(path, arcname=os.path.basename(path))


def main() -> int:
    ap = argparse.ArgumentParser(description="Supplier ranking LightGBM training + batch prediction pipeline.")
    ap.add_argument("--pg-url", default=None)
    ap.add_argument("--ado-net-connection-string", default=None)
    ap.add_argument("--feature-view", default=FEATURE_VIEW_DEFAULT)
    ap.add_argument("--prediction-view", default=PREDICTION_VIEW_DEFAULT)
    ap.add_argument("--output-dir", required=True)
    ap.add_argument("--take", type=int, default=500000)
    args = ap.parse_args()

    _ensure_dir(args.output_dir)
    run_stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    run_dir = os.path.join(args.output_dir, f"run_{run_stamp}")
    _ensure_dir(run_dir)

    conn = _connect(args.pg_url, args.ado_net_connection_string)
    try:
        train_df = _load_df(conn, args.feature_view, args.take, require_labels=True)
        if train_df.empty:
            raise SystemExit("Supplier training dataset is empty.")

        infer_df = _load_df(conn, args.prediction_view, args.take, require_labels=False)
        if infer_df.empty:
            raise SystemExit("Supplier inference dataset is empty.")

        X_all, feature_names = _prepare_features(train_df)
        infer_X_df, _ = _prepare_features(infer_df)
        train_split, test_split = _split_train_test(train_df)

        X_train_df, _ = _prepare_features(train_split)
        X_test_df, _ = _prepare_features(test_split)

        X_train = X_train_df.values
        X_test = X_test_df.values
        X_infer = infer_X_df.values

        y_success_train = pd.to_numeric(train_split["success_label"], errors="coerce").fillna(0).astype(np.int32).values
        y_success_test = pd.to_numeric(test_split["success_label"], errors="coerce").fillna(0).astype(np.int32).values
        y_revenue_train = pd.to_numeric(train_split["label_revenue_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_revenue_test = pd.to_numeric(test_split["label_revenue_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_margin_train = pd.to_numeric(train_split["label_margin_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_margin_test = pd.to_numeric(test_split["label_margin_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_sellthrough_train = pd.to_numeric(train_split["label_sellthrough_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values
        y_sellthrough_test = pd.to_numeric(test_split["label_sellthrough_next_30d"], errors="coerce").fillna(0.0).astype(np.float32).values

        success_model = _train_classifier(X_train, y_success_train)
        revenue_model = _train_regressor(X_train, y_revenue_train)
        margin_model = _train_regressor(X_train, y_margin_train)
        sellthrough_model = _train_regressor(X_train, y_sellthrough_train)

        success_prob_test = _predict_proba(success_model, X_test)
        pred_revenue_test = np.asarray(revenue_model.predict(X_test), dtype=np.float32)
        pred_margin_test = np.asarray(margin_model.predict(X_test), dtype=np.float32)
        pred_sellthrough_test = np.clip(np.asarray(sellthrough_model.predict(X_test), dtype=np.float32), 0.0, 1.0)

        metrics = {
            "model_type": MODEL_TYPE,
            "trained_at_utc": _utc_now_iso(),
            "rows_total": int(len(train_df)),
            "rows_train": int(len(train_split)),
            "rows_test": int(len(test_split)),
            "classifier": _compute_classifier_metrics(y_success_test, success_prob_test),
            "revenue_regressor": _compute_reg_metrics(y_revenue_test, pred_revenue_test),
            "margin_regressor": _compute_reg_metrics(y_margin_test, pred_margin_test),
            "sellthrough_regressor": _compute_reg_metrics(y_sellthrough_test, pred_sellthrough_test),
        }

        success_prob_infer = np.clip(_predict_proba(success_model, X_infer), 0.0, 1.0)
        pred_revenue_infer = np.asarray(revenue_model.predict(X_infer), dtype=np.float32)
        pred_margin_infer = np.asarray(margin_model.predict(X_infer), dtype=np.float32)
        pred_sellthrough_infer = np.clip(np.asarray(sellthrough_model.predict(X_infer), dtype=np.float32), 0.0, 1.0)
        contrib_rows = _top_feature_contributions(success_model, X_infer, feature_names)

        prediction_rows: List[Dict[str, Any]] = []
        for idx, (_, infer_row) in enumerate(infer_df.iterrows()):
            top1, top2, top3, explanation = _build_explanation(contrib_rows[idx], infer_row)
            score = float(np.clip(success_prob_infer[idx] * 100.0, 0.0, 100.0))
            prediction_rows.append(
                {
                    "supplier_id": int(infer_row["supplier_id"]),
                    "snapshot_date": str(infer_row["snapshot_date"]),
                    "ml_supplier_score": round(score, 2),
                    "predicted_supplier_success_score": round(score, 2),
                    "predicted_revenue_next_30d": round(float(pred_revenue_infer[idx]), 2),
                    "predicted_margin_next_30d": round(float(pred_margin_infer[idx]), 2),
                    "predicted_sellthrough_next_30d": round(float(pred_sellthrough_infer[idx]), 4),
                    "success_probability": round(float(success_prob_infer[idx]), 6),
                    "top_feature_1": top1,
                    "top_feature_2": top2,
                    "top_feature_3": top3,
                    "explanation_text": explanation,
                }
            )

        success_model_path = os.path.join(run_dir, "success_model.joblib")
        revenue_model_path = os.path.join(run_dir, "revenue_model.joblib")
        margin_model_path = os.path.join(run_dir, "margin_model.joblib")
        sellthrough_model_path = os.path.join(run_dir, "sellthrough_model.joblib")
        joblib.dump(success_model, success_model_path)
        joblib.dump(revenue_model, revenue_model_path)
        joblib.dump(margin_model, margin_model_path)
        joblib.dump(sellthrough_model, sellthrough_model_path)

        feature_schema_path = os.path.join(run_dir, "feature_schema.json")
        metrics_path = os.path.join(run_dir, "metrics.json")
        calibration_path = os.path.join(run_dir, "calibration.json")
        fi_path = os.path.join(run_dir, "feature_importance.json")
        min_path = os.path.join(run_dir, "min_feature_values.json")
        max_path = os.path.join(run_dir, "max_feature_values.json")
        predictions_path = os.path.join(run_dir, "predictions.json")

        _write_json(feature_schema_path, [{"name": col, "dtype": "float32"} for col in feature_names])
        _write_json(metrics_path, metrics)
        _write_json(calibration_path, {"method": "none", "notes": "batch supplier ranking uses raw LightGBM probabilities"})
        _write_json(fi_path, _feature_importance(success_model, feature_names))
        _write_json(min_path, {name: float(X_all[name].min()) for name in feature_names})
        _write_json(max_path, {name: float(X_all[name].max()) for name in feature_names})
        _write_json(predictions_path, prediction_rows)

        bundle_path = os.path.join(run_dir, "model_bundle.zip")
        _zip_bundle(
            [
                success_model_path,
                revenue_model_path,
                margin_model_path,
                sellthrough_model_path,
                feature_schema_path,
                metrics_path,
                fi_path,
            ],
            bundle_path,
        )
        bundle_sha = _sha256_file(bundle_path)

        artifacts = TrainArtifacts(
            model_onnx_path=bundle_path,
            model_onnx_sha256=bundle_sha,
            feature_schema_path=feature_schema_path,
            metrics_path=metrics_path,
            calibration_path=calibration_path,
            feature_importance_path=fi_path,
            shap_summary_path=None,
            min_feature_values_path=min_path,
            max_feature_values_path=max_path,
            predictions_path=predictions_path,
        )

        print(json.dumps({"ok": True, "artifacts": asdict(artifacts)}, ensure_ascii=False))
        return 0
    finally:
        try:
            conn.close()
        except Exception:
            pass


if __name__ == "__main__":
    raise SystemExit(main())
