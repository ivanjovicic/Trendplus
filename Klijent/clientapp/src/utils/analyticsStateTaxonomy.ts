import type { AnalyticsResponseMeta } from "../types/analytics";

export type AnalyticsStateKind = "empty" | "error" | "loading";
export type AnalyticsStateTone = "neutral" | "warning" | "info" | "error";

export interface AnalyticsStateAction {
  label: string;
  href?: string;
}

export interface AnalyticsStateDefinition {
  kind: AnalyticsStateKind;
  tone: AnalyticsStateTone;
  title: string;
  message: string;
  action?: AnalyticsStateAction;
  retryable?: boolean;
}

const state = (
  kind: AnalyticsStateKind,
  tone: AnalyticsStateTone,
  title: string,
  message: string,
  action?: AnalyticsStateAction,
  retryable = false,
): AnalyticsStateDefinition => ({ kind, tone, title, message, action, retryable });

export const ANALYTICS_STATE_TAXONOMY: Readonly<Record<string, AnalyticsStateDefinition>> = {
  empty_no_data: state("empty", "neutral", "Nema podataka za izabrani period.", "Sistem nije pronašao zapise za potvrđeni period.", { label: "Proširi period" }),
  empty_filtered_out: state("empty", "neutral", "Nema rezultata za filtere.", "Promenite filtere ili proširite period.", { label: "Poništi filtere" }),
  insufficient_data: state("empty", "warning", "Nema dovoljno podataka za pouzdanu analizu.", "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak.", { label: "Kvalitet podataka", href: "/analytics/data-quality" }),
  beyond_source_horizon: state("empty", "warning", "Podaci postoje do dostupnog horizonta.", "Izabrani period prevazilazi period koji potvrđuje izvor.", { label: "Prikaži dostupni period" }),
  source_dimension_not_populated: state("empty", "info", "Dimenzija nije popunjena u izvoru.", "Izvor ne sadrži ovu dimenziju za pouzdan prikaz.", { label: "Kvalitet podataka", href: "/analytics/data-quality" }),
  source_not_ready: state("error", "error", "Izvor nije spreman na serveru.", "Podaci trenutno nisu dostupni. Proverite status osvežavanja.", { label: "Status osvežavanja", href: "/admin/configuration?panel=workers" }),
  blocked_by_readiness: state("empty", "warning", "Preporuka je blokirana.", "Proverite navedene uslove pre nego što postupite po preporuci.", { label: "Kvalitet podataka", href: "/analytics/data-quality" }),
  partial: state("empty", "warning", "Prikaz je delimičan.", "Neki podaci nedostaju; rezultate tumačite uz oprez.", { label: "Ponovo učitaj" }, true),
  suppressed: state("empty", "info", "Neki kandidati nisu prikazani.", "Dostupni kandidati su izostavljeni prema pravilima pouzdanosti.", { label: "Detalji" }),
  error_retryable: state("error", "error", "Analitika trenutno nije dostupna.", "Podaci trenutno nisu dostupni. Pokušajte ponovo.", { label: "Pokušaj ponovo" }, true),
  backend_unreachable: state("error", "error", "Server trenutno nije dostupan.", "Moguće je da se server budi. Pokušajte ponovo za koji trenutak.", { label: "Pokušaj ponovo" }, true),
  schema_mismatch: state("error", "error", "Server je vratio neočekivan format podataka.", "Pokušajte ponovo. Ako se problem nastavi, prosledite ID provere podršci.", { label: "Pokušaj ponovo" }, true),
  loading_slow: state("loading", "neutral", "Učitavanje traje duže nego obično.", "Moguće je da se server budi. Možete pokušati ponovo ili otkazati učitavanje.", { label: "Pokušaj ponovo" }, true),
  error_permission: state("error", "error", "Nemate pristup ovom prikazu.", "Proverite pristup sa administratorom."),
  unknown_code: state("error", "neutral", "Prikaz nije dostupan.", "Sačuvajte tehnički kod iz detalja i kontaktirajte podršku."),
};

