# 004 Add Scraper Scoring Tables

This migration adds persistent analytics tables for scraper ranking, score explainability, and momentum tracking.

## SQL file

- `Database/Analytics/004_AddScraperScoringTables.sql`

## Created objects

Core:
- `items`
- `item_sources`
- `runs`
- `item_run_stats`
- `score_components`

Extended:
- `item_price_history`
- `item_images`
- `item_market_stats`

View:
- `vw_latest_item_scores`

## Apply migration

```bash
psql -h localhost -U your_user -d analytics_db -f Database/Analytics/004_AddScraperScoringTables.sql
```

## Typical query (latest run)

```sql
SELECT *
FROM vw_latest_item_scores
ORDER BY final_score DESC NULLS LAST
LIMIT 10;
```

## Notes

- The script is idempotent (`CREATE ... IF NOT EXISTS`).
- Names are snake_case for easier Python + SQL integration.
- Foreign keys are defined for run/item lifecycle consistency.
