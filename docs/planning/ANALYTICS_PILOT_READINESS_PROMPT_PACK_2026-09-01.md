# Analytics pilot readiness prompt pack - 2026-09-01

Status: planning candidates only; no candidate is READY

## Use

This pack turns the 2026-09-01 readiness assessment into bounded execution prompts. It does not replace the owner queues, change `MASTER_ROADMAP.md`, or bypass the blocked `STAB16` release gate. Before promotion, an owner must assign the candidate to the named program queue, confirm dependencies and preserve the single-READY rule.

Shared non-negotiable contract for every prompt:

- source truth, units, numerator/denominator, true zero, missing/unknown, no-baseline and freshness/fallback semantics must be explicit;
- no value becomes zero, healthy, fresh, delivered, executed or measured without evidence;
- test API, UI, export/report/action/notification parity when the value crosses those surfaces;
- record exact release SHA, fixture/source window and residual risk in durable evidence.

## Data analyst prompts

### PILOT-DATA-01 - Read-only source-to-endpoint reconciliation

Status: WAITING. Owner: STAB16 / Stabilization and Release.

Problem: No current evidence independently proves that source sales, stock, costs and supplier fields equal the analytics values shown for a bounded pilot period.

Evidence: The 2026-08-27 live audit had no `TRENDPLUS_AUDIT_DATABASE_URL`; data-quality score alone does not reconcile source totals or lineage.

Scope: A read-only reconciliation runner and evidence schema for one approved store/data scope and date window; no source mutation.

Read first: `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`, `docs/Analytics/PILOT_DATA_REQUIREMENTS.md`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

Do: Define source extract identifiers, expected totals/counts, joins, duplicate rules, currency/quantity units, acceptable tolerances and variance classification; compare source -> imported facts -> snapshots -> selected API payloads.

Tests: Deterministic fixture reconciliation including duplicate, return, missing-cost, unknown-supplier, true-zero and date-boundary rows; read-only production command only after access is supplied.

Acceptance: A signed artifact identifies source window, query versions, every variance, owner decision and endpoint result; unexplained variance blocks recommendation readiness.

Dependencies: Read-only provider/database access, approved pilot window and data owner.

### PILOT-DATA-02 - Import lineage and idempotency ledger

Status: WAITING. Owner: QDB / Data-source connectors.

Problem: Import completion is not sufficient if a later refresh cannot identify exactly which source file/batch/mapping produced each analytics window.

Evidence: Connector/checkpoint contracts exist, but pilot release requires source freshness and provenance that an operator can trace.

Scope: Import run lineage, mapping version, source watermark/checksum, accepted/rejected counts and idempotency behavior.

Read first: `docs/Analytics/PILOT_ONBOARDING_IMPORT_MAP.md`, `docs/Analytics/PILOT_DATA_REQUIREMENTS.md`, `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`.

Do: Persist and expose non-secret source/batch identity, mapping profile version, time zone, row outcome counts and duplicate/replay disposition; link refresh and analytics snapshot provenance to this record.

Tests: Same batch replay; changed mapping version; partial malformed batch; daylight-saving/date-only boundary; failed import must not advance freshness.

Acceptance: Every pilot analytics result can name its import lineage or is visibly blocked/unknown; replay never doubles commercial facts.

Dependencies: QDB owner promotion and source-specific identifier/mapping contract.

### PILOT-DATA-03 - Immutable analytics gold dataset

Status: WAITING. Owner: RQ / Analytics reliability.

Problem: Separate test fixtures do not provide a single human-auditable truth set for cross-surface semantics.

Evidence: The repository has broad focused testing but no declared release-authoritative fixture covering all important retail edge cases.

Scope: A compact deterministic seed pack and expected assertions only; do not change production formulas in this task.

Read first: `docs/ai/ANALYTICS_TEST_STRATEGY.md`, `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`, `docs/ANALYTICS_SEMANTIC_GUARDRAILS.md`.

Do: Define versioned sales, returns, stock, cost, supplier, out-of-stock, stale, missing and no-baseline rows with business-readable expected KPI/decision outcomes.

Tests: Fixture validates API DTOs, report/export projections and selected frontend labels against the same expected manifest.

Acceptance: Any formula/unit/period/recommendation drift fails one named gold-data assertion with a business explanation.

Dependencies: Data and business owner approval of expected outcomes; no live production data in fixtures.

