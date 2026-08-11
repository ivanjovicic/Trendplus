# Decision Graph Contract

Status: accepted contract for DEX01
Date: 2026-08-11
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
First mapping example: Product Decision Center

## Purpose

This document is the first durable Decision Intelligence contract for Trendplus. It defines how a recommendation can be represented as a deterministic graph of decision, evidence, confidence, constraint, alternative and action-link nodes without introducing runtime graph generation or AI-generated business truth.

The contract starts with the Product Decision Center because that family already exposes the richest row-level recommendation semantics and feeds the Executive Decision Board as a downstream consumer.

## Non-goals

- no AI/LLM provider dependency
- no runtime graph engine
- no endpoint or schema changes in this prompt
- no frontend-invented confidence, recommendation or impact logic

## Canonical shape

`decision -> evidence -> confidence contributors -> constraints -> alternatives -> action link -> outcome link`

This is a declarative model. The current code does not need to emit a literal graph object to satisfy the contract.

## Node types

| Node type | Contract meaning | Current source fields | Notes |
|---|---|---|---|
| Decision | The recommendation that will be shown and acted on | `RecommendationId`, `ProductId`, `SourceType`, `SourceKey`, `RecommendationType`, `RecommendationStatus`, `RecommendationLabel`, `RecommendedAction` | `RecommendationId` is the best current correlation key, not an authorization token |
| Evidence | Business facts supporting the decision | `Revenue`, `UnitsSold`, `VelocityUnitsPerDay`, `MarginContribution`, `MarginPct`, `MarginCoveragePct`, `CurrentStock`, `MinStock`, `StockGap`, `DaysSinceLastSale`, `TrendPct`, `LostSalesEstimate`, `SlowStockCapital`, `StockCoverDays`, `SellThroughRatio` | Evidence must preserve null/unknown values instead of collapsing into zero |
| Confidence contributor | Inputs that explain how trustworthy the decision is | `ConfidenceLevel`, `ConfidenceScore`, `ConfidencePct`, `ConfidenceBreakdown`, `ReliabilityPct`, `SignalConfidencePct`, `InputFreshnessStatus`, `DataQualityStatus` | Contributors can lower confidence; they must never fabricate certainty |
| Constraint | A reason the decision is not fully actionable | `RecommendationAllowed`, `WarningCodes`, `StockCoverStatus`, `SellThroughStatus`, `MarginQualityLabel` | Constraint state is not the same as confidence state |
| Alternative | A valid competing action or non-action | `AlternativeRecommendations` on `ProductDecisionCenterRowDto` | Alternatives must stay explicit and backend-led; do not infer them from `ReasonCodes` |
| Action link | The recommended next step | `RecommendedAction`, `RiskIfIgnored`, `ImpactWindowDays`, `ExpectedImpactRsd` | The action link should remain deterministic and reviewable |
| Outcome link | What happened after action | future RL/DT contract, plus `DecisionBoardCard.MeasuredImpactRsd` and `RealizationRatio` as downstream consumers | Not required for the first runtime implementation |

## Decision tree contract

The decision tree is a deterministic branch-path projection of the same recommendation graph. It is only present when the backend can prove a rule-based evaluation path. It is not inferred from reason codes, alternative recommendations or the evidence chain.

The current runtime emits a dedicated `decisionTree` object on `ProductDecisionWhyPanelDto`. The contract below defines the canonical shape and keeps the absence state explicit for older payloads.

### Tree state

| State | Meaning |
|---|---|
| `available` | The backend returned a deterministic branch path. |
| `unavailable` | The recommendation was not rule-based, or the backend cannot prove the branch trace yet. |
| `unknown` | The backend cannot confirm whether a tree exists. The UI must still render that absence explicitly. |

### Tree metadata

| Field | Meaning |
|---|---|
| `decisionTreeRuleSetId` | Stable rule family identifier. |
| `decisionTreeRuleSetVersion` | Rule version used for evaluation. |
| `decisionTreeEvaluatedAtUtc` | Timestamp of the backend evaluation. |
| `decisionTreeLabel` | Short label for the branch family. |
| `decisionTreeUnavailableReason` | Deterministic reason code for no tree. |
| `decisionTreeNodes` | Ordered branch nodes from root to selected leaf. |

