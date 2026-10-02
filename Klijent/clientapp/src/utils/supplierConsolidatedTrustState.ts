import type { SupplierTab, SupplierTrustHeaderPayload } from "../pages/supplierSharedState";

export const SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS: Record<string, string> = {
  good: "Pouzdani podaci",
  warning: "Potreban oprez",
  insufficient_data: "Nedovoljno podataka",
  error: "Problem u podacima",
  critical: "Kritičan problem u podacima",
  unknown: "Pouzdanost nije potvrđena",
};

export const SUPPLIER_TAB_FALLBACK_DATA_SOURCE: Record<SupplierTab, string> = {
  overview: "Analitika maloprodajne prodaje po dobavljačima",
  scorecard: "Materijalizovani prikaz skorkarte dobavljača",
  assortment: "Analitika asortimana (prodaja posle nivelacije)",
};

export const SUPPLIER_TAB_PENDING_TRUST_HEADLINE = "Pouzdanost se učitava za aktivni tab";
export const SUPPLIER_TAB_PENDING_TRUST_DESCRIPTION =
  "Sačekaj odgovor aktivnog taba pre zaključka o kvalitetu podataka ili preporuci.";
export const SUPPLIER_TAB_PENDING_DATA_QUALITY_LABEL = "Učitavanje pouzdanosti prikaza";
export const SUPPLIER_TAB_PENDING_DATASET_LABEL = "Učitava se za aktivni tab";

export function isSupplierTrustPayloadPending(payload: SupplierTrustHeaderPayload | null): boolean {
  return payload == null;
}

export function resolveSupplierTabFallbackDataSource(tab: SupplierTab): string {
  return SUPPLIER_TAB_FALLBACK_DATA_SOURCE[tab];
}

export function normalizeSupplierChildDataQualityStatus(
  status: string | null | undefined,
): "good" | "warning" | "critical" | "insufficient_data" | null {
  if (typeof status !== "string" || !status.trim()) return null;
  const normalized = status.trim().toLowerCase();
  if (normalized === "error") return "critical";
  if (normalized === "good" || normalized === "warning" || normalized === "critical" || normalized === "insufficient_data") {
    return normalized;
  }
  return null;
}

export function resolveSupplierConsolidatedTrustMode(tab: SupplierTab): "recommendation" | "signal" {
  return tab === "overview" ? "recommendation" : "signal";
}

export function resolveSupplierConsolidatedContextToneClass(
  payload: SupplierTrustHeaderPayload | null,
): "critical" | "warning" | "info" | "neutral" {
  if (payload == null) return "neutral";

  const normalizedStatus = normalizeSupplierChildDataQualityStatus(payload.dataQualityStatus);
  if (normalizedStatus === "critical") return "critical";

  if (
    payload.usedFallback
    || normalizedStatus === "warning"
    || normalizedStatus === "insufficient_data"
    || (payload.recommendationAllowed !== true && normalizedStatus != null)
  ) {
    return "warning";
  }

  return "info";
}

export function resolveSupplierConsolidatedTrustStatusLabel(
  payload: SupplierTrustHeaderPayload | null,
): string {
  if (payload == null) return SUPPLIER_TAB_PENDING_DATA_QUALITY_LABEL;

  const normalizedStatus = normalizeSupplierChildDataQualityStatus(payload.dataQualityStatus);
  if (normalizedStatus) {
    return SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS[normalizedStatus] ?? SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS.unknown;
  }

  return SUPPLIER_CONSOLIDATED_DATA_QUALITY_LABELS.unknown;
}

export function resolveSupplierConsolidatedTrustHeadline(
  tab: SupplierTab,
  payload: SupplierTrustHeaderPayload | null,
): string {
  if (payload == null) return SUPPLIER_TAB_PENDING_TRUST_HEADLINE;

  if (payload.usedFallback) return "Pomoćni ili suženi skup podataka je aktivan";

  if (tab === "overview") {
    return payload.recommendationAllowed === true
      ? "Pregled je glavni izvor preporuke"
      : "Pregled traži proveru pre konačne odluke";
  }

  if (tab === "scorecard") return "Skorkarta je pomoćni signal";

  return "Asortiman je objašnjenje i detaljna razrada";
}

export function resolveSupplierConsolidatedTrustDescription(
  tab: SupplierTab,
  payload: SupplierTrustHeaderPayload | null,
  recommendationNoteText: string | null,
  fallbackReasonText: string | null,
): string {
  if (payload == null) return SUPPLIER_TAB_PENDING_TRUST_DESCRIPTION;

  if (payload.usedFallback && fallbackReasonText) return fallbackReasonText;
  if (recommendationNoteText) return recommendationNoteText;

  if (tab === "scorecard") {
    return "Poređenje dobavljača čitaj uz finalnu preporuku iz taba Pregled.";
  }

  if (tab === "assortment") {
    return "Koristi ovaj prikaz da razumeš uzrok rezultata, ne kao samostalnu finalnu preporuku.";
  }

  return "Skorkarta i asortiman služe da potvrde ili objasne ono što vidiš u pregledu.";
}

export function resolveSupplierConsolidatedDatasetLabel(payload: SupplierTrustHeaderPayload | null): string {
  if (payload == null) return SUPPLIER_TAB_PENDING_DATASET_LABEL;

  const effectiveDataset = typeof payload.effectiveDataset === "string" ? payload.effectiveDataset.trim() : "";
  const requestedDataset = typeof payload.requestedDataset === "string" ? payload.requestedDataset.trim() : "";
  return effectiveDataset || requestedDataset || "Aktivni skup podataka nije posebno označen";
}

export function resolveSupplierConsolidatedRecommendationNote(
  tab: SupplierTab,
  payload: SupplierTrustHeaderPayload | null,
  recommendationNoteText: string | null,
): string | undefined {
  if (payload == null) return "Pouzdanost aktivnog taba se učitava; preporuka i kvalitet još nisu zaključeni.";

  if (recommendationNoteText) return recommendationNoteText;

  if (tab === "scorecard") {
    return payload.recommendationAllowed === true
      ? "Skorkarta je signalni sloj; konačna poslovna preporuka ostaje u tabu Pregled."
      : "Ovo je analitički signal. Konačna preporuka je u tabu Pregled.";
  }

  if (tab === "assortment") {
    return "Asortiman objašnjava strukturu prometa; konačna preporuka je u tabu Pregled.";
  }

  return "Pregled je konačna preporuka; asortiman i skorkarta su signalni slojevi.";
}
