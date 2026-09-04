import type { CSSProperties } from "react";

export type CanonicalRecommendationStatus =
  | "increase_focus"
  | "maintain"
  | "review"
  | "do_not_trust"
  | "insufficient_data";

export type RecommendationQualityStatus =
  | "good"
  | "warning"
  | "critical"
  | "insufficient_data";

export const RECOMMENDATION_STATUS_PRIORITY: Record<CanonicalRecommendationStatus, number> = {
  increase_focus: 5,
  maintain: 4,
  review: 3,
  insufficient_data: 2,
  do_not_trust: 1,
};

export const RECOMMENDATION_RELIABILITY_LABEL = "Pouzdanost signala %";
export const RECOMMENDATION_CONFIDENCE_LABEL = "Sigurnost preporuke %";
export const RECOMMENDATION_SIGNAL_UNAVAILABLE =
  "Pouzdanost nije dostupna (backend nije dostavio confidence/reliability signal).";

export const RECOMMENDATION_RELIABILITY_TOOLTIP =
  "Pouzdanost signala % pokazuje koliko je ulazni signal stabilan i podatkovno pokriven. Nije garancija ishoda.";

export const RECOMMENDATION_CONFIDENCE_TOOLTIP =
  "Sigurnost preporuke % pokazuje koliko je backend preporuka upotrebljiva za odluku. Nije garancija poslovnog ishoda.";

const RECOMMENDATION_REASON_LABELS: Record<string, string> = {
  high_velocity: "Artikal se brzo prodaje.",
  low_stock: "Zaliha je ispod bezbednog nivoa.",
  poor_margin: "Marža je ispod željenog nivoa.",
  stale_stock: "Artikal dugo nema prodaju.",
  missing_cost: "Nedostaje nabavna cena.",
  missing_supplier: "Nedostaje dobavljač.",
  insufficient_history: "Nema dovoljno istorije za sigurnu preporuku.",
  low_sample_size: "Uzorak prodaje je premali za sigurnu odluku.",
  no_sales_in_period: "U izabranom periodu nema evidentirane prodaje.",
  missing_last_sale: "Nedostaje datum poslednje prodaje.",
  replenish_needed: "Potrebna je dopuna da bi se izbegao gubitak prodaje.",
  high_stock_risk: "Postoji rizik od viška zalihe.",
  data_quality_blocker: "Kvalitet podataka blokira pouzdanu preporuku.",
  data_quality_critical: "Kvalitet podataka je kritičan i traži proveru.",
  expected_impact_denominator_missing: "Nedostaje ulaz za procenu očekivanog uticaja.",
  missing_cost_coverage: "Marža je procena jer deo nabavne cene nije potvrđen.",
  limited_nivelacija_coverage: "Pre/post poređenje nema dovoljno pokrića.",
  previous_period_missing: "Nedostaje prethodni period za poređenje.",
  no_previous_baseline: "Nema prethodne baze za poređenje.",
  pop_unavailable: "Poređenje sa prethodnim periodom nije dostupno.",
  insufficient_data: "Signal nije dovoljno jak za pouzdanu preporuku.",
  positive_trend: "Trend podržava rast.",
  weak_signal: "Signal je slab i traži oprez.",
  monitor_only: "Potrebno je samo praćenje.",
  confidence_monitor: "Pouzdanost sugeriše praćenje, ne agresivnu akciju.",
  signal_gap: "Signal je previše ograničen za čvrstu odluku.",
  selected_action_has_stronger_signal: "Odabrana preporuka ima jači signal.",
  demand_not_weak_enough: "Potražnja još nije dovoljno slaba za sniženje.",
  no_replenishment_gap: "Nema dovoljno razlike do minimalne zalihe.",
  margin_support_missing: "Marža ne podržava jaču akciju.",
  understock_risk: "Postoji rizik od premale zalihe.",
  enough_signal_for_action: "Signal je dovoljan za aktivniju akciju.",
  no_blocking_data_issue: "Nema blokirajućeg problema sa podacima.",
  weak_demand: "Potražnja nije dovoljno jaka.",
  negative_trend: "Trend nije povoljan.",
  stock_gap: "Postoji rupa u zalihama.",
  unknown_entity: "Entitet nije prepoznat.",
  new_entity: "Entitet je nov i nema dovoljno istorije.",
  tiny_sample: "Uzorak je premali za pouzdanu odluku.",
  unstable_margin: "Marža je nestabilna.",
  unknown_heavy_dataset: "Skup podataka sadrži previše nepoznatih vrednosti.",
};

