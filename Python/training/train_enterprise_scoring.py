import argparse
import hashlib
import json
import math
import os
import re
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional, Sequence, Tuple

import joblib
import numpy as np
import pandas as pd

try:
    import psycopg2  # type: ignore
except ImportError:
    psycopg2 = None  # type: ignore

from sklearn.decomposition import PCA
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import brier_score_loss, log_loss, roc_auc_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType


DEFAULT_FEATURE_COLUMNS: List[str] = [
    "price_fit",
    "margin",
    "popularity",
    "trend_momentum",
    "source_coverage",
    "local_demand",
    "image_similarity",
    "deal_score",
    "supplier_score",
    "season_score",
]

DEFAULT_TARGET_COLUMN = "sold"


@dataclass(frozen=True)
class TrainOutput:
    output_dir: str
    rows_total: int
    rows_train: int
    rows_val: int
    use_pca: bool
    pca_components: int
    metrics_raw: Dict[str, Optional[float]]
    metrics_calibrated: Dict[str, Optional[float]]
    model_onnx_path: str
    model_onnx_sha256: str
    feature_schema_path: str
    metrics_path: str
    calibration_path: str
    feature_importance_path: str
    shap_summary_path: Optional[str]
    min_feature_values_path: str
    max_feature_values_path: str
    weights_json_path: str
    scaler_json_path: str
    calibration_json_path: str
    metrics_json_path: str
    manifest_path: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Enterprise scoring training pipeline (StandardScaler + optional PCA + Logistic + Platt + ONNX).")
    parser.add_argument("--input-csv", default=None, help="Path to training CSV file.")
    parser.add_argument("--pg-url", default=None, help="postgresql:// connection string.")
    parser.add_argument("--ado-net-connection-string", default=None, help="ADO.NET connection string (Host=...;Database=...;Username=...;Password=...;).")
    parser.add_argument("--sql-query", default=None, help="SQL query to load dataset from PostgreSQL.")
    parser.add_argument("--table", default=None, help="Table/view name used when --sql-query is not provided.")
    parser.add_argument("--output-dir", required=True, help="Folder where artifacts are written.")
    parser.add_argument("--target-column", default=DEFAULT_TARGET_COLUMN)
    parser.add_argument("--feature-columns", default=",".join(DEFAULT_FEATURE_COLUMNS), help="Comma-separated feature column names.")
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument("--random-state", type=int, default=42)
    parser.add_argument("--pca-variance", type=float, default=0.95, help="Explained variance ratio for PCA, e.g. 0.95.")
    parser.add_argument("--use-pca", action="store_true", default=True)
    parser.add_argument("--no-pca", action="store_false", dest="use_pca")
    parser.add_argument("--max-iter", type=int, default=500)
    parser.add_argument("--class-weight-balanced", action="store_true", default=False)
    return parser.parse_args()


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def ensure_dir(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def write_json(path: str, payload: Any) -> None:
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2, default=str)