const CODE_ALIASES: Readonly<Record<string, string>> = {
  no_data: "empty_no_data",
  no_data_in_period: "empty_no_data",
  no_sales_in_period: "empty_no_data",
  no_supplier_sales: "empty_no_data",
  no_pulse_items: "empty_no_data",
  no_open_issues: "empty_no_data",
  no_top_offenders: "empty_no_data",
  no_import: "empty_no_data",
  no_intake_evidence: "insufficient_data",
  no_rows_for_period: "empty_no_data",
  no_rows: "empty_no_data",
  filtered_out: "empty_filtered_out",
  insufficient_history: "insufficient_data",
  source_dimension_not_populated: "source_dimension_not_populated",
  partial_payload: "partial",
  schema_fallback: "partial",
  stale_cache: "partial",
  stale_cache_warning: "partial",
  board_partial: "partial",
  pulse_partial: "partial",
  missing_object: "schema_mismatch",
  missing_schema: "schema_mismatch",
  contract_missing: "schema_mismatch",
  vendor_sales_nivelacija_contract_missing: "schema_mismatch",
  backend_unavailable: "backend_unreachable",
  analytics_db_unavailable: "source_not_ready",
  analytics_database_unavailable: "source_not_ready",
  analytics_unavailable: "source_not_ready",
  inventory_status_error: "error_retryable",
  inventory_cached_list_db_error: "error_retryable",
  inventory_cached_list_error: "error_retryable",
  inventory_cached_insights_db_error: "error_retryable",
  inventory_cached_insights_error: "error_retryable",
  supplier_decision_unavailable: "error_retryable",
  source_error: "error_retryable",
  missing_contract: "schema_mismatch",
};

export type AnalyticsEmptyVariant = "no_data" | "insufficient_data" | "filtered_out";

const VARIANT_CODES: Readonly<Record<AnalyticsEmptyVariant, string>> = {
  no_data: "empty_no_data",
  insufficient_data: "insufficient_data",
  filtered_out: "empty_filtered_out",
};

export interface ResolvedAnalyticsState {
  code: string | null;
  rawCode: string | null;
  definition: AnalyticsStateDefinition | null;
}

function normalizeCode(code: string): string {
  return code.trim().toLocaleLowerCase().replace(/[\s-]+/g, "_");
}

export function isAnalyticsReasonCode(value?: string | null): boolean {
  return Boolean(value?.trim() && /^[a-z][a-z0-9_.:-]*$/i.test(value.trim()));
}

function stateCodeFromMeta(meta?: AnalyticsResponseMeta | null): string | null {
  if (!meta) return null;
  if (meta.errorCode?.trim()) return meta.errorCode;
  if (meta.success === false) return "error_retryable";
  if (meta.warningCode?.trim() && (!meta.isPartial || isKnownAnalyticsStateCode(meta.warningCode))) return meta.warningCode;
  if (meta.isPartial) return "partial";
  if (isAnalyticsReasonCode(meta.emptyReason)) return meta.emptyReason ?? null;
  if (meta.success === true && meta.dataQualityStatus === "insufficient_data") return "insufficient_data";
  return null;
}

export function resolveAnalyticsState(
  code?: string | null,
  meta?: AnalyticsResponseMeta | null,
  variant?: AnalyticsEmptyVariant,
): ResolvedAnalyticsState {
  const providedCode = code?.trim() ? code : stateCodeFromMeta(meta);
  const effectiveCode = providedCode ?? (variant ? VARIANT_CODES[variant] : null);
  if (!effectiveCode) return { code: null, rawCode: null, definition: null };

  const unknownPartialWarning = !code?.trim()
    && meta?.success === true
    && meta?.isPartial === true
    && Boolean(meta.warningCode?.trim())
    && !isKnownAnalyticsStateCode(meta.warningCode ?? "");
  const normalized = normalizeCode(effectiveCode);
  const canonical = CODE_ALIASES[normalized] ?? normalized;
  const knownDefinition = ANALYTICS_STATE_TAXONOMY[canonical];
  const definition = knownDefinition ?? ANALYTICS_STATE_TAXONOMY.unknown_code;

  return {
    code: knownDefinition ? canonical : "unknown_code",
    rawCode: knownDefinition ? (unknownPartialWarning ? meta?.warningCode?.trim() ?? null : null) : effectiveCode.trim(),
    definition,
  };
}

export function getAnalyticsFailureCode(
  failure: unknown,
  meta?: AnalyticsResponseMeta | null,
): string {
  if (meta?.errorCode?.trim()) return meta.errorCode;

  if (failure && typeof failure === "object") {
    const candidate = failure as { name?: unknown; message?: unknown; errorCode?: unknown };
    if (typeof candidate.errorCode === "string" && candidate.errorCode.trim()) return candidate.errorCode;
    if (candidate.name === "AnalyticsResponseValidationError") return "schema_mismatch";
    if (candidate.name === "TypeError" && typeof candidate.message === "string"
      && /^(?:typeerror:\s*)?failed to fetch\.?$/i.test(candidate.message.trim())) {
      return "backend_unreachable";
    }
  }

  return "error_retryable";
}

export function isKnownAnalyticsStateCode(code: string): boolean {
  const normalized = normalizeCode(code);
  const canonical = CODE_ALIASES[normalized] ?? normalized;
  return Object.prototype.hasOwnProperty.call(ANALYTICS_STATE_TAXONOMY, canonical);
}
