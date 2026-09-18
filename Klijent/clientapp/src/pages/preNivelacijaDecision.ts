import type { AnalyticsTableColumn } from "../types/analyticsTable";
import type { PreNivelacijaRecommendation, PreNivelacijaSkuCandidate } from "../types/preNivelacija";
import {
  RECOMMENDATION_RELIABILITY_LABEL,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";

export type FiniteNumber = number | null;

export type NormalizedScenario = {
  expectedUnits30d: FiniteNumber;
  expectedRevenue30d: FiniteNumber;
  expectedMargin30d: FiniteNumber;
  effectivePrice: FiniteNumber;
};

export type DecisionStatus = PreNivelacijaRecommendation["status"];

export type DecisionCandidate = Omit<
  PreNivelacijaSkuCandidate,
  | "stockUnits"
  | "units180"
  | "velocity180"
  | "daysSinceLastSale"
  | "markdownEvents"
  | "avgMarkdownPct"
  | "grossMarginPctEst"
  | "seasonRecencyBoost"
  | "preNivelacijaScore"
  | "scoreBreakdown"
  | "scenarioHighlightNow"
  | "scenarioMarkdownNow"
  | "marginDeltaHighlightVsMarkdown"
  | "revenueDeltaHighlightVsMarkdown"
  | "reliabilityPct"
  | "decisionScore"
> & {
  stockUnits: FiniteNumber;
  units180: FiniteNumber;
  velocity180: FiniteNumber;
  daysSinceLastSale: FiniteNumber;
  markdownEvents: FiniteNumber;
  avgMarkdownPct: FiniteNumber;
  grossMarginPctEst: FiniteNumber;
  seasonRecencyBoost: FiniteNumber;
  preNivelacijaScore: FiniteNumber;
  scoreBreakdown: {
    stockPressure: FiniteNumber;
    velocityRisk: FiniteNumber;
    recencyRisk: FiniteNumber;
    markdownOpportunity: FiniteNumber;
    marginPotential: FiniteNumber;
    seasonRecencyBoost: FiniteNumber;
  };
  scenarioHighlightNow: NormalizedScenario;
  scenarioMarkdownNow: NormalizedScenario;
  marginDeltaHighlightVsMarkdown: FiniteNumber;
  revenueDeltaHighlightVsMarkdown: FiniteNumber;
  reliabilityPct: FiniteNumber;
  decisionScore: FiniteNumber;
  revenueDelta: FiniteNumber;
  marginDelta: FiniteNumber;
  confidencePct: number | null;
  confidenceAvailable: boolean;
  reliabilityAvailable: boolean;
  recommendationAllowed: boolean;
  decisionScoreAvailable: boolean;
  status: DecisionStatus;
  statusReason: string;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

export const decisionColumns: AnalyticsTableColumn<DecisionCandidate>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "supplierName", header: "Dobavljač", dataType: "text" },
  { key: "preNivelacijaScore", header: "Skor nivelacije", dataType: "number" },
  { key: "stockUnits", header: "Zaliha (kom)", dataType: "number" },
  { key: "daysSinceLastSale", header: "Dana bez prodaje", dataType: "number" },
  { key: "revenueDelta", header: "Isticanje vs sniženje (prihod)", dataType: "currency", getValue: (row) => row.recommendationAllowed ? row.revenueDelta : null },
  { key: "reliabilityPct", header: RECOMMENDATION_RELIABILITY_LABEL, dataType: "percent", getValue: (row) => row.reliabilityAvailable ? row.reliabilityPct : null },
  { key: "decisionScore", header: "Ocena preporuke", dataType: "number", getValue: (row) => row.decisionScoreAvailable ? row.decisionScore : null },
  { key: "status", header: "Preporuka", dataType: "text" },
];