### PILOT-DATA-04 - Forecast and inventory signal backtest

Status: WAITING. Owner: RQ inventory forecast foundation.

Problem: Forecast/OOS/replenishment signals may be technically present without measured historical accuracy or explicit untrusted provenance.

Evidence: Inventory snapshot materializer keeps provenance states, while roadmap evidence keeps forecast work contract-heavy and fail-closed.

Scope: Historical forecast-to-observed pairing, accuracy bands and recommendation gate behavior.

Read first: `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`, `docs/qa/ANALYTICS_INVENTORY_SIGNAL_RELIABILITY_AUDIT.md`, `docs/Analytics/INTELLIGENCE_README.md`.

Do: Define target, horizon, observed-stock provenance, stockout censoring, sample minimum and accuracy measures; reject unpaired/unknown snapshots from trusted forecast claims.

Tests: Trusted paired observation; stale/owner-unknown snapshot; sparse sample; stockout censoring; returned-count versus total-count.

Acceptance: UI/API can show measured performance only with a defined sample and provenance; all other states say unavailable or insufficient.

Dependencies: Durable observed inventory history and business acceptance of forecast error bands.

## Business analyst prompts

### PILOT-BIZ-01 - KPI decision dictionary and sign-off

Status: WAITING. Owner: RQ / Analytics reliability.

Problem: A formula can be implemented consistently yet still be commercially wrong if business meaning, exclusions and decision use are not approved.

Evidence: Methodology audits exist, but pilot release needs a signed business source for the KPIs that trigger action.

Scope: Pilot-critical sales, margin, stock, supplier, freshness and data-quality measures.

Read first: `docs/Analytics/KPI_METHODOLOGY_AUDIT.md`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`, `docs/roadmaps/BUSINESS_ROADMAP.md`.

Do: For every KPI define owner, source fields, unit, formula, inclusion/exclusion, true-zero/missing behavior, refresh cadence, UI label and allowed decision use.

Tests: Gold-data examples approved by business; negative examples for returns, missing cost, no baseline and low sample.

Acceptance: No recommendation-driving KPI lacks an approved definition or silently falls back to a different business meaning.

Dependencies: Named commercial owner and pilot assortment/source conventions.

### PILOT-BIZ-02 - Recommendation acceptance and outcome measurement protocol

Status: WAITING. Owner: DEX/RL / Decision intelligence.

Problem: Action status cannot prove business value; the pilot needs a repeatable method to assess whether recommendations helped.

Evidence: Outcome code distinguishes `pending` and `not_measured`, but current readiness still calls confidence calibration and action ledger partial.

Scope: Operator workflow for accept/reject/defer/execute/measure; no automated learning model.

Read first: `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`, `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

Do: Define baseline, counterfactual/control where feasible, observation window, qualitative versus measured evidence, who approves measured impact and when a result is ineligible.

Tests: Valid measured result; executed but not measured; rejected recommendation; delayed outcome; missing baseline must not produce realization rate.

Acceptance: Pilot reviewers can distinguish recommendation quality, operator choice and measured commercial result without inferring causality from status.

Dependencies: Business reviewer and a feasible measurement window per action family.

### PILOT-BIZ-03 - Notification policy and truthful message catalogue

Status: WAITING. Owner: STAB/DEX shared contract; split implementation by owner after approval.

Problem: A message that says "sent", "completed" or "urgent" has commercial and trust consequences, but no approved semantic catalogue governs recipient, severity and evidence.

Evidence: Outbox, alerts, scheduler and client toasts exist; no end-to-end correctness proof was found in release evidence.

Scope: Pilot-critical refresh, data-quality, action and outcome messages only.

