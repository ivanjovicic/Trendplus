# Open Product Training 2.0 (Python)

This folder contains the **SellProbabilityRS 2.0** training pipeline:

- reads features from `vw_product_training_export` (PostgreSQL)
- trains a **LightGBM regressor** for `sell_probability_rs_label`
- evaluates (RMSE + basic AUC proxy + Brier/MSE)
- generates global feature importance (+ optional SHAP)
- exports model to **ONNX**
- writes artifacts (`*.json`, `model.onnx`) to an output folder

It also contains an **Enterprise Logistic + Platt** pipeline:

- reads training data from CSV or PostgreSQL
- standardizes features (StandardScaler)
- optional PCA dimensionality reduction
- trains Logistic Regression
- trains Platt calibration layer (`A`, `B`)
- reports AUC / LogLoss / Brier
- exports JSON artifacts for C# runtime and ONNX model

## Install (separate from scraper deps)

Use a dedicated venv (recommended):

```bash
python -m venv .venv-training
.\.venv-training\Scripts\activate
pip install -r Python/training/requirements.txt
```

## Run

```bash
python Python/training/train_sell_probability_rs.py ^
  --ado-net-connection-string "Host=...;Database=...;Username=...;Password=...;" ^
  --output-dir out/models/sell_probability_rs ^
  --take 500000
```

If you have a `postgresql://` URL instead:

```bash
python Python/training/train_sell_probability_rs.py --pg-url "postgresql://user:pass@host:5432/db" --output-dir out/models/sell_probability_rs
```

Enterprise training from CSV:

```bash
python Python/training/train_enterprise_scoring.py ^
  --input-csv training_dataset.csv ^
  --output-dir out/models/enterprise_scoring ^
  --use-pca
```

Enterprise training from PostgreSQL:

```bash
python Python/training/train_enterprise_scoring.py ^
  --ado-net-connection-string "Host=...;Database=...;Username=...;Password=...;" ^
  --table vw_enterprise_scoring_training ^
  --output-dir out/models/enterprise_scoring
```

## Notes

- You must run label recomputation first (`POST /api/training/recompute-labels`) so
  `sell_probability_rs_label` exists.
- This script is designed to be called by a .NET worker later (training_run queue).
