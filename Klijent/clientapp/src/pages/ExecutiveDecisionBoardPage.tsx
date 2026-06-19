import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import { buildRowFromInsightItem } from "../components/inventory/inventoryUtils";
import type { InventoryRow } from "../components/inventory/types";
import { buildInventorySignalActionSpec } from "./InventoryPage";
import {
  getAnalyticsActionOutcomeSummary,
  getAnalyticsActions,
  getAnalyticsDataQualityHealth,
  getDecisionBoardAggregate,
  getAnalyticsRefreshStatus,
  getDashboardBootstrap,
  getInventoryInsights,
  getInventoryList,
  getPilotDataQualityIntakeReport,
  getProductDecisionCenter,
  getStores,
  getSupplierFilters,
} from "../services/analyticsApi";
import { getSupplierDecisionSummary } from "../services/supplierDecisionHubApi";
import type {
  AnalyticsActionItem,
  AnalyticsActionListResponse,
  AnalyticsActionOutcomeSummaryResponse,
  AnalyticsDashboardBootstrap,
  AnalyticsDataQualityHealth,
  DecisionBoardAggregateResponse,
  AnalyticsRefreshStatus,
  InventoryInsights,
  InventoryListItem,
  InventoryPagedResponse,
  PilotDataQualityIntakeReport,
  ProductDecisionCenterItem,
  ProductDecisionCenterResponse,
  StoreOption,
  SupplierFilterOption,
} from "../types/analytics";
import type {
  SummaryResponse,
  SummarySupplierItem,
} from "../services/supplierDecisionHubApi";
import { getRecommendationMeta } from "../components/supplierDecisionHub/utils";
import { fmtNumber, fmtPct, fmtPctFromRatio, fmtRsd, formatDateTime } from "../utils/analyticsFormatters";
import { getAnalyticsMetaMessage, isAnalyticsMetaError, isAnalyticsMetaInsufficient } from "../utils/analyticsResponseMeta";
import { normalizeRecommendationPct } from "../utils/canonicalRecommendationSemantics";
import "./ExecutiveDecisionBoardPage.css";

type BoardTone = "good" | "warning" | "critical" | "neutral" | "insufficient";
type BoardCardKind = "product" | "inventory" | "supplier" | "blocker" | "action" | "outcome";
type BoardSectionKey =
  | "urgent"
  | "impact"
  | "stockRisk"
  | "supplierRisk"
  | "blockers"
  | "actionsDecision"
  | "actionsOutcome";
type ActionState = "open" | "closed" | "none";

type BoardLoadError = {
  key: string;
  message: string;
  errorCode?: string | null;
  correlationId?: string | null;
};

type BoardCard = {
  id: string;
  sectionKey: BoardSectionKey;
  kind: BoardCardKind;
  sourceModule: string;
  sourceType?: string | null;
  sourceKey?: string | null;
  title: string;
  summary?: string | null;
  confidenceLabel: string;
  confidenceTone: BoardTone;
  confidenceScore?: number | null;
  expectedImpactRsd?: number | null;
  measuredImpactRsd?: number | null;
  realizationRatio?: number | null;
  riskIfIgnored: string;
  recommendedNextAction: string;
  actionCta: string;
  sourceLink: string;
  actionHref: string;
  alreadyInAction: boolean;
  alreadyClosed: boolean;
  actionStateLabel: string;
  warningCodes: string[];
  dataQualityStatus: string;
  generatedAtUtc?: string | null;
  priorityScore: number;
  impactScore: number;
};

type BoardSection = {
  key: BoardSectionKey;
  title: string;
  description: string;
  sourceLink: string;
  emptyMessage: string;
  cards: BoardCard[];
};

type BoardMetric = {
  label: string;
  value: string;
  tone: BoardTone;
  note?: string | null;
};

type BoardPayload = DecisionBoardAggregateResponse | null;

type BoardModel = {
  sections: BoardSection[];
  metrics: BoardMetric[];
  hasData: boolean;
  isPartial: boolean;
  overallDataQualityStatus: string | null;
  periodFrom: string | null;
  periodTo: string | null;
  lastRefreshAt: string | null;
  recommendationNote: string;
  emptyReason: string | null;
};

type LoadTask = {
  key: string;
  request: Promise<unknown>;
  assign: (value: unknown) => void;
  fallback: string;
};

const SECTION_TITLES: Record<BoardSectionKey, string> = {
  urgent: "Top 5 urgentnih odluka",
  impact: "Najveći očekivani uticaj",
  stockRisk: "Odluke o riziku zaliha",
  supplierRisk: "Odluke o riziku i prilici kod dobavljača",
  blockers: "Blokatori kvaliteta podataka",
  actionsDecision: "Akcije koje čekaju odluku",
  actionsOutcome: "Akcije koje čekaju ishod",
};

const SECTION_DESCRIPTIONS: Record<BoardSectionKey, string> = {
  urgent: "Najpre obrati pažnju na sigurnost, blokere i najveći signal koji je već spreman za odluku.",
  impact: "Gde je očekivani poslovni efekat najveći ako tim odmah reaguje.",
  stockRisk: "Dopuna, rasprodaja, spor obrt i prekomerna zaliha moraju biti vidljivi na jednom mestu.",
  supplierRisk: "Dobavljači sa jakim signalom, ali i oni sa rizikom, treba da budu poređani zajedno.",
  blockers: "Kada su podaci slabi, odluke moraju biti eksplicitno blokirane ili upozorene.",
  actionsDecision: "Otvorene akcije još čekaju odluku i ne treba da nestanu iz fokusa.",
  actionsOutcome: "Zatvorene akcije bez merenja su feedback gap, ne failure.",
};

const SECTION_EMPTY: Record<BoardSectionKey, string> = {
  urgent: "Trenutno nema dovoljno jakih odluka za ovu sekciju.",
  impact: "Nema kandidata sa procenjenim uticajem u ovom preseku.",
  stockRisk: "Nema jasnih signala za zalihe u ovom trenutku.",
  supplierRisk: "Nema dobavljača sa dovoljno jakim signalom za ovaj pogled.",
  blockers: "Nema aktivnih blokatora u izabranim izvorima.",
  actionsDecision: "Nema otvorenih akcija koje čekaju odluku.",
  actionsOutcome: "Nema akcija koje čekaju ishod.",
};

function buildLoadError(key: string, reason: unknown, fallback: string): BoardLoadError {
  if (reason && typeof reason === "object") {
    const maybeError = reason as {
      message?: string;
      errorCode?: string | null;
      correlationId?: string | null;
    };

    if (typeof maybeError.message === "string" && maybeError.message.trim()) {
      return {
        key,
        message: maybeError.message,
        errorCode: maybeError.errorCode ?? null,
        correlationId: maybeError.correlationId ?? null,
      };
    }
  }

  if (reason instanceof Error && reason.message.trim()) {
    return { key, message: reason.message };
  }

  if (typeof reason === "string" && reason.trim()) {
    return { key, message: reason };
  }

  return { key, message: fallback };
}