Read first: `docs/ops/ANALYTICS_MONITORING_ALERTING.md`, `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

Do: Define event, authorized recipient/role, severity, exact Serbian copy, evidence link, deduplication window, retry/escalation rule and prohibited optimistic wording.

Tests: Queued versus delivered; stale versus fresh; duplicate event; unauthorized recipient; action pending versus executed; missing measurement.

Acceptance: Every pilot notification has one authoritative state meaning and an escalation owner; enqueue is never presented as successful delivery or business completion.

Dependencies: Business owner, security/authorization owner and channel/provider decision.

### PILOT-BIZ-04 - Bounded pilot acceptance and stop policy

Status: WAITING. Owner: STAB / Release.

Problem: "Pilot ready with warnings" is not operational until the business accepts which warnings allow observation and which force a decision stop.

Evidence: Existing runbook has escalation rules, but current live audit verdict is not ready for trusted actions.

Scope: One retailer, source window, operator group and time-boxed pilot exit criteria.

Read first: `docs/roadmaps/BUSINESS_ROADMAP.md`, `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`, `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`.

Do: Set permitted action families, red stop conditions, warning acceptance owner, daily reconciliation cadence, rollback method, success metrics and evidence retention.

Tests: Tabletop exercise for failed worker, stale dashboard, wrong recipient notification and disputed recommendation result.

Acceptance: A pilot cannot continue after a P0 truth breach without an explicit owner decision and recorded remediation.

Dependencies: Pilot retailer agreement, named operator/business owner and STAB16 evidence.

## .NET analyst prompts

### PILOT-NET-01 - Dedicated worker deployment and lifecycle proof

Status: WAITING. Owner: STAB16 / Stabilization and Release.

Problem: Worker registration logic is implemented, but production proof showed that the canonical service was web-only with analytics workers unavailable.

Evidence: `WorkerRuntimeConfig` and worker registry support `PROCESS_TYPE=worker`; the live audit observed six analytics jobs not registered in the web process.

Scope: Provider worker service, configuration validation, runtime status and durable job heartbeat only.

Read first: `Api/Config/WorkerRuntimeConfig.cs`, `Api/Services/AnalyticsRefreshStatusService.cs`, `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`.

Do: Deploy/configure the separate worker, prove registered expected jobs, capture start/success/failure timestamps and ensure the web process does not run heavy workers accidentally.

Tests: Runtime config unit tests; worker registry/refresh-status integration test; deployed read-only smoke for process mode and one completed refresh.

Acceptance: Production refresh status names the worker process and a durable successful analytics refresh; failed/missing worker becomes stale/critical with an actionable reason.

Dependencies: Provider deployment access and non-secret worker configuration.

### PILOT-NET-02 - Refresh-to-cache event parity under restart and multi-instance conditions

Status: WAITING. Owner: RQ with STAB deployment proof.

Problem: Cache-family invalidation is covered locally but can still serve stale, divergent values after a worker restart or on another instance.

Evidence: RQ134/RQ135 added local aggregate/data-quality cache invalidation coverage; production audit reported an in-memory-cache warning.

Scope: Analytics cache backend, invalidation version/timestamp and restart behavior for dashboard, product, supplier, inventory, reports and data quality.

Read first: `docs/Analytics/ANALYTICS_CACHE_POLICY.md`, `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`, `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`.

Do: Specify supported cache topology; prove invalidation event reaches every instance or explicitly degrade/disable shared-cache claims; expose version/timestamp/source.

Tests: Successful refresh invalidates all trust-bearing families; refresh failure changes none; second instance/restart reads new version; report payload cannot outlive its source snapshot.

Acceptance: A supported topology cannot present a prior-snapshot report/decision as fresh after a completed invalidating refresh.

Dependencies: Cache provider/topology decision and STAB worker evidence.

### PILOT-NET-03 - Transactional outbox delivery correctness

Status: WAITING. Owner: STAB / operational messaging.

Problem: Persisting an outbox record does not prove exactly-once user-visible delivery or truthful terminal state.

Evidence: `OutboxProcessorWorker`, broker and client pages exist, but no current pilot proof traces an analytics message through delivery and authorization.

Scope: Message state machine, idempotency key, retry/backoff, dead-letter/disposition, recipient authorization and correlation only.

Read first: `Workers/OutboxProcessorWorker.cs`, `Infrastructure/Services/OutboxService.cs`, `Api/Endpoints/OutboxEndpoints.cs`.

Do: Define authoritative statuses from created through delivered/failed, secure recipient resolution, payload version and correlation to the originating analytics event.

Tests: Duplicate publish; transient broker failure; permanent failure; retry after restart; unauthorized recipient; payload says queued until provider acknowledgement.

Acceptance: One source event cannot generate duplicate user action prompts, and every terminal UI/API message agrees with the durable outbox state.

Dependencies: Broker/provider delivery semantics and authorization owner.

### PILOT-NET-04 - Cross-surface analytics contract differential suite

Status: WAITING. Owner: RQ / Analytics reliability.

Problem: Unit tests can pass while endpoint, report, action projection and export disagree about an important decision value.

Evidence: The safety gate explicitly requires API/table/chart/detail/export/report/action parity; no single executable matrix is the release authority.

Scope: Backend contracts and projection/export builders for pilot-critical fields; frontend rendering is a separate React prompt.

Read first: `docs/ai/ANALYTICS_TEST_STRATEGY.md`, `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

