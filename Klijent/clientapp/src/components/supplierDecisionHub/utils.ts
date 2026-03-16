import type { RecommendationCode } from "../../services/supplierDecisionHubApi";

export type RecommendationUiMeta = {
  label: string;
  razlog: string;
  ton: "pozitivno" | "upozorenje" | "rizik" | "neutralno";
};

const rsdFormatter = new Intl.NumberFormat("sr-RS", {
  maximumFractionDigits: 0,
});

const numberFormatter = new Intl.NumberFormat("sr-RS", {
  maximumFractionDigits: 1,
});

const integerFormatter = new Intl.NumberFormat("sr-RS", {
  maximumFractionDigits: 0,
});

const dateFormatter = new Intl.DateTimeFormat("sr-RS", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

export function formatCurrency(value: number): string {
  return `${rsdFormatter.format(Number.isFinite(value) ? value : 0)} RSD`;
}

export function formatInteger(value: number): string {
  return integerFormatter.format(Number.isFinite(value) ? value : 0);
}

export function formatNumber(value: number, digits = 1): string {
  return new Intl.NumberFormat("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(Number.isFinite(value) ? value : 0);
}

export function formatRatioPercent(value: number, digits = 1): string {
  return `${new Intl.NumberFormat("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format((Number.isFinite(value) ? value : 0) * 100)}%`;
}

export function formatPercentValue(value: number, digits = 1): string {
  return `${new Intl.NumberFormat("sr-RS", {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  }).format(Number.isFinite(value) ? value : 0)}%`;
}

export function formatScore(value: number): string {
  return `${formatNumber(value, 1)}/100`;
}

export function formatDate(value?: string | null): string {
  if (!value) return "Nema podatka";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Nema podatka";
  return dateFormatter.format(date);
}

export function formatDateRange(from?: string | null, to?: string | null): string {
  return `${formatDate(from)} - ${formatDate(to)}`;
}

export function confidenceLabel(score: number): string {
  if (score >= 75) return "Visoka";
  if (score >= 55) return "Srednja";
  return "Niža";
}

export function signalQualityLabel(value: string): string {
  switch (value?.toLowerCase()) {
    case "high":
      return "Jak signal";
    case "medium":
      return "Srednji signal";
    case "low":
      return "Slab signal";
    default:
      return "Bez oznake";
  }
}

export function signalQualityReasonLabel(value: string): string {
  if (!value) return "Nema dodatnog objašnjenja.";

  const translations: Record<string, string> = {
    base_nivelacija_view_marked_low_signal: "Osnovni pre/post signal je obeležen kao slab.",
    sparse_pre_period_coverage: "Pre period ima retku pokrivenost podacima.",
    sparse_post_period_coverage: "Post period ima retku pokrivenost podacima.",
    no_sales_before_first_markdown: "Nema prodaje pre prvog sniženja.",
    stock_proxy_clamped_to_zero: "Proxy zalihe je korigovan na nulu.",
    stockout_before_markdown: "Artikal je ostao bez zaliha pre sniženja.",
    sufficient_pre_markdown_signal: "Signal pre sniženja je dovoljan za tumačenje.",
    no_reason: "Nema dodatnog objašnjenja.",
  };

  return value
    .split(";")
    .map((part) => part.trim())
    .filter(Boolean)
    .map((part) => translations[part] ?? "Potrebna je ručna provera signala.")
    .join(" ");
}

export function getRecommendationMeta(code: RecommendationCode | string): RecommendationUiMeta {
  switch (code) {
    case "EXPAND":
      return {
        label: "Povecati saradnju",
        razlog: "Dobavljač ima dobar rezultat pre sniženja i zadržava zdravu marginu.",
        ton: "pozitivno",
      };
    case "EXPAND_SELECTIVELY":
      return {
        label: "Povecati selektivno",
        razlog: "Dobavljač ima jake kategorije, ali ne i u celom asortimanu.",
        ton: "upozorenje",
      };
    case "PRICE_NEGOTIATE":
      return {
        label: "Pregovarati o ceni",
        razlog: "Potražnja se otvara tek posle sniženja, pa je ulazna cena verovatno previsoka.",
        ton: "upozorenje",
      };
    case "ASSORTMENT_REDUCE":
      return {
        label: "Smanjiti nabavku",
        razlog: "Prodaja previše zavisi od sniženja i vezuje kapital u zalihama.",
        ton: "rizik",
      };
    case "OOS_FALSE_NEGATIVE":
      return {
        label: "Proveriti zalihe",
        razlog: "Rezultat može delovati slabije zato što je artikal ostajao bez zaliha pre sniženja.",
        ton: "upozorenje",
      };
    case "REVIEW_QUALITY":
      return {
        label: "Proveriti kvalitet",
        razlog: "Povraćaji ili kvalitet mogu kočiti bezbedno širenje saradnje.",
        ton: "rizik",
      };
    default:
      return {
        label: "Zadrzati stanje",
        razlog: "Signal je mešovit i za sada je najbolje zadržati postojeći nivo nabavke.",
        ton: "neutralno",
      };
  }
}