function openStatus(status?: string | null): boolean {
  return status === "new" || status === "accepted" || status === "deferred";
}

function confidenceToneFromValue(score: number | null | undefined): BoardTone {
  const normalized = normalizeRecommendationPct(score);
  if (normalized == null) return "insufficient";
  if (normalized >= 75) return "good";
  if (normalized >= 55) return "neutral";
  return "warning";
}

function confidenceLabelFromValue(score: number | null | undefined, fallback = "Nedovoljno podataka"): string {
  const normalized = normalizeRecommendationPct(score);
  if (normalized == null) return fallback;
  if (normalized >= 75) return `Visoka (${fmtPct(normalized, 0)})`;
  if (normalized >= 55) return `Srednja (${fmtPct(normalized, 0)})`;
  return `Niska (${fmtPct(normalized, 0)})`;
}

function capInsufficientDataPriority(
  priorityScore: number,
  confidenceTone: BoardTone,
  dataQualityStatus: string | null | undefined,
): number {
  if (confidenceTone === "insufficient" || (dataQualityStatus ?? "").trim().toLowerCase() === "insufficient_data") {
    return Math.min(priorityScore, 40);
  }

  return priorityScore;
}

function confidenceLabelFromProduct(item: ProductDecisionCenterItem): { label: string; tone: BoardTone; score: number | null } {
  if (item.confidenceLevel === "insufficient_data") {
    const score = normalizeRecommendationPct(item.confidenceScore ?? item.confidencePct);
    return {
      label: "Nedovoljno podataka",
      tone: "insufficient",
      score,
    };
  }

  const normalizedScore = normalizeRecommendationPct(item.confidenceScore ?? item.confidencePct);
  const level = (item.confidenceLevel ?? "").trim().toLowerCase();
  if (level === "high" || normalizedScore != null && normalizedScore >= 75) {
    return { label: confidenceLabelFromValue(normalizedScore, "Visoka"), tone: "good", score: normalizedScore };
  }
  if (level === "medium" || normalizedScore != null && normalizedScore >= 55) {
    return { label: confidenceLabelFromValue(normalizedScore, "Srednja"), tone: "neutral", score: normalizedScore };
  }
  if (level === "low" || normalizedScore != null) {
    return { label: confidenceLabelFromValue(normalizedScore, "Niska"), tone: "warning", score: normalizedScore };
  }
  return { label: "Nedovoljno podataka", tone: "insufficient", score: normalizedScore };
}

function openActionStateLabel(state: ActionState): string {
  if (state === "open") return "U akcijama";
  if (state === "closed") return "Već zatvoreno";
  return "Dodaj u akcije";
}

function recommendationWarningCodes(item: ProductDecisionCenterItem): string[] {
  const codes = [...(item.warningCodes ?? []), ...(item.reasonCodes ?? [])];
  return Array.from(new Set(codes.filter((code) => code && code.trim().length > 0)));
}

function actionStateIndex(actions: AnalyticsActionItem[]): Map<string, ActionState> {
  const stateByKey = new Map<string, ActionState>();

  for (const action of actions) {
    const key = `${action.sourceType}:${action.sourceKey}`;
    if (openStatus(action.status)) {
      stateByKey.set(key, "open");
      continue;
    }

    if (!stateByKey.has(key)) {
      stateByKey.set(key, "closed");
    }
  }

  return stateByKey;
}

function resolveActionState(sourceType: string | null | undefined, sourceKey: string | null | undefined, states: Map<string, ActionState>): ActionState {
  if (!sourceType || !sourceKey) return "none";
  return states.get(`${sourceType}:${sourceKey}`) ?? "none";
}

function sourceActionLink(sourceType: string | null | undefined): string {
  if (sourceType === "supplier") return "/analytics/actions?sourceType=supplier";
  if (sourceType === "inventory") return "/analytics/actions?sourceType=inventory";
  if (sourceType === "product") return "/analytics/actions?sourceType=product";
  if (sourceType === "data_quality") return "/analytics/actions?sourceType=data_quality";
  return "/analytics/actions";
}

function sourceScreenLink(kind: BoardCardKind, extra?: string | null): string {
  if (kind === "product") return "/analytics/products";
  if (kind === "inventory") return "/analytics/inventory";
  if (kind === "supplier") return "/analytics/supplier?tab=overview";
  if (kind === "blocker") return extra?.trim() === "pilot_readiness" ? "/analytics/pilot-readiness" : "/analytics/data-quality";
  return "/analytics/actions";
}

function buildProductCards(product: ProductDecisionCenterResponse | null, states: Map<string, ActionState>): BoardCard[] {
  if (!product) return [];

  return [...product.rows]
    .sort((a, b) => {
      const aImpact = a.expectedImpactRsd ?? a.lostSalesEstimate ?? 0;
      const bImpact = b.expectedImpactRsd ?? b.lostSalesEstimate ?? 0;
      const aConfidence = normalizeRecommendationPct(a.confidenceScore ?? a.confidencePct) ?? 0;
      const bConfidence = normalizeRecommendationPct(b.confidenceScore ?? b.confidencePct) ?? 0;
      return (bImpact + bConfidence) - (aImpact + aConfidence);
    })
    .slice(0, 12)
    .map((row, index) => {
      const confidence = confidenceLabelFromProduct(row);
      const actionState = resolveActionState(row.sourceType ?? "product", row.sourceKey ?? `product:${row.productId}`, states);
      const expectedImpact = row.expectedImpactRsd ?? row.lostSalesEstimate ?? null;
      const warnings = recommendationWarningCodes(row);

      return {
        id: `product:${row.productId}:${index}`,
        sectionKey: "urgent" as const,
        kind: "product" as const,
        sourceModule: "Odluke o proizvodima",
        sourceType: row.sourceType ?? "product",
        sourceKey: row.sourceKey ?? `product:${row.productId}`,
        title: row.productName,
        summary: row.explainabilityText ?? row.recommendationReason,
        confidenceLabel: confidence.label,
        confidenceTone: confidence.tone,
        confidenceScore: confidence.score,
        expectedImpactRsd: expectedImpact,
        measuredImpactRsd: null,
        realizationRatio: null,
        riskIfIgnored: row.riskIfIgnored ?? row.recommendationReason,
        recommendedNextAction: row.recommendedAction,
        actionCta: openActionStateLabel(actionState === "none" ? "none" : actionState),
        sourceLink: sourceScreenLink("product"),
        actionHref: sourceActionLink("product"),
        alreadyInAction: actionState === "open",
        alreadyClosed: actionState === "closed",
        actionStateLabel: openActionStateLabel(actionState),
        warningCodes: warnings,
        dataQualityStatus: row.dataQualityStatus,
        generatedAtUtc: product.generatedAtUtc,
        priorityScore: capInsufficientDataPriority(
          computePriorityScore(expectedImpact, confidence.score, row.dataQualityStatus, row.recommendationStatus),
          confidence.tone,
          row.dataQualityStatus,
        ),
        impactScore: expectedImpact ?? 0,
      };
    });
}