Do: Build a manifest-driven suite for requested/effective period, scope, freshness, quality, recommendationAllowed, impact, count semantics and correlation/error meta.

Tests: True zero; unknown; empty; fallback; stale; partial; top-N truncation; unit mismatch; action/outcome projection.

Acceptance: A changed field is tested at each backend surface where it appears, with an explicit approved conversion where parity is not literal.

Dependencies: PILOT-DATA-03 gold dataset and contract owner approval.

### PILOT-NET-05 - Storage budget, retention and archive-write guard

Status: WAITING. Owner: STAB / SEC retention assurance, with implementation in the import/storage owner.

Problem: A cleanup operation wrote 582,788 deleted-row payloads into `deleted_rows_archive`, consuming 198 MB and pushing Neon storage from a 0.5 GB allowance to 0.54 GB. The system had no enforced size budget, retention policy, projection check or automatic distinction between rollback-required data and disposable operational archive.

Evidence: The 2026-09-01 pilot intake measured `deleted_rows_archive` at 198 MB; all rows were from `cleanup-non-access` between 2026-04-04 and 2026-05-06. The archive was later intentionally cleared after explicit rollback waiver. Existing code creates and writes the archive, while retention is separately defined for refresh history; no equivalent archive budget/retention guard is established for deleted-row payloads.

Scope: Define and implement a dedicated-pilot storage policy for deleted-row archives, import logs, error/performance logs, refresh history, rebuildable materialized views and other operational records. Include preflight size/projection checks, bounded retention, archive disable/overflow behavior, provider-quota alerting and operator evidence. Do not delete authoritative sales, stock, cost, return, supplier or audit facts in this prompt.

Read first: `Database/sql/create_deleted_rows_archive.sql`, `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Services/AccessImportService.cs`, `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs`, `docs/ops/POSTGRES_STORAGE_LIMIT_RUNBOOK.md`, `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`, `docs/qa/ANALYTICS_PILOT_READINESS_GAP_ASSESSMENT_2026-09-01.md`.

Do: Classify each storage class as authoritative, rollback/audit, rebuildable or disposable; define owner-approved retention and legal-hold behavior; add configurable maximum rows/age/bytes for deleted-row archive; prevent an archive write when the projected operation would breach the configured budget; make cleanup fail safely or continue without archive only when the contract explicitly permits it; expose current size, oldest record, projected growth and blocked reason through an admin-only diagnostic; add alerts at 70%, 85% and 95% of provider allowance; ensure cleanup is idempotent and never targets business tables implicitly; document backup/export and restore implications.

Tests: Archive write below budget; archive write at budget; projected overflow; missing/invalid budget; retention expiry; legal hold; cleanup retry/idempotency; archive unavailable; provider quota warning; large `row_json` cap; no archive write for a failed business deletion; proof that cleanup cannot delete `SalesFacts`, `SalesLineFacts`, `ReturnFacts`, `StoresDim` or audit tables; storage diagnostic authorization and secret redaction.

Acceptance: No routine import/cleanup can silently create unbounded rollback payloads; every archive write has an explicit retention class and size budget; a projected quota breach blocks or degrades with a visible actionable reason before business data is mutated; rebuildable data is preferred for regeneration over indefinite persistence; authoritative business/audit data is never removed by a storage-pressure fallback; operator can see size, retention and next action without exposing row payloads or credentials; docs include a recovery/rollback decision and provider-specific quota response.

Dependencies: Business/data owner approval of rollback retention; backup/restore proof; provider quota/plan details; STAB16 worker/deployment access; security review of admin diagnostics; no promotion to `READY` until the current storage incident has an owner and a tested retention decision.

## React analyst prompts

### PILOT-REACT-01 - Trust header completeness and source honesty

Status: WAITING. Owner: RQ / Analytics reliability.

Problem: Reusing `AnalyticsTrustHeader` is not enough when period, source, freshness or data quality is absent on a decision surface.