### Node fields

Each branch node should include:

- `nodeId`
- `parentNodeId`
- `order`
- `ruleId`
- `ruleVersion`
- `nodeLabel`
- `predicateText`
- `predicateResult`
- `selectedBranchLabel`
- `evidenceSourceFields`
- `isFallback`
- `isTerminal`
- `explanationText`

### Branch-path rules

- show the branch path only when rule-based logic applies;
- if no branch path exists, render that absence explicitly;
- never infer a tree from `ReasonCodes`, `PrimaryDrivers`, `ConfidenceBreakdown`, `EvidenceChain` or `AlternativeRecommendations`;
- keep the tree backend-led and additive to the Why panel, not a replacement for it.

## Stable identity and correlation rules

- `RecommendationId` is the best current row-level identity for tracing a decision across surfaces.
- `SourceType` and `SourceKey` identify the domain source record, not access rights.
- `ProductId` and `Sku` are business identifiers, not tenant or user authority.
- `DecisionBoardCard.Id` is a presentation identifier for the aggregate view. It is not a replacement for the underlying recommendation identity.
- `GeneratedAtUtc` and `PeriodFromUtc`/`PeriodToUtc` are freshness and scope markers, not decision truth by themselves.
- No identifier in this contract authorizes access, edits or tenant selection.

## Product Decision Center field inventory

The current Product Decision Center row DTO exposes the first useful mapping surface for this contract.

### Identity and context

| Current field | Contract role |
|---|---|
| `ProductId` | decision identity anchor |
| `RecommendationId` | stable correlation key |
| `SourceType` | source family label |
| `SourceKey` | source record correlation key |
| `Sku` | product identity |
| `ProductName` | decision label context |
| `SupplierId` | upstream business context |
| `SupplierName` | upstream business context |
| `Category` | segmentation context |
| `TipObuce`, `Color`, `Size` | descriptive context |

### Decision and explanation

| Current field | Contract role |
|---|---|
| `RecommendationType` | decision family |
| `RecommendationStatus` | authoritative recommendation status |
| `RecommendationLabel` | user-facing label |
| `RecommendedAction` | next action |
| `RecommendationReason` | deterministic explanation text |
| `ReasonCodes` | stable machine-readable reasons |
| `PrimaryDrivers` | top supporting drivers |
| `RiskIfIgnored` | why the decision matters |
| `ExplainabilityText` | current narrative explanation |

### Evidence and trust

| Current field | Contract role |
|---|---|
| `Revenue` | revenue evidence |
| `UnitsSold` | volume evidence |
| `VelocityUnitsPerDay` | demand rate evidence |
| `MarginContribution` | margin evidence |
| `MarginPct` | margin rate evidence |
| `MarginQualityLabel` | margin trust qualifier |
| `MarginCoveragePct` | coverage signal |
| `CurrentStock` | on-hand stock evidence |
| `MinStock` | policy threshold evidence |
| `StockGap` | inventory gap evidence |
| `DaysSinceLastSale` | recency evidence |
| `TrendPct` | trend evidence |
| `LostSalesEstimate` | estimated opportunity evidence |
| `SlowStockCapital` | capital lock evidence |
| `StockCoverDays` | coverage evidence |
| `StockCoverStatus` | status contributor |
| `StockCoverStatusLabel` | status text |
| `SellThroughRatio` | sell-through evidence |
| `SellThroughStatus` | status contributor |
| `SellThroughStatusLabel` | status text |
| `InputFreshnessStatus` | freshness contributor |
| `DataQualityStatus` | data quality contributor |
| `WarningCodes` | warning contributors |

### Confidence and impact

