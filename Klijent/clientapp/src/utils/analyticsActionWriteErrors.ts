const FORBIDDEN_ACTION_WRITE_MESSAGE =
  "Nemate dozvolu za izmenu akcija. Preporuke ostaju dostupne za pregled.";

function readErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  if (typeof error === "string") {
    return error;
  }

  if (error && typeof error === "object") {
    const candidate = error as { message?: unknown; detail?: unknown; title?: unknown };
    if (typeof candidate.message === "string") return candidate.message;
    if (typeof candidate.detail === "string") return candidate.detail;
    if (typeof candidate.title === "string") return candidate.title;
  }

  return "";
}

function readErrorStatus(error: unknown): number | null {
  if (!error || typeof error !== "object") return null;
  const candidate = error as { status?: unknown; statusCode?: unknown };
  const status = typeof candidate.status === "number" ? candidate.status : typeof candidate.statusCode === "number" ? candidate.statusCode : null;
  return status != null && Number.isFinite(status) ? status : null;
}

export function isAnalyticsActionWriteForbidden(error: unknown): boolean {
  const status = readErrorStatus(error);
  if (status === 401 || status === 403) return true;

  const message = readErrorMessage(error).toLowerCase();
  return (
    message.includes("unauthorized")
    || message.includes("forbidden")
    || message.includes("not authorized")
    || message.includes("nemate dozvolu")
    || message.includes("nije dozvoljeno")
  );
}

export function getAnalyticsActionWriteErrorMessage(error: unknown): string {
  if (isAnalyticsActionWriteForbidden(error)) {
    return FORBIDDEN_ACTION_WRITE_MESSAGE;
  }

  const message = readErrorMessage(error).trim();
  return message || "Izmena akcija nije uspela.";
}

