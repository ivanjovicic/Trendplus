## Engineering Experience — Trendplus Retail Analytics Platform

Ivan Jovicic

### Context

Trendplus is a .NET 8 backend platform for a footwear retailer. It supports POS transactions, inventory management, pricing decisions, and marketplace-driven product scoring.

I led the backend architecture and implementation, including API design, CQRS pipeline, PostgreSQL data modeling, worker orchestration, scoring infrastructure, and production operations.

### Problem

The main challenge was resource contention between transactional and analytical workloads. POS writes had to stay fast, while analytics dashboards required heavier aggregations against the same tables and database connections used by transactional traffic. External dependencies could also fail independently, but the system still had to return a usable score and keep processing reliable. The system also had to run on modest infrastructure and could not rely on external ML services always being available.

### Decision: Runtime Scoring Engine

The **RuntimeScoringEngine** combines marketplace signals, scraper data, and internal demand indicators into a sell-probability score. To avoid expensive joins during inference, I built a PostgreSQL feature-store layer that pre-materializes 28 product features. At runtime the service reads a single snapshot:

```sql
SELECT product_id, price, volatility, momentum, demand_score, sentiment
FROM feature_store_product_snapshot
WHERE product_id = @productId;
```

EF Core reads use **AsNoTracking**.

I used ONNX Runtime directly in the application process. The model loads at application startup and runs on CPU, so there is no network hop:

```csharp
var input = new DenseTensor<float>(features, new[] { 1, features.Length });
var result = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", input) });
var prediction = result.First().AsEnumerable<float>().First();
```

Python inference remains available as a fallback path. I kept the heuristic score as a first-class input because pricing decisions still require explainable behavior.

As system signals degrade, weight shifts toward the heuristic path. ONNX plus features runs 70/30, Python-only 60/40, and the fully degraded path is 100% heuristic.

### Decision: Derived Data Architecture

The data layer is split into three PostgreSQL databases: transactional, analytics, and ML training. Each has its own **DbContext**. I separated them so reporting and training do not interfere with POS writes.

Analytics relies on two derived-data patterns: summary tables for projected facts and materialized views for heavier rollups. For dashboard endpoints I used raw SQL through Npgsql, because **FILTER** clauses and window functions were easier to control directly. Partial indexes (**WHERE "IsPrimary" = true**) keep index size down, while HNSW indexes support semantic search. Materialized views refresh nightly using **REFRESH MATERIALIZED VIEW CONCURRENTLY**, coordinated with advisory locks so refresh jobs do not overlap.

### Decision: Outbox and Worker Pipeline

For event delivery I used a transactional outbox. Every sale event is written in the same transaction as the sale, so the event and POS write commit atomically.

The **OutboxProcessorWorker** polls in batches of 50 and projects idempotently into analytics tables:

```sql
INSERT INTO analytics_sales (sale_id, product_id, amount, sale_date)
VALUES (@saleId, @productId, @amount, @saleDate)
ON CONFLICT (sale_id)
DO UPDATE SET amount = EXCLUDED.amount;
```

Retries are safe: replaying the same event cannot inflate aggregates or duplicate facts. I considered CDC, but polling was simpler at this volume.

The ML training worker uses PostgreSQL as a queue:

```sql
SELECT id FROM training_run
WHERE status = 'queued'
FOR UPDATE SKIP LOCKED LIMIT 1;
```

**SKIP LOCKED** lets multiple workers claim jobs concurrently without blocking each other. If a worker fails mid-transaction, the job becomes visible again.

### Reliability

Most reliability work focused on protecting PostgreSQL and isolating failure domains. Rate limiting groups API endpoints by cost so expensive analytics queries are shed before they consume connections needed by POS operations.

A hybrid cache (L1 memory + L2 Redis) absorbs repeated reads before they reach PostgreSQL. External calls use Polly v8 with retries for transient faults and circuit breakers for sustained failures.

If Redis or RabbitMQ is unavailable, the system falls back to memory caching and drains events after recovery.

### Results

Dashboards read from derived tables and materialized views, so POS latency stays predictable during reporting spikes. Typical dashboard queries complete in under 100ms because they read pre-aggregated data. The scoring endpoint still returns a bounded-latency response when services fail. Workers scale horizontally using **SKIP LOCKED** job acquisition, and idempotent projections keep analytical state correct during replay. This architecture scales mainly by adding workers or read replicas, without structural changes.

### Development Approach

During development I used Copilot and LLM tools as accelerators for MediatR boilerplate, SQL migrations, and log analysis. Generated code was always reviewed and adjusted before merging.