function computePriorityScore(
  expectedImpact: number | null | undefined,
  confidenceScore: number | null | undefined,
  dataQualityStatus: string | null | undefined,
  recommendationStatus?: string | null,
): number {
  const impactComponent = Math.min(Math.max(expectedImpact ?? 0, 0), 500_000) / 5_000;
  const confidenceComponent = Math.min(Math.max(normalizeRecommendationPct(confidenceScore) ?? 0, 0), 100);
  const dataQualityPenalty =
    dataQualityStatus === "critical"
      ? 35
      : dataQualityStatus === "warning"
        ? 15
        : dataQualityStatus === "insufficient_data"
          ? 25
          : 0;
  const statusBonus =
    recommendationStatus === "REPLENISH" || recommendationStatus === "EXPAND"
      ? 20
      : recommendationStatus === "BOOST"
        ? 18
        : recommendationStatus === "MARKDOWN"
          ? 14
          : recommendationStatus === "FIX_DATA"
            ? 22
            : recommendationStatus === "INSUFFICIENT_DATA"
              ? -15
              : 0;

  return impactComponent + confidenceComponent + statusBonus - dataQualityPenalty;
}

function buildInventoryCards(
  inventoryInsights: InventoryInsights | null,
  inventoryRows: InventoryRow[],
  states: Map<string, ActionState>,
): BoardCard[] {
  if (!inventoryInsights) return [];

  const rowsById = new Map<number, InventoryRow>();
  for (const row of inventoryRows) {
    rowsById.set(row.id, row);
  }

  const candidates = [
    ...(inventoryInsights.topAgedItems ?? []),
    ...(inventoryInsights.topCapitalLockedItems ?? []),
  ];

  const deduped = new Map<number, BoardCard>();

  for (const candidate of candidates) {
    const row = rowsById.get(candidate.id);
    if (!row) continue;

    const actionSpec = buildInventorySignalActionSpec(row);
    const actionState = resolveActionState("inventory", actionSpec.sourceKey, states);
    const confidence = confidenceLabelFromValue(row.signalConfidencePct, row.recommendationAllowed === false ? "Nedovoljno podataka" : "Nedovoljno podataka");
    const impact = actionSpec.expectedImpactRsd ?? row.estimatedValueAmount ?? row.estimatedValue ?? null;
    const riskIfIgnored = row.signalText || row.stockCoverStatusLabel || "Signal nije dovoljno opisan.";

    deduped.set(candidate.id, {
      id: `inventory:${candidate.id}:${actionSpec.sourceKey}`,
      sectionKey: "stockRisk",
      kind: "inventory",
      sourceModule: "Zalihe",
      sourceType: "inventory",
      sourceKey: actionSpec.sourceKey,
      title: row.naziv,
      summary: `${row.stockCoverStatusLabel}. ${row.sellThroughStatusLabel}.`,
      confidenceLabel: confidence,
      confidenceTone: confidenceToneFromValue(row.signalConfidencePct),
      confidenceScore: row.signalConfidencePct,
      expectedImpactRsd: impact,
      measuredImpactRsd: null,
      realizationRatio: null,
      riskIfIgnored,
      recommendedNextAction: actionSpec.title,
      actionCta: openActionStateLabel(actionState),
      sourceLink: sourceScreenLink("inventory"),
      actionHref: sourceActionLink("inventory"),
      alreadyInAction: actionState === "open",
      alreadyClosed: actionState === "closed",
      actionStateLabel: openActionStateLabel(actionState),
      warningCodes: row.reasonCodes ?? [],
      dataQualityStatus: row.dataQualityStatus,
      generatedAtUtc: inventoryInsights.meta?.generatedAtUtc ?? null,
      priorityScore: capInsufficientDataPriority(
        computePriorityScore(impact, row.signalConfidencePct, row.dataQualityStatus, actionSpec.recommendationStatus),
        confidenceToneFromValue(row.signalConfidencePct),
        row.dataQualityStatus,
      ),
      impactScore: impact ?? 0,
    });
  }

  return Array.from(deduped.values()).sort((a, b) => b.priorityScore - a.priorityScore);
}

function buildSupplierActionSourceKey(
  item: SummarySupplierItem,
  filters: { fromDate: string; toDate: string; storeId?: number | null; dataScope?: string | null },
  recommendationAllowed: boolean,
): string {
  const actionKind = recommendationAllowed ? "negotiation" : "signal_check";
  return `supplier:${actionKind}:${item.supplierId}:${filters.fromDate}:${filters.toDate}:${filters.storeId ?? "all"}:${filters.dataScope ?? "all"}`;
}

