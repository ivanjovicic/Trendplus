import { getSafeAnalyticsErrorMessage } from "./analyticsErrorMessages";

export const NIVELACIJA_ERROR_FALLBACK = "Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.";

const knownNivelacijaErrorMessages: Record<string, string> = {
  vendor_sales_nivelacija_contract_missing: "Pre/post analiza čeka ispravku šeme baze. Sačuvajte kod i ID za podršku.",
  pre_nivelacija_sales_unavailable: "Prodaja za skor trenutno nije dostupna.",
};

export function getNivelacijaBusinessErrorMessage(errorCode: string | null | undefined): string | null {
  return errorCode ? knownNivelacijaErrorMessages[errorCode.trim().toLocaleLowerCase()] ?? null : null;
}

export type NivelacijaErrorDetails = {
  message: string;
  errorCode: string | null;
  correlationId: string | null;
};

export function resolveNivelacijaErrorDetails(
  reason: unknown,
  fallback = NIVELACIJA_ERROR_FALLBACK,
  allowlist?: readonly string[],
): NivelacijaErrorDetails {
  const error = typeof reason === "object" && reason !== null
    ? reason as { message?: unknown; errorCode?: unknown; correlationId?: unknown }
    : null;
  const errorCode = typeof error?.errorCode === "string" ? error.errorCode : null;
  const correlationId = typeof error?.correlationId === "string" ? error.correlationId : null;
  const rawMessage = typeof error?.message === "string" ? error.message : null;
  const mappedMessage = getNivelacijaBusinessErrorMessage(errorCode);

  return {
    message: mappedMessage ?? getSafeAnalyticsErrorMessage(rawMessage, errorCode, fallback, allowlist),
    errorCode,
    correlationId,
  };
}


export function getSupplierOptionLabels<T extends { id: number; naziv: string }>(suppliers: readonly T[]) {
  const counts = new Map<string, number>();
  for (const supplier of suppliers) {
    const key = supplier.naziv.trim().toLocaleLowerCase("sr");
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }

  return [...suppliers]
    .sort((left, right) => left.naziv.localeCompare(right.naziv, "sr") || left.id - right.id)
    .map((supplier) => {
      const name = supplier.naziv.trim() || `Nepoznat dobavljač`;
      const key = supplier.naziv.trim().toLocaleLowerCase("sr");
      return {
        ...supplier,
        displayName: (counts.get(key) ?? 0) > 1 ? `${name} (${supplier.id})` : name,
      };
    });
}
