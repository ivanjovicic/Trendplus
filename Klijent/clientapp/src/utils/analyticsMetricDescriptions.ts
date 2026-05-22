import { fmtPct, fmtRsd } from "./analyticsFormatters";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_CONFIDENCE_TOOLTIP,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_RELIABILITY_TOOLTIP,
} from "./canonicalRecommendationSemantics";

export const analyticsMetricDescriptions = {
  revenue:
    "Promet predstavlja ukupnu vrednost prodaje u izabranom periodu.",
  quantity:
    "Kolicina predstavlja ukupan broj prodatih jedinica (komada) u izabranom periodu.",
  marginContribution:
    "Marzni doprinos je razlika prodajne i nabavne vrednosti na prometu sa dostupnim troskom. Nije neto profit.",
  marginPct:
    "Marza % prikazuje udeo marznog doprinosa u prometu sa dostupnim troskom. Nije neto profit i treba je citati uz kvalitet troska.",
  confidencePct:
    "Pouzdanost pokazuje koliko je signal stabilan i potkrepljen podacima; niza vrednost znaci veci rizik pogresne odluke.",
  popRevenueChangePct:
    "PoP promena % poredi promet sa prethodnim uporedivim periodom iste duzine. Ako prethodna baza ne postoji ili je nula, procenat nije pun signal rasta.",
  reliabilityPct:
    `${RECOMMENDATION_RELIABILITY_LABEL} - ${RECOMMENDATION_RELIABILITY_TOOLTIP}`,
  recommendationConfidencePct:
    `${RECOMMENDATION_CONFIDENCE_LABEL} - ${RECOMMENDATION_CONFIDENCE_TOOLTIP}`,
  recommendationSafety:
    "Sigurnost preporuke predstavlja stepen poverenja da je preporucena akcija poslovno opravdana za izabrani period i filtere.",
  prePostNivelacijaImpactPct:
    "Pre/post nivelacija uticaj % meri promenu prometa samo na uporedivom skupu artikala sa prodajom i pre i posle prve nivelacije. Nije isto sto i PoP trend.",
  costCoveragePct:
    "Pokrivenost troska pokazuje koliki deo prometa ima dostupnu ili procenjenu nabavnu cenu.",
  costCoverage:
    "Pokrivenost troska pokazuje koliki deo prometa ima direktan ili procenjeni trosak. Niza pokrivenost znaci da marzu i preporuku treba citati opreznije.",
  velocity:
    "Velocity (brzina prodaje) pokazuje prosecan broj prodatih jedinica po danu.",
  outOfStock:
    "OOS (out of stock) znaci da artikal nije dostupan za prodaju jer nema zalihe.",
  lostSales:
    "Lost sales je procena potencijalno izgubljenog prometa zbog nedostupnosti zaliha.",
  dataFreshness:
    "Data freshness pokazuje koliko su podaci svezi i koliko je vremena proslo od poslednjeg osvezavanja.",
  completeness:
    "Completeness pokazuje da li su kljucna polja potrebna za analitiku popunjena i upotrebljiva.",
  recommendation:
    "Preporuka je pomocni signal za fokus, a ne automatska odluka. Tumaci se zajedno sa razlogom preporuke, marzom, PoP trendom i pokrivenoscu podataka.",
  recommendationReason:
    "Razlog preporuke objasnjava koji su signali doveli do odluke (trend, marza, zaliha, kvalitet podataka).",
  reasonCodes:
    "Reason codes su kratke backend oznake signala (npr. stock_gap, low_cost_coverage, stale_sales) za audit i brze provere.",
} as const;

export const canonicalTerms = {
  revenue: { label: "Promet", desc: analyticsMetricDescriptions.revenue },
  quantity: { label: "Kolicina", desc: analyticsMetricDescriptions.quantity },
  marginContribution: { label: "Marzni doprinos", desc: analyticsMetricDescriptions.marginContribution },
  marginPct: { label: "Marza %", desc: analyticsMetricDescriptions.marginPct },
  popChange: { label: "PoP promena", desc: analyticsMetricDescriptions.popRevenueChangePct },
  recommendation: { label: "Preporuka", desc: analyticsMetricDescriptions.recommendation },
  recommendationReason: { label: "Razlog preporuke", desc: analyticsMetricDescriptions.recommendationReason },
  reasonCodes: { label: "Reason codes", desc: analyticsMetricDescriptions.reasonCodes },
  confidence: { label: "Pouzdanost", desc: analyticsMetricDescriptions.confidencePct },
  recommendationSafety: { label: "Sigurnost preporuke", desc: analyticsMetricDescriptions.recommendationSafety },
  reliabilityPct: { label: RECOMMENDATION_RELIABILITY_LABEL, desc: RECOMMENDATION_RELIABILITY_TOOLTIP },
  confidencePct: { label: RECOMMENDATION_CONFIDENCE_LABEL, desc: RECOMMENDATION_CONFIDENCE_TOOLTIP },
  dataQuality: { label: "Kvalitet podataka", desc: "Pokazuje pokrivenost i probleme u ulaznim podacima koji uticu na signale." },
  dataFreshness: { label: "Svezina podataka", desc: analyticsMetricDescriptions.dataFreshness },
  completeness: { label: "Kompletnost", desc: analyticsMetricDescriptions.completeness },
  costCoverage: { label: "Pokrivenost troska", desc: analyticsMetricDescriptions.costCoverage },
  prePostImpact: { label: "Pre/Post nivelacija uticaj", desc: analyticsMetricDescriptions.prePostNivelacijaImpactPct },
  stock: { label: "Zaliha", desc: "Broj jedinica na stanju." },
  outOfStock: { label: "Nema na stanju", desc: analyticsMetricDescriptions.outOfStock },
  lostSales: { label: "Izgubljena prodaja", desc: analyticsMetricDescriptions.lostSales },
  shortage: { label: "Manjak", desc: "Nedostatak zalihe u odnosu na ocekivanu potraznju." },
  nivelacija: { label: "Nivelacija / snizenje", desc: analyticsMetricDescriptions.prePostNivelacijaImpactPct },
  avg7d: { label: "Prosek 7 dana", desc: "Pokazuje prosecnu vrednost metrika za poslednjih 7 dana." },
  sku: { label: "Sifra artikla", desc: "Jedinstveni kod proizvoda (SKU)." },
  velocity: { label: "Brzina prodaje", desc: analyticsMetricDescriptions.velocity },
  velocityMarginMatrix: { label: "Matrica brzine i marze", desc: "Klasifikacija proizvoda po brzini prodaje i marzi." },
  statDeviation: { label: "Statisticko odstupanje", desc: "Merilo varijabilnosti signala u okviru izabranog vremenskog prozora." },
  timeWindow: { label: "Vremenski prozor", desc: "Period preko kojeg se racuna metrika (npr. poslednjih 30 dana)." },
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
