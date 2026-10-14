# RQ665 evidence

- Failing runs: 38422963322 (PR #147, base 6ca11bf9) and 38419332741 (babd557d): 3 SupplierSalesStatsEndpointPostgresParityTests failed with ManyServiceProvidersCreatedWarning; same base passed on main run 38422316417.
- Root cause: a per-host application service provider is part of the EF Core internal service-provider cache key; WebApplicationFactory hosts accumulate past 20 per process and the warning throws, order-dependently.
- Fix: repo-standard warning opt-out added to 11 test-host factories (29 registrations); new TestHostDbContextWarningConventionTests (1 scan + 2 self-tests).
- Local Testing env: convention tests + SupplierSalesStatsEndpointPostgresParityTests + ShoeTypeRemainingContractsPostgresTests + PostgresRequiredAttributeConventionTests 14 total, 8 passed, 6 skipped, 0 failed; AnalyticsCriticalRouteMappingsTests 8/8 passed. CachedAnalyticsFailureContractTests crashes the local test host on unchanged origin/main too (pre-existing, local only; green in CI).
- PR #158 CI: backend run 38565788120 pass, planning governance run 38565788009 pass, frontend/POS/SDK/RQ453 pass; Vercel failed on deployment rate limit only (not a required check).
- Merge (--merge): 85d29cd275ee4ab5d3975f0bc522d078e1ca7930