function buildSupplierCards(summary: SummaryResponse | null, states: Map<string, ActionState>): BoardCard[] {
  if (!summary) return [];

  const trustMetadata = summary.trustMetadata ?? null;
  const recommendationAllowed = trustMetadata?.recommendationAllowed ?? true;
  const filters = {
    fromDate: summary.from.slice(0, 10),
    toDate: summary.to.slice(0, 10),
    storeId: null,
    dataScope: trustMetadata?.dataScope ?? "all",
  };

  const cards: BoardCard[] = [];
  const groups: Array<{ sourceModule: string; items: SummarySupplierItem[]; sectionKey: BoardSectionKey; actionLink: string; cardTitle: string }> = [
    { sourceModule: "Dobavljači", items: summary.topGrowSuppliers ?? [], sectionKey: "supplierRisk", actionLink: "/analytics/supplier?tab=overview", cardTitle: "Dobavljači za širenje saradnje" },
    { sourceModule: "Dobavljači", items: summary.topRiskSuppliers ?? [], sectionKey: "supplierRisk", actionLink: "/analytics/supplier?tab=overview", cardTitle: "Dobavljači sa rizikom" },
  ];

  for (const group of groups) {
    group.items.forEach((item, index) => {
      const recommendation = getRecommendationMeta(item.recommendationCode);
      const actionKey = buildSupplierActionSourceKey(item, filters, recommendationAllowed);
      const actionState = resolveActionState("supplier", actionKey, states);
      const confidenceScore = normalizeRecommendationPct(item.confidenceScore);
      const confidence = confidenceLabelFromValue(confidenceScore);
      const confidenceTone = confidenceToneFromValue(confidenceScore);
      const impact = item.revenue > 0 ? item.revenue : null;

      cards.push({
        id: `supplier:${group.cardTitle}:${item.supplierId}:${index}`,
        sectionKey: group.sectionKey,
        kind: "supplier",
        sourceModule: group.sourceModule,
        sourceType: "supplier",
        sourceKey: actionKey,
        title: item.supplierName,
        summary: `${recommendation.label}. ${recommendation.razlog}`,
        confidenceLabel: confidence,
        confidenceTone: confidenceToneFromValue(confidenceScore),
        confidenceScore,
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        riskIfIgnored: recommendation.razlog,
        recommendedNextAction: recommendation.label,
        actionCta: openActionStateLabel(actionState),
        sourceLink: group.actionLink,
        actionHref: sourceActionLink("supplier"),
        alreadyInAction: actionState === "open",
        alreadyClosed: actionState === "closed",
        actionStateLabel: openActionStateLabel(actionState),
        warningCodes: trustMetadata?.dataCoverageStatus && trustMetadata.dataCoverageStatus !== "good"
          ? [String(trustMetadata.dataCoverageStatus)]
          : [],
        dataQualityStatus: trustMetadata?.dataCoverageStatus ?? "unknown",
        generatedAtUtc: summary.to,
        priorityScore: capInsufficientDataPriority(
          computePriorityScore(impact, confidenceScore, trustMetadata?.dataCoverageStatus ?? "unknown", item.recommendationCode),
          confidenceTone,
          trustMetadata?.dataCoverageStatus,
        ),
        impactScore: impact ?? 0,
      });
    });
  }

  return cards.sort((a, b) => b.priorityScore - a.priorityScore);
}

function buildActionCards(actions: AnalyticsActionListResponse | null): BoardCard[] {
  if (!actions) return [];

  const openActions = actions.items.filter((item) => openStatus(item.status));

  return openActions
    .slice()
    .sort((a, b) => {
      const priorityScore = (value: string) => (value === "P1" ? 3 : value === "P2" ? 2 : 1);
      const aDue = a.dueAtUtc ? new Date(a.dueAtUtc).getTime() : Number.POSITIVE_INFINITY;
      const bDue = b.dueAtUtc ? new Date(b.dueAtUtc).getTime() : Number.POSITIVE_INFINITY;
      return priorityScore(b.priority) - priorityScore(a.priority) || aDue - bDue;
    })
    .slice(0, 12)
    .map((item, index) => {
      const confidence = confidenceLabelFromValue(item.confidencePct);
      const confidenceTone = confidenceToneFromValue(item.confidencePct);
      const state = item.status === "new" || item.status === "accepted" || item.status === "deferred" ? "open" : "none";
      const nextStep =
        item.status === "new"
          ? "Prihvati ili odbij preporuku."
          : item.status === "accepted"
            ? "Prati sprovođenje i zabeleži ishod."
            : "Ponovo proceni prioritet i rok.";

      return {
        id: `action:${item.id}:${index}`,
        sectionKey: "actionsDecision",
        kind: "action",
        sourceModule: "Centralne akcije",
        sourceType: item.sourceType,
        sourceKey: item.sourceKey,
        title: item.title,
        summary: item.description ?? item.recommendationStatus ?? "Otvorena akcija još čeka odluku.",
        confidenceLabel: confidence,
        confidenceTone,
        confidenceScore: item.confidencePct ?? null,
        expectedImpactRsd: item.expectedImpactRsd ?? item.impactEstimateRsd ?? null,
        measuredImpactRsd: item.measuredImpactRsd ?? null,
        realizationRatio: null,
        riskIfIgnored: item.description ?? "Akcija još nije zatvorena.",
        recommendedNextAction: nextStep,
        actionCta: "Otvori akcije",
        sourceLink: "/analytics/actions",
        actionHref: "/analytics/actions",
        alreadyInAction: true,
        alreadyClosed: false,
        actionStateLabel: "U akcijama",
        warningCodes: item.dataQualityStatus ? [String(item.dataQualityStatus)] : [],
        dataQualityStatus: item.dataQualityStatus ?? "unknown",
        generatedAtUtc: item.updatedAtUtc,
        priorityScore: capInsufficientDataPriority(
          computePriorityScore(item.expectedImpactRsd ?? item.impactEstimateRsd, item.confidencePct, item.dataQualityStatus, item.recommendationStatus),
          confidenceTone,
          item.dataQualityStatus,
        ),
        impactScore: item.expectedImpactRsd ?? item.impactEstimateRsd ?? 0,
      } satisfies BoardCard;
    });
}