Evidence: Coverage audit shows several screens intentionally pass null/unknown fields, including inventory and action surfaces.

Scope: Pilot-critical routes only: dashboard, products, supplier, inventory, data quality, actions, Decision Board and durable reports.

Read first: `docs/ANALYTICS_TRUST_HEADER_COVERAGE.md`, `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`, `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

Do: Map each rendered trust datum to authoritative API fields; display unknown/unavailable without local `Date.now`, fabricated quality or optimistic copy; link warnings to next action.

Tests: Fresh; stale; unknown; partial; fallback; empty; API error; missing trust field; direct route refresh.

Acceptance: No pilot decision route visually implies a known period/freshness/quality value when the backend did not provide it.

Dependencies: PILOT-NET-04 contract manifest where an API field is currently absent.

### PILOT-REACT-02 - Truthful toast, alert and inbox state rendering

Status: WAITING. Owner: STAB/React implementation after PILOT-BIZ-03.

Problem: Client copy can turn enqueue/retry/unknown state into a misleading success message, especially around outbox and background operations.

Evidence: Outbox pages emit toast success messages; some text shows encoding damage (`ozna?eno`), and no end-to-end message-state proof was found.

Scope: Outbox, worker-status, inventory-alert and analytics-action message components; do not redesign unrelated global toast behavior.

Read first: `Klijent/clientapp/src/pages/OutboxDashboard.tsx`, `Klijent/clientapp/src/pages/OutboxMessagesPage.tsx`, `docs/ai/ENCODING_AND_TEXT_SAFETY.md`.

Do: Map authoritative backend statuses to approved Serbian copy, severity and safe next action; fix confirmed owned-scope mojibake; retain correlation/reference where useful.

Tests: Queued/retrying/delivered/failed/dead-letter; partial retry; no recipient; duplicate suppression; Serbian diacritics; error body without technical secret leakage.

Acceptance: UI never says delivered/executed/measured merely because a request was accepted, and all covered Serbian messages render correctly.

Dependencies: PILOT-BIZ-03 catalogue and PILOT-NET-03 state contract.

### PILOT-REACT-03 - Decision value parity in table, detail, report and export

Status: WAITING. Owner: RQ / Analytics reliability.

Problem: A user can receive conflicting decision meaning when list, detail, printable report, CSV/XLSX and action drawer format the same field differently.

Evidence: Safety rules name every affected surface, while legacy audits found count, denominator and fallback drift across analytics families.

Scope: Pilot-critical product, supplier, inventory and action values only.

Read first: `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`, `docs/ai/ANALYTICS_TEST_STRATEGY.md`, `Klijent/clientapp/src/utils/analyticsFormatters.ts`.

Do: Consume the manifest from PILOT-NET-04; use shared formatters/labels; make requested/effective period and truncation visible in every applicable export/report.

Tests: Ratio/percent; currency; null versus zero; no baseline; top-N; fallback; selected row versus export row; action payload relation.

Acceptance: One selected decision has the same approved value, units and trust explanation across its visible React and export surfaces.

Dependencies: PILOT-DATA-03 and PILOT-NET-04.

### PILOT-REACT-04 - Authenticated deployed pilot browser proof

Status: WAITING. Owner: STAB16 / Release.

Problem: Static bundle or unauthenticated API checks cannot prove that a real pilot operator sees the correct trust state and can complete a safe workflow.

Evidence: The 2026-08-27 audit could not initialize in-app browser verification; prior smoke evidence is historical for current deployment truth.

Scope: Authenticated browser smoke against the canonical deployed frontend/backend, with no production data mutation beyond a pre-approved safe test action if authorized.

Read first: `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`, `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`, `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`.

Do: Verify direct URL/refresh, trust header, error/empty/warning states, recommendation gating, report availability and one notification/action observation with screenshots plus request evidence.

Tests: The live scenario matrix and a local non-watch route spec for any UI defect fixed during the run.

Acceptance: Evidence captures real rendered content, relevant API state and exact deployment identifiers; shell-only HTTP 200 is rejected as proof.

Dependencies: Authenticated pilot account, browser capability, STAB deploy/worker proof and an approved safe workflow.

## Architecture prompts

### PILOT-ARCH-01 - End-to-end analytics evidence-chain architecture

Status: WAITING. Owner: DEX/OBS planning, with implementation split by source owner.

Problem: Current contracts name many trust properties but do not yet establish one queryable chain from source batch to decision, message and outcome.

Evidence: Import, refresh, cache, action and outbox components exist independently; the pilot gate needs correlated provenance.

Scope: Canonical identifiers, ownership and retention for source batch, mapping version, refresh run, snapshot/cache version, API correlation, decision ID, message ID and outcome evidence.

Read first: `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`, `docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md`, `docs/ai/ARCHITECTURE_BOUNDARIES.md`.

Do: Define the canonical evidence graph, propagation rules, redaction/retention, missing-link behavior and whether each edge is synchronous or eventually consistent.

Tests: Contract tests ensure a selected decision can be traced to its snapshot and source lineage; missing edge yields explicit unknown/blocked state.

Acceptance: Each pilot decision and notification can be explained with stable IDs rather than logs or informal inference.

Dependencies: QDB lineage, STAB worker proof, security review for identifiers/retention.

### PILOT-ARCH-02 - Automated release evidence gate

Status: WAITING. Owner: STAB/OBS.

Problem: Readiness is spread across historical documents and manual observations, allowing stale PASS statements to coexist with new blockers.

Evidence: `MASTER_ROADMAP.md` correctly treats historical snapshots as non-current, while release evidence is still manually assembled.

Scope: A read-only release-evidence manifest and gate that reports deploy SHA, CI, worker success, reconciliation, cache mode, smoke and unresolved warnings.

Read first: `MASTER_ROADMAP.md`, `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`, `docs/roadmaps/BUSINESS_ROADMAP.md`.

Do: Specify producers, staleness limits, immutable timestamped artifacts, required signatures/owners and fail-closed status aggregation; do not fabricate live checks in CI.

Tests: Missing or stale input; SHA mismatch; queued CI; worker unknown; reconciliation variance; browser smoke unavailable; accepted warning expiration.

Acceptance: One generated evidence record can say `Pilot Ready`, `Pilot Ready With Accepted Warnings` or `Not Ready` with every prerequisite linked and current.

Dependencies: STAB16, provider/CI read access and agreed freshness thresholds.

### PILOT-ARCH-03 - Data correctness SLO, alerting and incident ownership

Status: WAITING. Owner: OBS / Platform evolution.

Problem: Health checks detect availability, but pilot trust needs measurable objectives for data delay, reconciliation variance, decision suppression and notification failure.

Evidence: Monitoring/alerting documentation exists; current business roadmap requires worker/import/refresh operational monitoring.

Scope: Pilot SLO/SLI definitions, severity, alert route and incident ownership; no large dashboard redesign.

Read first: `docs/ops/ANALYTICS_MONITORING_ALERTING.md`, `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`, `docs/roadmaps/BUSINESS_ROADMAP.md`.

Do: Define freshness lag, failed-refresh rate, unresolved reconciliation variance, cache-version lag, suppressed-decision rate, outbox failure age and evidence-retention targets.

Tests: Synthetic failed worker; stale input; failed reconciliation; dead-letter growth; alert deduplication and escalation timeout.

Acceptance: Every P0 data-truth condition has an objective signal, owner, response time and a user-facing fail-closed consequence.

Dependencies: PILOT-ARCH-01 identifiers and approved on-call/owner model.

### PILOT-ARCH-04 - Customer/source boundary and data-quality incident model

Status: WAITING. Owner: QDB/MT/STAB planning split.

Problem: Support must distinguish product defects from retailer source defects, mapping changes and deployment incidents before the first customer can trust corrective action.

Evidence: Business roadmap requires source connection/mapping, dedicated deployment/isolation and support triage; MT remains gated for broader identity decisions.

Scope: Dedicated-pilot compatible incident taxonomy, ownership matrix and evidence requirements; no shared-SaaS tenant implementation.

Read first: `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`, `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`, `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`.

Do: Define source/mapping/refresh/cache/API/UI/notification incident classes, safe diagnostic fields, escalation target, customer communication rule and recovery validation.

Tests: Mapping regression; source duplicate; cache divergence; UI stale warning; unauthorized diagnostics request; recovery without false freshness.

Acceptance: An operator can classify an inaccurate-looking result, preserve evidence and communicate an honest status without exposing another customer or sensitive source data.

Dependencies: Dedicated-pilot deployment topology and MT owner decisions for any shared boundary.