def sha256_file(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_ado_net_connection_string(connection_string: str) -> Dict[str, str]:
    parts: Dict[str, str] = {}
    for segment in connection_string.split(";"):
        segment = segment.strip()
        if not segment or "=" not in segment:
            continue
        key, value = segment.split("=", 1)
        parts[key.strip().lower()] = value.strip()

    def pick(*keys: str) -> Optional[str]:
        for key in keys:
            value = parts.get(key.lower())
            if value:
                return value
        return None

    host = pick("host", "server")
    dbname = pick("database", "initial catalog")
    user = pick("username", "user id", "userid", "user")
    password = pick("password", "pwd") or ""
    port = pick("port") or "5432"
    sslmode = pick("sslmode", "ssl mode")

    if not host or not dbname or not user:
        raise ValueError("ADO.NET connection string must contain Host/Database/Username.")

    dsn: Dict[str, str] = {
        "host": host,
        "dbname": dbname,
        "user": user,
        "password": password,
        "port": port,
    }
    if sslmode:
        dsn["sslmode"] = sslmode
    return dsn


def open_postgres_connection(pg_url: Optional[str], ado_connection_string: Optional[str]):
    if psycopg2 is None:
        raise RuntimeError("Missing dependency: psycopg2-binary")

    if pg_url:
        return psycopg2.connect(pg_url)
    if ado_connection_string:
        return psycopg2.connect(**parse_ado_net_connection_string(ado_connection_string))
    raise ValueError("Provide --input-csv, or PostgreSQL connection (--pg-url or --ado-net-connection-string).")


def parse_feature_columns(raw: str) -> List[str]:
    columns = [column.strip() for column in raw.split(",") if column.strip()]
    if not columns:
        raise ValueError("feature columns cannot be empty.")
    return columns


def load_dataframe(args: argparse.Namespace) -> pd.DataFrame:
    if args.input_csv:
        return pd.read_csv(args.input_csv)

    table = args.table or "vw_enterprise_scoring_training"
    if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", table):
        raise ValueError("table must be a simple identifier (no schema/quotes).")

    query = args.sql_query or f"SELECT * FROM {table};"
    connection = open_postgres_connection(args.pg_url, args.ado_net_connection_string)
    try:
        return pd.read_sql_query(query, connection)
    finally:
        try:
            connection.close()
        except Exception:
            pass


def sanitize_dataset(df: pd.DataFrame, feature_columns: Sequence[str], target_column: str) -> Tuple[np.ndarray, np.ndarray, pd.DataFrame]:
    missing = [column for column in list(feature_columns) + [target_column] if column not in df.columns]
    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    base = df[list(feature_columns) + [target_column]].copy()
    for column in feature_columns:
        base[column] = pd.to_numeric(base[column], errors="coerce")
    base[target_column] = pd.to_numeric(base[target_column], errors="coerce")
    base = base.dropna(subset=[target_column]).copy()
    if base.empty:
        raise ValueError("No rows after target cleanup.")

    y_float = base[target_column].astype(float).values
    y = (y_float >= 0.5).astype(np.int32)
    for column in feature_columns:
        median_value = float(base[column].median()) if base[column].notna().any() else 0.0
        base[column] = base[column].fillna(median_value)

    x = base[list(feature_columns)].astype(np.float32).values
    if len(np.unique(y)) < 2:
        raise ValueError("Target must contain at least two classes (0 and 1).")

    return x, y, base


def sigmoid(values: np.ndarray) -> np.ndarray:
    clipped = np.clip(values, -35.0, 35.0)
    return 1.0 / (1.0 + np.exp(-clipped))


def safe_auc(y_true: np.ndarray, y_prob: np.ndarray) -> Optional[float]:
    if np.min(y_true) == np.max(y_true):
        return None
    return float(roc_auc_score(y_true, y_prob))


def evaluate_binary(y_true: np.ndarray, y_prob: np.ndarray) -> Dict[str, Optional[float]]:
    eps = 1e-7
    probabilities = np.clip(y_prob, eps, 1.0 - eps)
    return {
        "auc": safe_auc(y_true, probabilities),
        "log_loss": float(log_loss(y_true, probabilities)),
        "brier": float(brier_score_loss(y_true, probabilities)),
    }


def build_onnx_pipeline(
    scaler: StandardScaler,
    pca: Optional[PCA],
    model: LogisticRegression,
    feature_count: int,
    output_path: str,
) -> None:
    steps = [("scaler", scaler)]
    if pca is not None:
        steps.append(("pca", pca))
    steps.append(("classifier", model))
    pipeline = Pipeline(steps)
    initial_type = [("float_input", FloatTensorType([None, feature_count]))]
    onnx_model = convert_sklearn(pipeline, initial_types=initial_type)
    with open(output_path, "wb") as handle:
        handle.write(onnx_model.SerializeToString())


def train(args: argparse.Namespace) -> TrainOutput:
    feature_columns = parse_feature_columns(args.feature_columns)
    target_column = args.target_column

    df = load_dataframe(args)
    x, y, _ = sanitize_dataset(df, feature_columns, target_column)

    stratify = y if len(np.unique(y)) > 1 else None
    x_train, x_val, y_train, y_val = train_test_split(
        x,
        y,
        test_size=args.test_size,
        random_state=args.random_state,
        stratify=stratify,
    )

    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(x_train)
    x_val_scaled = scaler.transform(x_val)

    pca: Optional[PCA] = None
    if args.use_pca:
        pca = PCA(n_components=args.pca_variance, random_state=args.random_state)
        x_train_model = pca.fit_transform(x_train_scaled)
        x_val_model = pca.transform(x_val_scaled)
    else:
        x_train_model = x_train_scaled
        x_val_model = x_val_scaled

    class_weight = "balanced" if args.class_weight_balanced else None
    base_model = LogisticRegression(
        max_iter=args.max_iter,
        solver="lbfgs",
        class_weight=class_weight,
        random_state=args.random_state,
    )
    base_model.fit(x_train_model, y_train)

    z_train = base_model.decision_function(x_train_model).astype(np.float64)
    z_val = base_model.decision_function(x_val_model).astype(np.float64)

    raw_train_prob = sigmoid(z_train)
    raw_val_prob = sigmoid(z_val)

    platt_model = LogisticRegression(
        max_iter=300,
        solver="lbfgs",
        random_state=args.random_state,
    )
    platt_model.fit(z_val.reshape(-1, 1), y_val)

    calibrated_val_prob = platt_model.predict_proba(z_val.reshape(-1, 1))[:, 1]

    # Convert model with optional PCA back to canonical standardized feature space:
    # z = effective_weights · standardized_features + effective_bias
    raw_coef = base_model.coef_[0].astype(np.float64)
    raw_bias = float(base_model.intercept_[0])
    if pca is not None:
        effective_weights = (pca.components_.T @ raw_coef).astype(np.float64)
        effective_bias = raw_bias - float(np.dot(raw_coef, pca.mean_.astype(np.float64)))
    else:
        effective_weights = raw_coef
        effective_bias = raw_bias

    metrics_raw = evaluate_binary(y_val, raw_val_prob)
    metrics_calibrated = evaluate_binary(y_val, calibrated_val_prob)

    ensure_dir(args.output_dir)
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
    run_dir = os.path.join(args.output_dir, f"enterprise_run_{timestamp}")
    ensure_dir(run_dir)

    onnx_path = os.path.join(run_dir, "enterprise_scoring_model.onnx")
    build_onnx_pipeline(
        scaler=scaler,
        pca=pca,
        model=base_model,
        feature_count=len(feature_columns),
        output_path=onnx_path,
    )
    onnx_sha256 = sha256_file(onnx_path)

    weights_json_path = os.path.join(run_dir, "enterprise_scoring_weights.json")
    scaler_json_path = os.path.join(run_dir, "enterprise_scoring_scaler.json")
    calibration_json_path = os.path.join(run_dir, "enterprise_scoring_calibration.json")
    metrics_json_path = os.path.join(run_dir, "enterprise_scoring_metrics.json")
    feature_schema_path = os.path.join(run_dir, "feature_schema.json")
    min_feature_values_path = os.path.join(run_dir, "min_feature_values.json")
    max_feature_values_path = os.path.join(run_dir, "max_feature_values.json")
    manifest_path = os.path.join(run_dir, "enterprise_scoring_manifest.json")

    feature_weight_map = {
        feature_columns[index]: float(effective_weights[index])
        for index in range(len(feature_columns))
    }
    feature_min_values = {
        feature_columns[index]: float(np.min(x_train[:, index]))
        for index in range(len(feature_columns))
    }
    feature_max_values = {
        feature_columns[index]: float(np.max(x_train[:, index]))
        for index in range(len(feature_columns))
    }

    weights_payload = {
        "generated_at_utc": utc_now_iso(),
        "feature_columns": feature_columns,
        "target_column": target_column,
        "weights": [float(value) for value in effective_weights.tolist()],
        "feature_weights": feature_weight_map,
        "bias": float(effective_bias),
        "model_type": "logistic_regression",
        "input_dimension_after_transform": int(base_model.coef_.shape[1]),
        "scaler_mean": scaler.mean_.astype(float).tolist(),
        "scaler_scale": scaler.scale_.astype(float).tolist(),
        "has_pca_projection": bool(pca is not None),
        "pre_projection_weights": raw_coef.astype(float).tolist(),
        "pre_projection_bias": float(raw_bias),
    }

    scaler_payload = {
        "generated_at_utc": utc_now_iso(),
        "feature_columns": feature_columns,
        "scaler_mean": scaler.mean_.astype(float).tolist(),
        "scaler_scale": scaler.scale_.astype(float).tolist(),
        "use_pca": bool(pca is not None),
        "pca_variance": float(args.pca_variance),
        "pca_components": pca.components_.astype(float).tolist() if pca is not None else None,
        "pca_mean": pca.mean_.astype(float).tolist() if pca is not None else None,
        "pca_explained_variance_ratio": pca.explained_variance_ratio_.astype(float).tolist() if pca is not None else None,
        "pca_n_components": int(pca.n_components_) if pca is not None else len(feature_columns),
    }

    a = float(platt_model.coef_[0][0])
    b = float(platt_model.intercept_[0])
    calibration_payload = {
        "generated_at_utc": utc_now_iso(),
        "method": "platt_sigmoid",
        "platt_A": a,
        "platt_B": b,
        "formula": "p_final = sigmoid(platt_A * z + platt_B)",
        "where_z_is": "logistic_decision_function_after_scaler_and_optional_pca",
    }

    metrics_payload = {
        "generated_at_utc": utc_now_iso(),
        "rows_total": int(len(x)),
        "rows_train": int(len(x_train)),
        "rows_validation": int(len(x_val)),
        "class_rate_train": float(np.mean(y_train)),
        "class_rate_validation": float(np.mean(y_val)),
        "raw": metrics_raw,
        "calibrated": metrics_calibrated,
        "training_config": {
            "use_pca": bool(pca is not None),
            "pca_variance": float(args.pca_variance),
            "test_size": float(args.test_size),
            "random_state": int(args.random_state),
            "max_iter": int(args.max_iter),
            "class_weight_balanced": bool(args.class_weight_balanced),
        },
    }

    feature_schema_payload = [{"name": name, "dtype": "float32"} for name in feature_columns]

    write_json(weights_json_path, weights_payload)
    write_json(scaler_json_path, scaler_payload)
    write_json(calibration_json_path, calibration_payload)
    write_json(metrics_json_path, metrics_payload)
    write_json(feature_schema_path, feature_schema_payload)
    write_json(min_feature_values_path, feature_min_values)
    write_json(max_feature_values_path, feature_max_values)

    joblib.dump(scaler, os.path.join(run_dir, "scaler.pkl"))
    if pca is not None:
        joblib.dump(pca, os.path.join(run_dir, "pca.pkl"))
    joblib.dump(base_model, os.path.join(run_dir, "logistic_base_model.pkl"))
    joblib.dump(platt_model, os.path.join(run_dir, "platt_model.pkl"))

    output = TrainOutput(
        output_dir=run_dir,
        rows_total=int(len(x)),
        rows_train=int(len(x_train)),
        rows_val=int(len(x_val)),
        use_pca=bool(pca is not None),
        pca_components=int(pca.n_components_) if pca is not None else len(feature_columns),
        metrics_raw=metrics_raw,
        metrics_calibrated=metrics_calibrated,
        model_onnx_path=onnx_path,
        model_onnx_sha256=onnx_sha256,
        feature_schema_path=feature_schema_path,
        metrics_path=metrics_json_path,
        calibration_path=calibration_json_path,
        feature_importance_path=weights_json_path,
        shap_summary_path=None,
        min_feature_values_path=min_feature_values_path,
        max_feature_values_path=max_feature_values_path,
        weights_json_path=weights_json_path,
        scaler_json_path=scaler_json_path,
        calibration_json_path=calibration_json_path,
        metrics_json_path=metrics_json_path,
        manifest_path=manifest_path,
    )
    write_json(manifest_path, asdict(output))
    return output


def main() -> int:
    args = parse_args()
    output = train(args)
    print(json.dumps({"ok": True, "artifacts": asdict(output)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