function buildOutcomeCards(
  outcomeSummary: AnalyticsActionOutcomeSummaryResponse | null,
  actions: AnalyticsActionListResponse | null,
): BoardCard[] {
  if (!outcomeSummary && !actions) return [];

  const cards: BoardCard[] = [];

  if (outcomeSummary) {
    const warningCodes = outcomeSummary.meta.warnings ?? [];
    const confidenceTone = outcomeSummary.meta.measuredSampleSize < 10 || warningCodes.length > 0 ? "warning" : "good";
    const confidenceLabel = outcomeSummary.meta.measuredSampleSize < 10
      ? "Nedovoljno podataka"
      : warningCodes.length > 0
        ? "Pomoćni signal"
        : "Stabilan feedback";

    cards.push({
      id: "outcome-summary",
      sectionKey: "actionsOutcome",
      kind: "outcome",
      sourceModule: "Sažetak ishoda",
      title: "Realizacija očekivanog uticaja",
      summary: `Izmereno: ${fmtRsd(outcomeSummary.impact.measuredImpactRsd)} · Očekivano: ${fmtRsd(outcomeSummary.impact.expectedImpactRsd)} · Coverage: ${fmtPctFromRatio(outcomeSummary.totals.outcomeCoverageRate, 1)}.`,
      confidenceLabel,
      confidenceTone,
      confidenceScore: outcomeSummary.impact.measuredImpactSampleCount,
      expectedImpactRsd: outcomeSummary.impact.expectedImpactRsd ?? null,
      measuredImpactRsd: outcomeSummary.impact.measuredImpactRsd ?? null,
      realizationRatio: outcomeSummary.impact.realizationRatio ?? null,
      riskIfIgnored: warningCodes.length > 0
        ? "Uzorak ishoda je još mali ili nepotpun."
        : "Feedback loop je otvoren i treba ga pratiti.",
      recommendedNextAction: "Uporedi očekivani i izmereni uticaj pre daljeg širenja preporuka.",
      actionCta: "Otvori akcije",
      sourceLink: "/analytics/actions",
      actionHref: "/analytics/actions",
      alreadyInAction: false,
      alreadyClosed: false,
      actionStateLabel: "Sažetak",
      warningCodes,
      dataQualityStatus: outcomeSummary.meta.measuredSampleSize < 10 ? "insufficient_data" : (warningCodes.length > 0 ? "warning" : "good"),
      generatedAtUtc: outcomeSummary.meta.generatedAtUtc,
      priorityScore: capInsufficientDataPriority(
        (outcomeSummary.impact.expectedImpactRsd ?? 0) / 5_000 + (warningCodes.length > 0 ? 30 : 0),
        confidenceTone,
        outcomeSummary.meta.measuredSampleSize < 10 ? "insufficient_data" : (warningCodes.length > 0 ? "warning" : "good"),
      ),
      impactScore: outcomeSummary.impact.expectedImpactRsd ?? 0,
    });
  }

  const pendingOutcomeActions = (actions?.items ?? []).filter((item) => openStatus(item.outcomeStatus) || item.outcomeStatus === "pending" || item.outcomeStatus === "not_measured");
  for (const item of pendingOutcomeActions.slice(0, 10)) {
    const confidence = confidenceLabelFromValue(item.confidencePct);
    const confidenceTone = confidenceToneFromValue(item.confidencePct);
    cards.push({
      id: `outcome:${item.id}`,
      sectionKey: "actionsOutcome",
      kind: "outcome",
      sourceModule: "Ishodi akcija",
      sourceType: item.sourceType,
      sourceKey: item.sourceKey,
      title: item.title,
      summary: item.outcomeNotes ?? item.description ?? "Ishod još nije izmeren.",
      confidenceLabel: confidence,
      confidenceTone,
      confidenceScore: item.confidencePct ?? null,
      expectedImpactRsd: item.expectedImpactRsd ?? item.impactEstimateRsd ?? null,
      measuredImpactRsd: item.measuredImpactRsd ?? null,
      realizationRatio: null,
      riskIfIgnored: item.outcomeNotes ?? "Ovaj ishod još ne može da se koristi za učenje.",
      recommendedNextAction: "Zabeleži ili validiraj ishod.",
      actionCta: "Otvori akcije",
      sourceLink: "/analytics/actions",
      actionHref: "/analytics/actions",
      alreadyInAction: false,
      alreadyClosed: true,
      actionStateLabel: "Čeka ishod",
      warningCodes: item.outcomeStatus ? [item.outcomeStatus] : [],
      dataQualityStatus: item.dataQualityStatus ?? "unknown",
      generatedAtUtc: item.updatedAtUtc,
      priorityScore: capInsufficientDataPriority(
        computePriorityScore(item.expectedImpactRsd ?? item.impactEstimateRsd, item.confidencePct, item.dataQualityStatus, item.recommendationStatus) - 20,
        confidenceTone,
        item.dataQualityStatus,
      ),
      impactScore: item.expectedImpactRsd ?? item.impactEstimateRsd ?? 0,
    });
  }

  return cards.sort((a, b) => b.priorityScore - a.priorityScore);
}

