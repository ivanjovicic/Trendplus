export const ANALYTICS_ERROR_FALLBACK_MESSAGE =
  "Podaci trenutno nisu dostupni. Proverite kvalitet podataka i pokušajte ponovo.";

export const ANALYTICS_EMPTY_ERROR_FALLBACK_MESSAGE =
  "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan.";

const TECHNICAL_ERROR_PATTERNS = [
  /\b(?:system|microsoft|npgsql(?:exception)?|postgres(?:ql)?|sql(?:server|state|exception)?|exception|stack\s*trace)\b/i,
  /\b(?:error|typeerror|rangeerror|referenceerror|syntaxerror|urierror|evalerror|aggregateerror)\b\s*:/i,
  /\b(?:http|status)\s*[:=]?\s*[45]\d{2}\b/i,
  /(?:^|\s)at\s+(?:[\w$]+\.)+[\w$]+\s*\(/i,
  /\b(?:invalidoperation|argumentnull|argumentoutofrange|nullreference)exception\b/i,
  /\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b/,
  /\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b/,
];

export function getSafeAnalyticsErrorMessage(
  message: string | null | undefined,
  errorCode?: string | null,
  fallback = ANALYTICS_ERROR_FALLBACK_MESSAGE,
): string {
  const normalizedMessage = typeof message === "string" ? message.trim() : null;
  if (!normalizedMessage) return fallback;

  const normalizedCode = typeof errorCode === "string" ? errorCode.trim().toLocaleLowerCase() : null;
  if (normalizedCode && normalizedMessage.toLocaleLowerCase().includes(normalizedCode)) {
    return ANALYTICS_ERROR_FALLBACK_MESSAGE;
  }

  if (TECHNICAL_ERROR_PATTERNS.some((pattern) => pattern.test(normalizedMessage))) {
    return ANALYTICS_ERROR_FALLBACK_MESSAGE;
  }

  return normalizedMessage;
}
