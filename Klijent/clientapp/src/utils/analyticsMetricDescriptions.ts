import { fmtPct, fmtRsd } from "./analyticsFormatters";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_CONFIDENCE_TOOLTIP,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_RELIABILITY_TOOLTIP,
} from "./canonicalRecommendationSemantics";
import { getAnalyticsMetricDefinition } from "./analyticsMetricDefinitions";

const totalRevenueDefinition = getAnalyticsMetricDefinition("totalRevenue");
const soldUnitsDefinition = getAnalyticsMetricDefinition("soldUnits");
const marginContributionDefinition = getAnalyticsMetricDefinition("marginContribution");
const lostSalesDefinition = getAnalyticsMetricDefinition("lostSales");

export const analyticsMetricDescriptions = {
  revenue: totalRevenueDefinition.description,
  quantity: soldUnitsDefinition.description,
  marginContribution: marginContributionDefinition.description,
  marginPct:
    "Marža % prikazuje udeo maržnog doprinosa u prometu sa dostupnim troškom. Nije neto profit i treba je čitati uz kvalitet troška.",
  confidencePct:
    "Pouzdanost pokazuje koliko je signal stabilan i potkrepljen podacima; niža vrednost znači veći rizik pogrešne odluke.",
  popRevenueChangePct:
    "PoP promena % poredi promet sa prethodnim uporedivim periodom iste dužine. Ako prethodna baza ne postoji ili je nula, procenat nije pun signal rasta.",
  reliabilityPct:
    `${RECOMMENDATION_RELIABILITY_LABEL} - ${RECOMMENDATION_RELIABILITY_TOOLTIP}`,
  recommendationConfidencePct:
    `${RECOMMENDATION_CONFIDENCE_LABEL} - ${RECOMMENDATION_CONFIDENCE_TOOLTIP}`,
  recommendationSafety:
    "Sigurnost preporuke predstavlja stepen poverenja da je preporučena akcija poslovno opravdana za izabrani period i filtere.",
  prePostNivelacijaImpactPct:
    "Pre/post nivelacija uticaj % meri promenu prometa samo na uporedivom skupu artikala sa prodajom i pre i posle prve nivelacije. Nije isto što i PoP trend.",
  costCoveragePct:
    "Pokrivenost troška pokazuje koliki deo prometa ima dostupnu ili procenjenu nabavnu cenu.",
  costCoverage:
    "Pokrivenost troška pokazuje koliki deo prometa ima direktan ili procenjeni trošak. Niža pokrivenost znači da maržu i preporuku treba čitati opreznije.",
  velocity:
    "Velocity (brzina prodaje) pokazuje prosečan broj prodatih jedinica po danu.",
  outOfStock:
    "OOS (out of stock) znači da artikal nije dostupan za prodaju jer nema zalihe.",
  lostSales: lostSalesDefinition.description,
  dataFreshness:
    "Data freshness pokazuje koliko su podaci sveži i koliko je vremena prošlo od poslednjeg osvežavanja.",
  completeness:
    "Completeness pokazuje da li su ključna polja potrebna za analitiku popunjena i upotrebljiva.",
  recommendation:
    "Preporuka je pomoćni signal za fokus, a ne automatska odluka. Tumači se zajedno sa razlogom preporuke, maržom, PoP trendom i pokrivenošću podataka.",
  recommendationReason:
    "Razlog preporuke objašnjava koji su signali doveli do odluke (trend, marža, zaliha, kvalitet podataka).",
  reasonCodes:
    "Reason codes su kratke backend oznake signala (npr. stock_gap, low_cost_coverage, stale_sales) za audit i brze provere.",
} as const;

export const canonicalTerms = {
  revenue: { label: "Promet", desc: analyticsMetricDescriptions.revenue },
  quantity: { label: "Količina", desc: analyticsMetricDescriptions.quantity },
  marginContribution: { label: "Maržni doprinos", desc: analyticsMetricDescriptions.marginContribution },
  marginPct: { label: "Marža %", desc: analyticsMetricDescriptions.marginPct },
  popChange: { label: "PoP promena", desc: analyticsMetricDescriptions.popRevenueChangePct },
  recommendation: { label: "Preporuka", desc: analyticsMetricDescriptions.recommendation },
  recommendationReason: { label: "Razlog preporuke", desc: analyticsMetricDescriptions.recommendationReason },
  reasonCodes: { label: "Reason codes", desc: analyticsMetricDescriptions.reasonCodes },
  confidence: { label: "Pouzdanost", desc: analyticsMetricDescriptions.confidencePct },
  recommendationSafety: { label: "Sigurnost preporuke", desc: analyticsMetricDescriptions.recommendationSafety },
  reliabilityPct: { label: RECOMMENDATION_RELIABILITY_LABEL, desc: RECOMMENDATION_RELIABILITY_TOOLTIP },
  confidencePct: { label: RECOMMENDATION_CONFIDENCE_LABEL, desc: RECOMMENDATION_CONFIDENCE_TOOLTIP },
  dataQuality: { label: "Kvalitet podataka", desc: "Pokazuje pokrivenost i probleme u ulaznim podacima koji utiču na signale." },
  dataFreshness: { label: "Svežina podataka", desc: analyticsMetricDescriptions.dataFreshness },
  completeness: { label: "Kompletnost", desc: analyticsMetricDescriptions.completeness },
  costCoverage: { label: "Pokrivenost troška", desc: analyticsMetricDescriptions.costCoverage },
  prePostImpact: { label: "Pre/Post nivelacija uticaj", desc: analyticsMetricDescriptions.prePostNivelacijaImpactPct },
  stock: { label: "Zaliha", desc: "Broj jedinica na stanju." },
  outOfStock: { label: "Nema na stanju", desc: analyticsMetricDescriptions.outOfStock },
  lostSales: { label: "Izgubljena prodaja", desc: analyticsMetricDescriptions.lostSales },
  shortage: { label: "Manjak", desc: "Nedostatak zalihe u odnosu na očekivanu potražnju." },
  nivelacija: { label: "Nivelacija / sniženje", desc: analyticsMetricDescriptions.prePostNivelacijaImpactPct },
  avg7d: { label: "Prosek 7 dana", desc: "Pokazuje prosečnu vrednost metrika za poslednjih 7 dana." },
  sku: { label: "Šifra artikla", desc: "Jedinstveni kod proizvoda (SKU)." },
  velocity: { label: "Brzina prodaje", desc: analyticsMetricDescriptions.velocity },
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
    ? "Pokriće nije dostupno."
    : `Pokriće: ${fmtPct(coveragePct, 1)} prometa.`;

  return `${analyticsMetricDescriptions.prePostNivelacijaImpactPct} ${coverageText}${noteSuffix ? ` ${noteSuffix}` : ""}`;
}