function buildBlockerCards(
  dashboard: AnalyticsDashboardBootstrap | null,
  health: AnalyticsDataQualityHealth | null,
  intake: PilotDataQualityIntakeReport | null,
  refreshStatus: AnalyticsRefreshStatus | null,
): BoardCard[] {
  const cards: BoardCard[] = [];

  if (refreshStatus?.dataFreshnessStatus === "stale" || refreshStatus?.dataFreshnessStatus === "critical") {
    cards.push({
      id: "blocker-refresh",
      sectionKey: "blockers",
      kind: "blocker",
      sourceModule: "Pilot spremnost",
      title: "Osvežavanje je zastarelo",
      summary: refreshStatus.lastErrorMessage ?? "Poslednje osvežavanje je zastarelo ili kritično.",
      confidenceLabel: refreshStatus.dataFreshnessStatus === "critical" ? "Kritično" : "Upozorenje",
      confidenceTone: refreshStatus.dataFreshnessStatus === "critical" ? "critical" : "warning",
      confidenceScore: null,
      expectedImpactRsd: null,
      measuredImpactRsd: null,
      realizationRatio: null,
      riskIfIgnored: "Board ne treba da izgleda sveže dok worker ne vrati poslednji uspešan refresh.",
      recommendedNextAction: "Proveri worker panel i pokreni osvežavanje ako je bezbedno.",
      actionCta: "Otvori worker panel",
      sourceLink: "/analytics/pilot-readiness",
      actionHref: "/admin/configuration?panel=workers",
      alreadyInAction: false,
      alreadyClosed: false,
      actionStateLabel: "Blokator",
      warningCodes: [String(refreshStatus.dataFreshnessStatus)],
      dataQualityStatus: String(refreshStatus.dataFreshnessStatus),
      generatedAtUtc: refreshStatus.generatedAtUtc,
      priorityScore: 300,
      impactScore: 0,
    });
  }

  if (intake) {
    if (intake.issues.missingCostCount > 0 || intake.impact.revenueWithoutCostPercent > 0) {
      cards.push({
        id: "blocker-cost",
        sectionKey: "blockers",
        kind: "blocker",
        sourceModule: "Pilot spremnost",
        title: "Dopuni nabavnu cenu",
        summary: `Redovi bez nabavne cene: ${fmtNumber(intake.issues.missingCostCount, 0)} · Prihod bez cene: ${fmtPctFromRatio(intake.impact.revenueWithoutCostPercent, 1)}.`,
        confidenceLabel: "Upozorenje",
        confidenceTone: "warning",
        confidenceScore: null,
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        riskIfIgnored: "Marža i očekivani uticaj ostaju slabiji dok nedostaje nabavna cena.",
        recommendedNextAction: "Otvori kvalitet podataka i proveri mapiranje troškova.",
        actionCta: "Kvalitet podataka",
        sourceLink: "/analytics/data-quality",
        actionHref: "/analytics/data-quality",
        alreadyInAction: false,
        alreadyClosed: false,
        actionStateLabel: "Blokator",
        warningCodes: ["missing_cost"],
        dataQualityStatus: "warning",
        generatedAtUtc: intake.generatedAtUtc,
        priorityScore: 220,
        impactScore: 0,
      });
    }

    if (intake.issues.missingSupplierCount > 0 || intake.impact.articlesWithoutSupplierPercent > 0) {
      cards.push({
        id: "blocker-supplier",
        sectionKey: "blockers",
        kind: "blocker",
        sourceModule: "Pilot spremnost",
        title: "Poveži dobavljače",
        summary: `Artikli bez dobavljača: ${fmtNumber(intake.issues.missingSupplierCount, 0)} · Udeo: ${fmtPctFromRatio(intake.impact.articlesWithoutSupplierPercent, 1)}.`,
        confidenceLabel: "Upozorenje",
        confidenceTone: "warning",
        confidenceScore: null,
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        riskIfIgnored: "Supplier signal i board prioritizacija ostaju slabiji bez dobavljačkog mapiranja.",
        recommendedNextAction: "Otvori kvalitet podataka i popravi mapping dobavljača.",
        actionCta: "Kvalitet podataka",
        sourceLink: "/analytics/data-quality",
        actionHref: "/analytics/data-quality",
        alreadyInAction: false,
        alreadyClosed: false,
        actionStateLabel: "Blokator",
        warningCodes: ["missing_supplier"],
        dataQualityStatus: "warning",
        generatedAtUtc: intake.generatedAtUtc,
        priorityScore: 210,
        impactScore: 0,
      });
    }

    if (intake.impact.insufficientSignalCount > 0 || intake.readinessStatus === "critical") {
      cards.push({
        id: "blocker-signal",
        sectionKey: "blockers",
        kind: "blocker",
        sourceModule: "Pilot spremnost",
        title: "Pojačaj signalnu pokrivenost",
        summary: `Nedovoljni signali: ${fmtNumber(intake.impact.insufficientSignalCount, 0)} · Readiness: ${intake.readinessLabel}.`,
        confidenceLabel: intake.readinessStatus === "critical" ? "Kritično" : "Upozorenje",
        confidenceTone: intake.readinessStatus === "critical" ? "critical" : "warning",
        confidenceScore: null,
        expectedImpactRsd: null,
        measuredImpactRsd: null,
        realizationRatio: null,
        riskIfIgnored: "Preporuke neće biti dovoljno pouzdane dok signalna pokrivenost ostane slaba.",
        recommendedNextAction: "Proveri pilot readiness i signalne izvore.",
        actionCta: "Pilot spremnost",
        sourceLink: "/analytics/pilot-readiness",
        actionHref: "/analytics/pilot-readiness",
        alreadyInAction: false,
        alreadyClosed: false,
        actionStateLabel: "Blokator",
        warningCodes: ["insufficient_signal"],
        dataQualityStatus: "insufficient_data",
        generatedAtUtc: intake.generatedAtUtc,
        priorityScore: capInsufficientDataPriority(
          intake.readinessStatus === "critical" ? 260 : 180,
          intake.readinessStatus === "critical" ? "critical" : "warning",
          "insufficient_data",
        ),
        impactScore: 0,
      });
    }
  }

  if (health && health.scoreStatus !== "excellent" && health.scoreStatus !== "good") {
    cards.push({
      id: "blocker-health",
      sectionKey: "blockers",
      kind: "blocker",
      sourceModule: "Kvalitet podataka",
      title: "Data quality health traži proveru",
      summary: health.scoreSummary,
      confidenceLabel: health.scoreStatus === "critical" ? "Kritično" : "Upozorenje",
      confidenceTone: health.scoreStatus === "critical" ? "critical" : "warning",
      confidenceScore: health.score,
      expectedImpactRsd: health.missingCostRevenue > 0 ? health.missingCostRevenue : null,
      measuredImpactRsd: null,
      realizationRatio: null,
      riskIfIgnored: "Slab data quality direktno spušta pouzdanost preporuka u board-u.",
      recommendedNextAction: "Otvori kvalitet podataka i reši najskuplje blokere.",
      actionCta: "Kvalitet podataka",
      sourceLink: "/analytics/data-quality",
      actionHref: "/analytics/data-quality",
      alreadyInAction: false,
      alreadyClosed: false,
      actionStateLabel: "Blokator",
      warningCodes: [
        ...(health.missingCostRevenueSharePct != null ? ["missing_cost"] : []),
        ...(health.unknownSupplierRevenueSharePct != null ? ["missing_supplier"] : []),
      ],
      dataQualityStatus: health.scoreStatus,
      generatedAtUtc: health.generatedAt,
      priorityScore: health.scoreStatus === "critical" ? 280 : 190,
      impactScore: health.missingCostRevenue,
    });
  }

  if (dashboard?.validationFreshness?.status && dashboard.validationFreshness.status !== "good") {
    cards.push({
      id: "blocker-dashboard-freshness",
      sectionKey: "blockers",
      kind: "blocker",
      sourceModule: "Dashboard",
      title: "Dashboard freshness nije dobar",
      summary: dashboard.validationFreshness.message,
      confidenceLabel: String(dashboard.validationFreshness.status).toLowerCase().includes("critical") ? "Kritično" : "Upozorenje",
      confidenceTone: String(dashboard.validationFreshness.status).toLowerCase().includes("critical") ? "critical" : "warning",
      confidenceScore: dashboard.validationFreshness.score ?? null,
      expectedImpactRsd: dashboard.validationLostSales?.lostSalesEstimate ?? null,
      measuredImpactRsd: null,
      realizationRatio: null,
      riskIfIgnored: "Izvršni board ne treba da izgleda sveže dok validation freshness nije potvrđen.",
      recommendedNextAction: "Proveri validation freshness i refresh status.",
      actionCta: "Status osvežavanja",
      sourceLink: "/analytics",
      actionHref: "/admin/configuration?panel=workers",
      alreadyInAction: false,
      alreadyClosed: false,
      actionStateLabel: "Blokator",
      warningCodes: ["freshness"],
      dataQualityStatus: String(dashboard.validationFreshness.status),
      generatedAtUtc: dashboard.meta?.generatedAtUtc ?? null,
      priorityScore: 240,
      impactScore: dashboard.validationFreshness.score ?? 0,
    });
  }

  return cards.sort((a, b) => b.priorityScore - a.priorityScore);
}

function boardActionState(card: { alreadyInAction: boolean; alreadyClosed: boolean }): ActionState {
  if (card.alreadyInAction) return "open";
  if (card.alreadyClosed) return "closed";
  return "none";
}

function confidencePresentationFromBoardCard(
  card: DecisionBoardAggregateResponse["sections"][number]["cards"][number],
): { label: string; tone: BoardTone; score: number | null } {
  const normalizedScore = normalizeRecommendationPct(card.confidenceScore);
  const level = (card.confidenceLevel ?? "").trim().toLowerCase();

  if (level === "insufficient_data" || card.dataQualityStatus === "insufficient_data") {
    return { label: confidenceLabelFromValue(normalizedScore, "Nedovoljno podataka"), tone: "insufficient", score: normalizedScore };
  }

  if (level === "high" || (normalizedScore != null && normalizedScore >= 75)) {
    return { label: confidenceLabelFromValue(normalizedScore, "Visoka"), tone: "good", score: normalizedScore };
  }

  if (level === "medium" || (normalizedScore != null && normalizedScore >= 55)) {
    return { label: confidenceLabelFromValue(normalizedScore, "Srednja"), tone: "neutral", score: normalizedScore };
  }

  if (level === "low" || normalizedScore != null) {
    return { label: confidenceLabelFromValue(normalizedScore, "Niska"), tone: "warning", score: normalizedScore };
  }

  return { label: "Nedovoljno podataka", tone: "insufficient", score: normalizedScore };
}

