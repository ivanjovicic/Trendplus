# Trendplus: Runtime Scoring and Derived Analytics Backend

## Context

Trendplus is a .NET 8 backend for a footwear retailer. It covers POS transactions, inventory, pricing, and marketplace-driven scoring. I owned the backend: API, CQRS pipeline, PostgreSQL model, workers, scoring engine, and production operations.

## Problem

Three constraints drove most design choices. POS writes had to stay fast. Analytical queries could consume pages and connections needed by live transactions. External services could fail independently, but scoring and event flow still had to return a safe result.

## Decision: Runtime Scoring Engine

The `RuntimeScoringEngine` combines marketplace signals, scraper scores, and local demand indicators into a sell-probability score. A PostgreSQL feature store pre-materializes 28 features, so inference reads one product snapshot:

```sql
SELECT product_id, price, volatility, momentum, demand_score, sentiment
FROM feature_store_product_snapshot
WHERE product_id = @productId;
```

Reads use `AsNoTracking`.

ONNX inference runs in-process â€” loaded at startup, pure CPU, no network hop:

```csharp
var input = new DenseTensor<float>(features, new[] { 1, features.Length });
var result = _session.Run(new[] { NamedOnnxValue.CreateFromTensor("input", input) });
var prediction = result.First().AsEnumerable<float>().First();
```

I kept Python prediction as the external fallback. ONNX gives lower latency, while Python stays available for degraded operation. The heuristic score remains first-class because pricing decisions still need explainable behavior.

```csharp
var externalProbability = onnx?.Prediction ?? python.SellProbability;
var finalScore = ResolveFinalSellProbability(
    heuristicSellProbability, externalProbability,
    enterprise?.FinalProbability,
    usedOnnxModel: onnx is not null,
    hasFeatureStoreFeatures: featureStoreSnapshot is not null);
```

Weights shift as services degrade: ONNX+features 70/30, Python-only 60/40, fully degraded 100% heuristic.

## Decision: Derived Data Architecture

The data layer is split into three PostgreSQL databases: transactional, analytics, and ML training, each with its own `DbContext`. That keeps POS and inventory writes isolated from reporting scans and training I/O.

For analytics I used two derived-data patterns. Summary tables store incrementally projected facts. Materialized views hold heavier periodic rollups. For dashboard-heavy paths I used raw SQL through Npgsql, because `FILTER` clauses and window functions needed direct query control:

```sql
SELECT product_id,
       SUM(quantity) FILTER (WHERE sale_date > now() - interval '7 days') AS sales_7d
FROM sales
GROUP BY product_id;
```

Composite indexes support the main dashboard groupings:

```sql
CREATE INDEX idx_sales_date_category_supplier
ON sales (sale_date, category_id, supplier_id);
```

Partial indexes (`WHERE "IsPrimary" = true`) reduce B-tree size; HNSW indexes handle semantic search.

Views refresh nightly with `CONCURRENTLY`. A worker acquires an advisory lock before refresh:

```csharp
const string sql = "SELECT pg_try_advisory_lock(@k1, @k2);";
await using var cmd = new NpgsqlCommand(sql, connection);
cmd.Parameters.AddWithValue("k1", LockKey1);
cmd.Parameters.AddWithValue("k2", LockKey2);
return await cmd.ExecuteScalarAsync(ct) is true;
```

## Decision: Outbox and Worker Pipeline

Every sale event is written to the outbox in the same transaction as the sale:

```csharp
await _outbox.PublishAsync("SaleCreated", new { saleId }, ct);
// committed atomically with the POS sale â€” no event lost
```

The `OutboxProcessorWorker` polls in batches of 50 and projects idempotently into analytics tables:

```sql
INSERT INTO analytics_sales (sale_id, product_id, amount, sale_date)
VALUES (@saleId, @productId, @amount, @saleDate)
ON CONFLICT (sale_id)
DO UPDATE SET amount = EXCLUDED.amount;
```

That makes retries safe: replaying the same event cannot inflate aggregates or duplicate facts. I chose polling over CDC because event volume fit simple intervals and CDC would add more overhead.

Six `IHostedService` workers can be controlled at runtime through the API. The ML training worker uses PostgreSQL itself as a queue:

```sql
SELECT id FROM training_run
WHERE status = 'queued'
FOR UPDATE SKIP LOCKED LIMIT 1;
```

`SKIP LOCKED` lets multiple workers claim distinct jobs without blocking each other. If one worker dies mid-transaction, PostgreSQL makes the job visible again.

## Reliability

Reliability here is mainly about protecting PostgreSQL and keeping failure domains separate. Rate limiting classifies endpoints by cost, so expensive analytics traffic is shed before it can consume connections needed by POS and inventory operations:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("analytics", o =>
    {
        o.PermitLimit = 20;
        o.Window = TimeSpan.FromMinutes(1);
    });
});
```

A hybrid cache (L1 memory + L2 Redis) absorbs repeated reads before they reach PostgreSQL. If Redis is unavailable, the system degrades to L1 instead of stampeding the database. External calls use Polly v8, with retry for transient faults and circuit breakers for sustained failure:

```csharp
services.AddHttpClient("marketplace")
    .AddResilienceHandler("default", builder =>
    {
        builder.AddRetry(new() { MaxRetryAttempts = 3, BackoffType = DelayBackoffType.Exponential });
        builder.AddCircuitBreaker(new() { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(30) });
    });
```

When RabbitMQ is unavailable, the outbox keeps accumulating events in PostgreSQL and drains them after recovery.

## Result

The main result was stable behavior under mixed workloads. Dashboards read from pre-aggregated data instead of competing with POS writes. Scoring returns a bounded-latency response even when ONNX, Python, or external signals are partially unavailable. Workers scale horizontally through `SKIP LOCKED`, and idempotent projections keep analytical state correct during retries and replay.

## Development Approach

I used GitHub Copilot and LLM tools for MediatR boilerplate, SQL migrations, Polly setup, and log analysis. All output was reviewed before merge.
