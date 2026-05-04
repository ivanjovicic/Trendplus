import { fmtPct, fmtRsd } from "./analyticsFormatters";

export const analyticsMetricDescriptions = {
  marginPct:
    "Marža % prikazuje udeo maržnog doprinosa u prometu sa dostupnim troškom. Nije neto profit i mora se čitati uz kvalitet troška.",
  popRevenueChangePct:
    "PoP promena % poredi promet sa prethodnim uporedivim periodom iste dužine. Ako prethodna baza ne postoji ili je nula, procenat nije pun signal rasta.",
  reliabilityPct:
    "Pouzdanost % pokazuje koliko je signal pokriven kvalitetnim podacima. Nije statistički interval poverenja niti garancija ishoda.",
  recommendationConfidencePct:
    "Sigurnost preporuke % pokazuje koliko je preporuka upotrebljiva za odluku. Veća vrednost znači stabilniji signal, ne garanciju poslovnog ishoda.",
  prePostNivelacijaImpactPct:
    "Pre/post nivelacija uticaj % meri promenu prometa samo na uporedivom skupu artikala sa prodajom i pre i posle prve nivelacije. Nije isto što i PoP trend.",
  costCoverage:
    "Pokrivenost troška pokazuje koliki deo prometa ima direktan ili procenjeni trošak. Niža pokrivenost znači da maržu i preporuku treba čitati opreznije.",
  recommendation:
    "Preporuka je pomoćni signal za fokus, a ne automatska odluka. Tumači se zajedno sa razlogom preporuke, maržom, PoP trendom i pokrivenošću podataka.",
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