function normalizeBoardTone(tone: string | null | undefined): BoardTone {
  const normalized = tone?.trim().toLowerCase();
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "neutral" || normalized === "insufficient") {
    return normalized;
  }

  return "neutral";
}

function mapDecisionBoardCard(
  section: DecisionBoardAggregateResponse["sections"][number],
  card: DecisionBoardAggregateResponse["sections"][number]["cards"][number],
): BoardCard {
  const confidence = confidencePresentationFromBoardCard(card);
  const actionState = boardActionState(card);

  return {
    id: card.id,
    sectionKey: section.key as BoardSectionKey,
    kind: card.kind as BoardCardKind,
    sourceModule: card.sourceModule,
    sourceType: card.sourceType ?? null,
    sourceKey: card.sourceKey ?? null,
    title: card.title,
    summary: card.summary ?? null,
    confidenceLabel: confidence.label,
    confidenceTone: confidence.tone,
    confidenceScore: confidence.score,
    expectedImpactRsd: card.expectedImpactRsd ?? null,
    measuredImpactRsd: card.measuredImpactRsd ?? null,
    realizationRatio: card.realizationRatio ?? null,
    riskIfIgnored: card.riskIfIgnored,
    recommendedNextAction: card.recommendedNextAction,
    actionCta: openActionStateLabel(actionState),
    sourceLink: section.sourceLink,
    actionHref: card.actionHref,
    alreadyInAction: card.alreadyInAction,
    alreadyClosed: card.alreadyClosed,
    actionStateLabel: openActionStateLabel(actionState),
    warningCodes: card.warningCodes ?? [],
    dataQualityStatus: card.dataQualityStatus,
    generatedAtUtc: card.generatedAtUtc ?? null,
    priorityScore: card.priorityScore,
    impactScore: card.impactScore,
  };
}

function buildBoardModel(payload: DecisionBoardAggregateResponse): BoardModel {
  const sections: BoardSection[] = (payload.sections ?? []).map((section) => ({
    key: section.key as BoardSectionKey,
    title: section.title,
    description: section.description,
    sourceLink: section.sourceLink,
    emptyMessage: section.emptyMessage,
    cards: (section.cards ?? []).map((card) => mapDecisionBoardCard(section, card)),
  }));

  const metrics: BoardMetric[] = (payload.metrics ?? []).map((metric) => ({
    label: metric.label,
    value: metric.value,
    tone: normalizeBoardTone(metric.tone),
    note: metric.note ?? null,
  }));

  const hasData = sections.some((section) => section.cards.length > 0);
  const normalizedOverallStatus = (payload.overallDataQualityStatus ?? "").trim().toLowerCase();
  const hasPartialSource = (payload.sourceStates ?? []).some((state) => {
    const status = (state.status ?? "").trim().toLowerCase();
    return status !== "good" && status !== "fresh" && status !== "excellent";
  });

  return {
    sections,
    metrics,
    hasData,
    isPartial: Boolean(payload.meta?.isPartial || payload.warnings.length > 0 || hasPartialSource || normalizedOverallStatus !== "good"),
    overallDataQualityStatus: payload.overallDataQualityStatus,
    periodFrom: payload.periodFromUtc ?? null,
    periodTo: payload.periodToUtc ?? null,
    lastRefreshAt: payload.lastRefreshAtUtc ?? null,
    recommendationNote: payload.recommendationNote,
    emptyReason: hasData ? null : payload.meta?.emptyReason ?? "Kombinovani analytics izvori trenutno ne vraćaju dovoljno signala za izvršni board.",
  };
}

function deriveWorstStatus(values: Array<string | null | undefined>): string | null {
  const normalized = values
    .map((value) => (value ?? "").trim().toLowerCase())
    .filter(Boolean);

  if (normalized.some((value) => value === "critical" || value === "error")) return "critical";
  if (normalized.some((value) => value === "warning" || value === "stale")) return "warning";
  if (normalized.some((value) => value === "insufficient_data")) return "insufficient_data";
  if (normalized.some((value) => value === "fresh" || value === "good" || value === "excellent")) return "good";
  return null;
}

function renderSectionCard(card: BoardCard) {
  return (
    <article key={card.id} className={`decision-board-card decision-board-card-${card.kind} decision-board-card-${card.confidenceTone}`}>
      <div className="decision-board-card-head">
        <div className="decision-board-card-head-copy">
          <p className="decision-board-card-module">{card.sourceModule}</p>
          <h3>{card.title}</h3>
        </div>
        <div className={`decision-board-pill decision-board-pill-${card.confidenceTone}`}>
          {card.confidenceLabel}
        </div>
      </div>

      {card.summary ? <p className="decision-board-card-summary">{card.summary}</p> : null}

      <dl className="decision-board-card-facts">
        <div>
          <dt>Očekivani uticaj</dt>
          <dd>{card.expectedImpactRsd != null ? fmtRsd(card.expectedImpactRsd) : "Nije dostupno"}</dd>
        </div>
        {card.measuredImpactRsd != null ? (
          <div>
            <dt>Ostvareni uticaj</dt>
            <dd>{fmtRsd(card.measuredImpactRsd)}</dd>
          </div>
        ) : null}
        {card.realizationRatio != null ? (
          <div>
            <dt>Realizacija</dt>
            <dd>{fmtPctFromRatio(card.realizationRatio, 1)}</dd>
          </div>
        ) : null}
        <div>
          <dt>Rizik ako se ignoriše</dt>
          <dd>{card.riskIfIgnored}</dd>
        </div>
        <div>
          <dt>Sledeći korak</dt>
          <dd>{card.recommendedNextAction}</dd>
        </div>
      </dl>

      {card.warningCodes.length > 0 ? (
        <div className="decision-board-warning-codes" aria-label="Warning codes">
          {card.warningCodes.slice(0, 4).map((code) => (
            <span key={code} className="decision-board-chip">
              {code.replaceAll("_", " ")}
            </span>
          ))}
        </div>
      ) : null}

      <div className="decision-board-card-actions">
        <Link to={card.sourceLink} className="decision-board-link decision-board-link-secondary">
          Otvori izvor
        </Link>
        <Link to={card.actionHref} className="decision-board-link decision-board-link-primary">
          {card.actionCta}
        </Link>
      </div>

      <div className="decision-board-card-footer">
        <span>{card.actionStateLabel}</span>
        <span>{card.dataQualityStatus}</span>
        {card.generatedAtUtc ? <span>{formatDateTime(card.generatedAtUtc, "Nije dostupno")}</span> : null}
      </div>
    </article>
  );
}

