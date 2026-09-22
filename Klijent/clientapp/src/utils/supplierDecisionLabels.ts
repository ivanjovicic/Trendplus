const DATASET_LABELS: Record<string, string> = {
  "30d": "poslednjih 30 dana",
  "90d": "poslednjih 90 dana",
  "180d": "poslednjih 180 dana",
  "custom_range": "prilagođeni period",
  "all_time": "celokupna istorija",
  "all_history": "celokupna istorija",
  "window_90d": "poslednjih 90 dana",
  "window_180d": "poslednjih 180 dana",
};

function normalize(value: string | null | undefined): string | null {
  const normalized = typeof value === "string" ? value.trim().toLowerCase() : "";
  return normalized || null;
}

export function supplierDecisionDatasetLabel(value: string | null | undefined): string | null {
  const normalized = normalize(value);
  if (!normalized) return null;

  const mapped = DATASET_LABELS[normalized];
  if (mapped) return mapped;
  if (/^mv_supplier_decision_score_cache(?:_\d+d)?$/.test(normalized)) return "keš signala odluke dobavljača";
  if (/^supplier_decision_score_cache(?:_\d+d)?$/.test(normalized)) return "keš signala odluke dobavljača";
  if (/^dataset[_-]/.test(normalized) || normalized.includes("supplier_decision")) return "skup podataka odluke dobavljača";

  return value!.trim();
}

export function supplierDecisionProvenanceLabel(value: string | null | undefined): string | null {
  const normalized = normalize(value);
  if (!normalized) return null;
  if (/^mv_supplier_decision_score_cache(?:_\d+d)?$/.test(normalized)) return "keš signala odluke dobavljača";
  if (/^supplier_decision_score_cache(?:_\d+d)?$/.test(normalized)) return "keš signala odluke dobavljača";
  if (normalized.includes("supplier_decision")) return "serverski izvor signala dobavljača";
  return value!.trim();
}

export function supplierDecisionFreshnessLabel(value: string | null | undefined): string | null {
  const normalized = normalize(value);
  if (!normalized) return null;
  if (normalized === "fresh") return "Sveže";
  if (normalized === "stale") return "Zastarelo";
  if (normalized === "critical") return "Kritično";
  if (normalized === "unknown") return "Nije poznato";
  return value!.trim();
}

export function supplierDecisionReasonText(value: string | null | undefined): string | null {
  const text = typeof value === "string" ? value.trim() : "";
  if (!text) return null;
  if (/^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$/i.test(text)) return null;

  return text
    .replace(/\bfallback dataset\b/gi, "pomoćni skup podataka")
    .replace(/\bfallback\b/gi, "pomoćni skup")
    .replace(/\bdataset\b/gi, "skup podataka")
    .replace(/\bscorecard\b/gi, "skorkarta")
    .replace(/\bdata quality\b/gi, "kvalitet podataka")
    .replace(/\bstock[- ]risk signal\b/gi, "signal rizika zaliha")
    .replace(/\bstock[- ]risk\b/gi, "rizik zaliha")
    .replace(/\bOOS false negative\b/gi, "signal nedostatka zaliha")
    .replace(/\bmarkdown dependency\b/gi, "zavisnost od sniženja")
    .replace(/\bbackend\b/gi, "server")
    .replace(/\bsnapshot\b/gi, "sažetak");
}
