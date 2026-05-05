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

export const canonicalTerms = {
  revenue: { label: "Promet", desc: "Ukupna vrednost prodaje; koristi se za poređenja i udele." },
  marginContribution: { label: "Maržni doprinos", desc: "Zbir razlike između prodajne i nabavne vrednosti. Nije neto profit." },
  marginPct: { label: "Marža %", desc: analyticsMetricDescriptions.marginPct },
  recommendation: { label: "Preporuka", desc: analyticsMetricDescriptions.recommendation },
  reliabilityPct: { label: RECOMMENDATION_RELIABILITY_LABEL, desc: RECOMMENDATION_RELIABILITY_TOOLTIP },
  confidencePct: { label: RECOMMENDATION_CONFIDENCE_LABEL, desc: RECOMMENDATION_CONFIDENCE_TOOLTIP },
  dataQuality: { label: "Kvalitet podataka", desc: "Pokazuje pokrivenost i probleme u ulaznim podacima koji utiču na signale." },
  costCoverage: { label: "Pokrivenost troška", desc: analyticsMetricDescriptions.costCoverage },
  stock: { label: "Zaliha", desc: "Broj jedinica na stanju." },
  outOfStock: { label: "Nema na stanju", desc: "Artikal nema dostupnu količinu na lageru." },
  shortage: { label: "Manjak", desc: "Nedostatak zalihe u odnosu na očekivanu potražnju." },
  nivelacija: { label: "Nivelacija / sniženje", desc: analyticsMetricDescriptions.prePostNivelacijaImpactPct },
  avg7d: { label: "Prosek 7 dana", desc: "Pokazuje prosečnu vrednost metrika za poslednjih 7 dana." },
  sku: { label: "Šifra artikla", desc: "Jedinstveni kod proizvoda (SKU)." },
  velocity: { label: "Brzina prodaje", desc: "Prosečan broj prodatih jedinica po danu." },
  velocityMarginMatrix: { label: "Matrica brzine i marže", desc: "Klasifikacija proizvoda po brzini prodaje i marži." },
  statDeviation: { label: "Statističko odstupanje", desc: "Merilo varijabilnosti signala u okviru izabranog vremenskog prozora." },
  timeWindow: { label: "Vremenski prozor", desc: "Period preko kojeg se računa metrika (npr. poslednjih 30 dana)." },
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