export function recommendationReasonLabel(code: string | null | undefined): string {
  const normalized = (code ?? "").trim().toLowerCase();
  return RECOMMENDATION_REASON_LABELS[normalized] ?? "Dodatno ograničenje iz procene.";
}

export function isCanonicalRecommendationStatus(value: string | null | undefined): value is CanonicalRecommendationStatus {
  return value === "increase_focus"
    || value === "maintain"
    || value === "review"
    || value === "do_not_trust"
    || value === "insufficient_data";
}

export function recommendationStatusLabel(status: CanonicalRecommendationStatus): string {
  if (status === "increase_focus") return "Pojacaj";
  if (status === "maintain") return "Zadrzi";
  if (status === "review") return "Pregledaj";
  if (status === "do_not_trust") return "Ne veruj";
  return "Nedovoljno podataka";
}

export function recommendationStatusTone(status: CanonicalRecommendationStatus): "boost" | "keep" | "review" | "reduce" | "na" {
  if (status === "increase_focus") return "boost";
  if (status === "maintain") return "keep";
  if (status === "review") return "review";
  if (status === "do_not_trust") return "reduce";
  return "na";
}

export function recommendationStatusTooltipBrief(status: CanonicalRecommendationStatus): string {
  if (status === "increase_focus") return "Pozitivan signal; povecati fokus uz standardnu kontrolu rizika.";
  if (status === "maintain") return "Stabilan signal; zadrzati trenutni nivo fokusa.";
  if (status === "review") return "Mesovit signal; potreban rucni pregled pre promene fokusa.";
  if (status === "do_not_trust") return "Signal je nepouzdan za akciju; ne donositi odluku bez dodatne provere.";
  return "Nedovoljno podataka za pouzdanu preporuku.";
}

export function normalizeRecommendationPct(value: number | null | undefined): number | null {
  if (value == null || !Number.isFinite(value)) return null;
  const normalized = value <= 1 ? value * 100 : value;
  return Math.max(0, Math.min(100, normalized));
}

export function normalizeRecommendationQualityStatus(
  value: string | null | undefined
): RecommendationQualityStatus {
  if (value === "good" || value === "warning" || value === "critical") return value;
  return "insufficient_data";
}

export function recommendationQualityLabel(status: RecommendationQualityStatus): string {
  if (status === "good") return "Dobar kvalitet podataka";
  if (status === "warning") return "Upozorenje kvaliteta podataka";
  if (status === "critical") return "Kritican kvalitet podataka";
  return "Nedovoljno podataka";
}

export function recommendationQualityTone(
  status: RecommendationQualityStatus
): "good" | "warning" | "critical" | "insufficient_data" {
  if (status === "good") return "good";
  if (status === "warning") return "warning";
  if (status === "critical") return "critical";
  return "insufficient_data";
}

export function recommendationQualityStyle(status: RecommendationQualityStatus): CSSProperties {
  if (status === "good") {
    return { color: "var(--success, #16a34a)" };
  }
  if (status === "warning") {
    return { color: "var(--warning, #d97706)" };
  }
  if (status === "critical") {
    return { color: "var(--danger, #dc2626)" };
  }
  return { color: "var(--text-muted, #6b7280)" };
}

export function recommendationReasonHintFromCode(code: string): string | null {
  const normalized = code.trim().toLowerCase();
  if (normalized === "missing_cost_coverage") {
    return "Marza je procena jer deo nabavne cene nije istorijski potvrdjen.";
  }
  if (normalized === "tiny_sample") {
    return "Preporuka nije pouzdana zbog malog uzorka.";
  }
  if (normalized === "previous_period_missing" || normalized === "no_previous_baseline" || normalized === "pop_unavailable") {
    return "Nema prethodnog perioda za PoP poredjenje.";
  }
  return null;
}

export function recommendationReasonHints(reasonCodes: string[]): string[] {
  return Array.from(
    new Set(
      reasonCodes
        .map((code) => recommendationReasonHintFromCode(code))
        .filter((hint): hint is string => Boolean(hint))
    )
  );
}
