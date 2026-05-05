import { fmtPct, fmtRsd } from "./analyticsFormatters";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_CONFIDENCE_TOOLTIP,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_RELIABILITY_TOOLTIP,
} from "./canonicalRecommendationSemantics";

export const analyticsMetricDescriptions = {
  marginPct:
    "Marza % prikazuje udeo marznog doprinosa u prometu sa dostupnim troskom. Nije neto profit i treba je citati uz kvalitet troska.",
  popRevenueChangePct:
    "PoP promena % poredi promet sa prethodnim uporedivim periodom iste duzine. Ako prethodna baza ne postoji ili je nula, procenat nije pun signal rasta.",
  reliabilityPct:
    `${RECOMMENDATION_RELIABILITY_LABEL} - ${RECOMMENDATION_RELIABILITY_TOOLTIP}`,
  recommendationConfidencePct:
    `${RECOMMENDATION_CONFIDENCE_LABEL} - ${RECOMMENDATION_CONFIDENCE_TOOLTIP}`,
  prePostNivelacijaImpactPct:
    "Pre/post nivelacija uticaj % meri promenu prometa samo na uporedivom skupu artikala sa prodajom i pre i posle prve nivelacije. Nije isto sto i PoP trend.",
  costCoverage:
    "Pokrivenost troska pokazuje koliki deo prometa ima direktan ili procenjeni trosak. Niza pokrivenost znaci da marzu i preporuku treba citati opreznije.",
  recommendation:
    "Preporuka je pomocni signal za fokus, a ne automatska odluka. Tumaci se zajedno sa razlogom preporuke, marzom, PoP trendom i pokrivenoscu podataka.",
} as const;

export function buildPopMetricDescription(previousPeriodRevenue: number | null | undefined): string {
  const previousPeriodText = previousPeriodRevenue == null
    ? "Prethodni uporedivi period nije dostupan."
    : `Prethodni period: ${fmtRsd(previousPeriodRevenue)}.`;

  return `${analyticsMetricDescriptions.popRevenueChangePct} ${previousPeriodText}`;
}

export function buildPrePostNivelacijaImpactDescription(
  coveragePct: number | null | undefined,
  noteSuffix?: string
): string {
  const coverageText = coveragePct == null
    ? "Pokrice nije dostupno."
    : `Pokrice: ${fmtPct(coveragePct, 1)} prometa.`;

  return `${analyticsMetricDescriptions.prePostNivelacijaImpactPct} ${coverageText}${noteSuffix ? ` ${noteSuffix}` : ""}`;
}
