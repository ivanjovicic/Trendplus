# Open Product Training 2.0 (Python)

This folder contains the **SellProbabilityRS 2.0** training pipeline:

- reads features from `vw_product_training_export` (PostgreSQL)
- trains a **LightGBM regressor** for `sell_probability_rs_label`
- evaluates (RMSE + basic AUC proxy + Brier/MSE)
- generates global feature importance (+ optional SHAP)
- exports model to **ONNX**
- writes artifacts (`*.json`, `model.onnx`) to an output folder

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

## Notes

- You must run label recomputation first (`POST /api/training/recompute-labels`) so
  `sell_probability_rs_label` exists.
- This script is designed to be called by a .NET worker later (training_run queue).