| Current field | Contract role |
|---|---|
| `ConfidenceLevel` | coarse confidence band |
| `ConfidenceScore` | internal numeric score when available |
| `ConfidencePct` | user-facing confidence percent |
| `ConfidenceBreakdown` | ordered confidence-contributor nodes for the Why panel |
| `ReliabilityPct` | evidence reliability percent |
| `SignalConfidencePct` | upstream signal trust input |
| `RecommendationAllowed` | actionable constraint |
| `ExpectedImpactRsd` | expected impact in RSD |
| `ImpactWindowDays` | impact horizon |

### Downstream consumer inventory

The Executive Decision Board already composes from the Product Decision Center and adds its own aggregate fields:

- `DecisionBoardCard.ConfidenceLevel`
- `DecisionBoardCard.ConfidenceScore`
- `DecisionBoardCard.ReliabilityPct`
- `DecisionBoardCard.ExpectedImpactRsd`
- `DecisionBoardCard.MeasuredImpactRsd`
- `DecisionBoardCard.RealizationRatio`
- `DecisionBoardCard.RiskIfIgnored`
- `DecisionBoardCard.RecommendedNextAction`
- `DecisionBoardCard.WarningCodes`
- `DecisionBoardCard.DataQualityStatus`
- `DecisionBoardCard.PriorityScore`
- `DecisionBoardCard.ImpactScore`

These are consumer fields, not a second source of truth for the underlying recommendation.

## Confidence rules

- `confidenceLevel`, `confidenceScore`, `confidencePct` and `reliabilityPct` are related but not interchangeable.
- The backend decides which numeric representation is authoritative for each surface.
- Missing evidence must stay missing, not converted into a fake high confidence or a fake zero.
- `RecommendationAllowed=false` is a constraint signal, not a confidence score.
- `DataQualityStatus`, `InputFreshnessStatus`, `WarningCodes`, `StockCoverStatus` and `SellThroughStatus` are confidence contributors, not replacement recommendations.
- `ConfidenceBreakdown` is a renderable evidence list, but it still must stay backend-led and deterministic.
- Revenue, lost sales, stock value or other money fields are not confidence proxies.

## Alternative contract

The current Product Decision Center DTOs emit explicit alternative recommendations for the Product Decision Center row contract.

For the future contract:

- alternatives must be explicit objects, not inferred from reason codes;
- each alternative should include a stable identifier, label, action, eligibility, selection reason and rejection reason codes;
- alternatives should preserve their own confidence, data quality and impact context;
- "selected" and "not selected" need deterministic reason vocabulary.

## Why panel inputs

The Why panel should be renderable from deterministic backend fields only.

Minimum inputs:

- `RecommendationStatus`
- `RecommendationLabel`
- `RecommendationReason`
- `ReasonCodes`
- `PrimaryDrivers`
- `RiskIfIgnored`
- `RecommendedAction`
- `ExpectedImpactRsd`
- `ImpactWindowDays`
- `ConfidenceLevel`
- `ConfidenceScore`
- `ConfidencePct`
- `ConfidenceBreakdown`
- `ReliabilityPct`
- `DataQualityStatus`
- `InputFreshnessStatus`
- `RecommendationAllowed`
- `WarningCodes`

If one of these inputs is missing, the UI should show the absence explicitly instead of inventing a substitute explanation.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not require a new API shape for the first mapping example.
- Existing analytics reliability semantics remain authoritative.
- The first runtime implementation can stay scoped to the Product Decision Center family.
- Decision Board can reuse the same semantics without becoming a second source of truth.
- The current Product Decision Center fields can gate tree display, but the emitted `decisionTree` object is the authoritative branch-path payload; older payloads may omit it, so the UI must show absence explicitly.

## Next implementation split

1. Keep the Product Decision Center as the first mapping family.
2. Use the contract to define a deterministic Why-panel and evidence-chain model.
3. Extend the same vocabulary to other decision families only after the Product Decision Center alternatives are stable.
4. Extend the same vocabulary to other decision families only after the first family is stable.

## Completion note

- DEX01 completed on 2026-08-11.
- The contract remains deterministic and backend-led; no runtime graph generation was added.
- The first mapping example stays Product Decision Center, with Decision Board treated as the downstream consumer.
