# Supplier Decision Nullability Audit

Date: 2026-08-05

This audit lists every supplier-decision field read through `GetInt32`, `GetDecimal`, or `GetString` in `Api/Endpoints/SupplierDecisionHubEndpoints.cs`.

It does not change runtime behavior. The goal is to document where a `DBNull` fallback is safe and where it could fabricate a real-looking zero or empty value.

`GetDateTime` and `GetBoolean` are out of scope for this pass.

## Summary

- Numeric score and metric fields are mostly safe to coerce to zero because the surrounding contract already treats zero as conservative or insufficient signal.
- Text helper fields are usually safe to coerce to empty string when the UI can display a neutral fallback.
- Identifier fields and recommendation codes are the highest-risk cases because a fabricated zero or empty value can look like a real entity or a real recommendation.

## Classification Table

### Summary row reader

| Field | Helper | Classification | Note |
| --- | --- | --- | --- |
| `supplier_id` | `GetInt32` | needs explicit unavailable flag | A fabricated `0` would look like a real key. Current SQL should not return null here, but a reader fallback would still hide the defect. |
| `supplier_name` | `GetString` | empty string acceptable | `NormalizeSupplierName(...)` turns blank into `Nepoznat dobavljac` and `SupplierNameMissing` tracks the missingness. |
| `recommendation_code` | `GetString` | needs explicit unavailable flag | Missing recommendation code can alter downstream recommendation semantics. |
| `confidence_score` | `GetDecimal` | observed zero is OK | `BuildRecommendationSignal(...)` already maps `<= 0` to `insufficient_data`. |
| `revenue` | `GetDecimal` | observed zero is OK | Derived metric; zero is conservative and already used in empty-state handling. |
| `units` | `GetDecimal` | observed zero is OK | Derived metric; zero is conservative. |
| `fullprice_revenue_share` | `GetDecimal` | observed zero is OK | Ratio metric already has a bounded zero fallback. |
| `fullprice_sellthrough` | `GetDecimal` | observed zero is OK | Ratio metric already has a bounded zero fallback. |
| `markdown_revenue_share` | `GetDecimal` | observed zero is OK | Ratio metric already has a bounded zero fallback. |
| `pre_markdown_margin_pct` | `GetDecimal` | observed zero is OK | Ratio metric already has a bounded zero fallback. |
| `dead_stock_rate` | `GetDecimal` | observed zero is OK | Conservative fallback for missing or empty signal. |
| `unsold_stock_value` | `GetDecimal` | observed zero is OK | Zero is a conservative fallback for missing value evidence. |
| `repeat_winner_rate` | `GetDecimal` | observed zero is OK | Ratio metric; zero means no repeat signal. |
| `markdown_dependency_score` | `GetDecimal` | observed zero is OK | Score is derived and clamped, so zero is conservative. |
| `stock_risk_score` | `GetDecimal` | observed zero is OK | Score is derived and clamped, so zero is conservative. |
| `return_rate` | `GetDecimal` | observed zero is OK | Zero is the conservative fallback for missing return evidence. |
| `category_focus_score` | `GetDecimal` | observed zero is OK | Derived score; zero is a safe fallback. |
| `ml_supplier_score` | `GetDecimal` | observed zero is OK | ML signal is optional and already has a safe fallback to supplier quality. |
| `ai_explanation` | `GetString` | empty string acceptable | Display-only ML text can be blank without fabricating a score. |
| `top_feature_1` | `GetString` | empty string acceptable | Display-only ML text can be blank. |
| `top_feature_2` | `GetString` | empty string acceptable | Display-only ML text can be blank. |
| `top_feature_3` | `GetString` | empty string acceptable | Display-only ML text can be blank. |
| `supplier_quality_index` | `GetDecimal` | observed zero is OK | Final score is derived and already uses conservative defaults. |

### Category breakdown reader

| Field | Helper | Classification | Note |
| --- | --- | --- | --- |
| `category` | `GetString` | empty string acceptable | The SQL already coalesces to `Uncategorized` before the reader sees the value. |
| `revenue` | `GetDecimal` | observed zero is OK | Aggregated numeric output. |
| `units` | `GetDecimal` | observed zero is OK | Aggregated numeric output. |
| `fullprice_revenue_share` | `GetDecimal` | observed zero is OK | Aggregated ratio output. |
| `fullprice_sellthrough` | `GetDecimal` | observed zero is OK | Aggregated ratio output. |
| `markdown_revenue_share` | `GetDecimal` | observed zero is OK | Aggregated ratio output. |
| `dead_stock_rate` | `GetDecimal` | observed zero is OK | Aggregated ratio output. |
| `unsold_stock_value` | `GetDecimal` | observed zero is OK | Aggregated value output. |
| `repeat_winner_rate` | `GetDecimal` | observed zero is OK | Aggregated ratio output. |

### Article decision reader

| Field | Helper | Classification | Note |
| --- | --- | --- | --- |
| `article_id` | `GetInt32` | needs explicit unavailable flag | A fabricated `0` would look like a real article key. |
| `sku` | `GetString` | empty string acceptable | Display-only identifier string; blank is neutral but should not be mistaken for a real SKU. |
| `article_name` | `GetString` | empty string acceptable | Display-only string; blank is neutral. |
| `category` | `GetString` | empty string acceptable | The SQL already coalesces category to `Uncategorized`. |
| `pre_revenue_30d` | `GetDecimal` | observed zero is OK | Aggregated metric. |
| `post_revenue_30d` | `GetDecimal` | observed zero is OK | Aggregated metric. |
| `pre_sellthrough_30d` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `pre_margin_30d` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `markdown_revenue_share` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `stock_before_markdown` | `GetDecimal` | observed zero is OK | Inventory-derived metric; zero is conservative. |
| `signal_quality_flag` | `GetString` | empty string acceptable | Display-only quality label; current SQL already uses conservative fallback values. |
| `signal_quality_reason` | `GetString` | empty string acceptable | Display-only explanation text; blank is neutral. |

### Recommendation history reader

| Field | Helper | Classification | Note |
| --- | --- | --- | --- |
| `recommendation_code` | `GetString` | needs explicit unavailable flag | This drives the history recommendation text and should not silently degrade to a generic code. |
| `revenue` | `GetDecimal` | observed zero is OK | Aggregated metric. |
| `fullprice_revenue_share` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `markdown_revenue_share` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `fullprice_sellthrough` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `pre_markdown_margin_pct` | `GetDecimal` | observed zero is OK | Aggregated ratio. |
| `recommendation_title` | `GetString` | empty string acceptable | Display-only text derived from the code; blank is neutral but should not be treated as a signal. |
| `recommendation_reason` | `GetString` | empty string acceptable | Display-only text derived from the code; blank is neutral. |

## Highest-risk fields

These are the fields that deserve a follow-up if the SQL contract ever loosens:

- `supplier_id`
- `article_id`
- `recommendation_code`

For now, the codebase preserves the current helper behavior and keeps the risk visible through tests and this audit rather than changing the reader helpers globally.

## Follow-up split

If any of the highest-risk fields can become nullable in a future schema change, split that work into a smaller prompt that adds an explicit unavailable flag or nullable DTO field instead of changing the helpers in place.