function renderSection(section: BoardSection) {
  return (
    <section key={section.key} className="decision-board-section">
      <div className="decision-board-section-head">
        <div>
          <h2>{section.title}</h2>
          <p>{section.description}</p>
        </div>
        <Link to={section.sourceLink} className="decision-board-section-link">
          Otvori izvor
        </Link>
      </div>

      {section.cards.length === 0 ? (
        <div className="decision-board-section-empty" role="status">
          <p>{section.emptyMessage}</p>
          <Link to={section.sourceLink} className="decision-board-link decision-board-link-secondary">
            Otvori izvor
          </Link>
        </div>
      ) : (
        <div className="decision-board-card-grid">
          {section.cards.map((card) => renderSectionCard(card))}
        </div>
      )}
    </section>
  );
}

export function buildExecutiveDecisionBoardModel(payload: BoardPayload): BoardModel {
  if (!payload) {
    return {
      sections: [],
      metrics: [],
      hasData: false,
      isPartial: false,
      overallDataQualityStatus: null,
      periodFrom: null,
      periodTo: null,
      lastRefreshAt: null,
      recommendationNote: "Backend decision board aggregate nije dostupan.",
      emptyReason: "Izvršni board trenutno nema dostupnih signala.",
    };
  }

  return buildBoardModel(payload);
}

export default function ExecutiveDecisionBoardPage() {
  const [payload, setPayload] = useState<BoardPayload>(null);
  const [loadError, setLoadError] = useState<BoardLoadError | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadTick, setReloadTick] = useState(0);

  const loadBoard = useCallback(async (isCancelled?: () => boolean) => {
    setLoading(true);

    try {
      const response = await getDecisionBoardAggregate({ dataScope: "all" });
      if (isCancelled?.()) return;
      setPayload(response);
      setLoadError(null);
    } catch (reason) {
      if (isCancelled?.()) return;
      setPayload(null);
      setLoadError(buildLoadError("decisionBoard", reason, "Izvr�ni board nije dostupan."));
    } finally {
      if (!isCancelled?.()) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    void loadBoard(() => cancelled);
    return () => {
      cancelled = true;
    };
  }, [loadBoard, reloadTick]);

  const model = useMemo(() => buildExecutiveDecisionBoardModel(payload), [payload]);
  const responseMeta = payload?.meta ?? null;
  const refreshSourceState = payload?.sourceStates.find((state) => state.sourceKey === "refresh-status") ?? null;
  const analyticsMetaMessage = getAnalyticsMetaMessage(responseMeta);
  const globalError = !loading && !model.hasData ? loadError : null;
  const isEmpty = !loading && !model.hasData && !globalError;

  return (
    <div className="decision-board-page">
      <AnalyticsTrustHeader
        title="Izvr�ni board odluka"
        description="Jedan pogled koji poredi najva�nije odluke, ocekivani uticaj, rizik i �ta je vec u akciji."
        mode="recommendation"
        periodFrom={model.periodFrom}
        periodTo={model.periodTo}
        lastRefreshAt={model.lastRefreshAt}
        dataFreshnessStatus={refreshSourceState?.status ?? model.overallDataQualityStatus ?? "unknown"}
        refreshIsRunning={false}
        refreshCurrentStep={null}
        isPartial={model.isPartial}
        dataSource="Decision Board aggregate"
        dataQualityStatus={model.overallDataQualityStatus ?? undefined}
        dataQualitySummary={undefined}
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        methodologyHref="/analytics/actions"
        methodologyLabel="Kako se cita board"
        recommendationNote={model.recommendationNote}
        emptyStateReason={model.emptyReason}
        compact
      />

      <AnalyticsRefreshStatusBanner
        status={null}
        loading={loading}
        error={loadError?.message ?? null}
      />

      {globalError ? (
        <AnalyticsErrorState
          title="Izvr�ni board trenutno nije dostupan"
          message={globalError.message}
          errorCode={globalError.errorCode ?? undefined}
          correlationId={globalError.correlationId ?? undefined}
          suggestions={[
            "Proveri status osve�avanja i worker panel.",
            "Otvori kvalitet podataka i pilot readiness.",
            "Poku�aj ponovo kada se izvori vrate.",
          ]}
          onRetry={() => setReloadTick((value) => value + 1)}
          helpHref="/admin/configuration?panel=workers"
          helpLabel="Otvori worker panel"
        />
      ) : null}

      {isEmpty ? (
        <AnalyticsEmptyState
          variant={isAnalyticsMetaInsufficient(responseMeta) ? "insufficient_data" : "no_data"}
          title="Nema dovoljno signala za izvr�ni board"
          message="Board je uspe�no ucitan, ali trenutno nema dovoljno kvalitetnih izvora da bi odluke bile smisleno rangirane."
          reasons={[
            "Proveri pilot readiness i kvalitet podataka.",
            "Proveri da li su Product, Supplier i Inventory signali osve�eni.",
            "Ponovo ucitaj board nakon sledeceg refresh ciklusa.",
          ]}
          actions={[
            { label: "Ponovo proveri", onClick: () => setReloadTick((value) => value + 1) },
            { label: "Kvalitet podataka", href: "/analytics/data-quality" },
            { label: "Pilot spremnost", href: "/analytics/pilot-readiness" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          showDefaultLinks={false}
        />
      ) : null}

      {!loading && !globalError && model.isPartial ? (
        <section className="decision-board-partial-note" role="status">
          <strong>Delimicni signali su dostupni.</strong>
          <span>
            {analyticsMetaMessage ?? "Board kombinuje samo potvrdene izvore i jasno oznacava blokirane ili zastarele signale."}
          </span>
        </section>
      ) : null}

      <section className="decision-board-summary-grid" aria-label="Sa�etak board-a">
        {model.metrics.map((metric) => (
          <article key={metric.label} className={`decision-board-summary-card tone-${metric.tone}`}>
            <span className="decision-board-summary-label">{metric.label}</span>
            <strong className="decision-board-summary-value">{metric.value}</strong>
            {metric.note ? <span className="decision-board-summary-note">{metric.note}</span> : null}
          </article>
        ))}
      </section>

      <div className="decision-board-sections">
        {model.sections.map((section) => renderSection(section))}
      </div>
    </div>
  );
}
