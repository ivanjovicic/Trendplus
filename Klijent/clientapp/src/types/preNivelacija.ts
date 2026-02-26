export interface PreNivelacijaScoreBreakdown {
  stockPressure: number;
  velocityRisk: number;
  recencyRisk: number;
  markdownOpportunity: number;
  marginPotential: number;
  seasonRecencyBoost: number;
}

export interface PreNivelacijaScenario {
  expectedUnits30d: number;
  expectedRevenue30d: number;
  expectedMargin30d: number;
  effectivePrice: number;
}

export interface PreNivelacijaSkuCandidate {
  artikalId: number;
  sku: string;
  supplierId?: number | null;
  seasonId?: number | null;
  footwearTypeId?: number | null;
  supplierName: string;
  category: string;
  footwearType: string;
  season: string;
  stockUnits: number;
  units180: number;
  velocity180: number;
  daysSinceLastSale: number;
  markdownEvents: number;
  avgMarkdownPct: number;
  grossMarginPctEst: number;
  seasonRecencyBoost: number;
  preNivelacijaScore: number;
  priorityBand: "high" | "medium" | "low" | string;
  scoreBreakdown: PreNivelacijaScoreBreakdown;
  scenarioHighlightNow: PreNivelacijaScenario;
  scenarioMarkdownNow: PreNivelacijaScenario;
  marginDeltaHighlightVsMarkdown: number;
  revenueDeltaHighlightVsMarkdown: number;
  confidence: "High" | "Medium" | "Low" | string;
}

export interface PreNivelacijaSupplierAction {
  supplierId?: number | null;
  supplierName: string;
  highPrioritySkuCount: number;
  candidateSkuCount: number;
  stockUnitsAtRisk: number;
  estimatedAvoidableMarkdownLoss: number;
  expectedHighlightRevenueUplift: number;
  actionScore: number;
  weekOverWeekRiskDeltaPct: number;
}

export interface PreNivelacijaQueueItem {
  artikalId: number;
  sku: string;
  supplierName: string;
  preNivelacijaScore: number;
  priorityBand: string;
  owner: string;
  status: string;
  dueDateUtc: string;
}

export interface PreNivelacijaQueues {
  highlightNow: PreNivelacijaQueueItem[];
  monitor: PreNivelacijaQueueItem[];
  likelyMarkdownSoon: PreNivelacijaQueueItem[];
}

export interface PreNivelacijaAlert {
  type: string;
  severity: "critical" | "warning" | "info" | string;
  message: string;
  supplierName?: string;
  artikalId?: number;
}

export interface PreNivelacijaSummary {
  supplierCount: number;
  candidatesCount: number;
  highPriorityCount: number;
  totalStockAtRisk: number;
  estimatedAvoidableMarkdownLoss: number;
  expectedHighlightRevenueUplift: number;
  averagePreNivelacijaScore: number;
}

export interface PreNivelacijaPriorityResponse {
  generatedAtUtc: string;
  formulaVersion: string;
  formulaDescription: string;
  summary: PreNivelacijaSummary;
  supplierLeaderboard: PreNivelacijaSupplierAction[];
  candidates: PreNivelacijaSkuCandidate[];
  queues: PreNivelacijaQueues;
  alerts: PreNivelacijaAlert[];
  page: number;
  pageSize: number;
  totalCandidates: number;
}
