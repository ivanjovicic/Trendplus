import { formatDate } from "./analyticsFormatters";

type PeriodLineage = {
  effectivePeriodLabel?: string | null;
  requestedFromUtc?: string | null;
  requestedToUtc?: string | null;
  effectiveFromUtc?: string | null;
  effectiveToUtc?: string | null;
  observedFromUtc?: string | null;
  observedToUtc?: string | null;
};

function formatRange(fromUtc?: string | null, toUtc?: string | null): string | null {
  if (!fromUtc || !toUtc) {
    return null;
  }

  return `${formatDate(fromUtc)} - ${formatDate(toUtc)}`;
}

function normalizeText(value?: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

export function buildPeriodLineageLabel({
  effectivePeriodLabel,
  effectiveFromUtc,
  effectiveToUtc,
  observedFromUtc,
  observedToUtc,
}: PeriodLineage): string | null {
  const effectiveLabel = normalizeText(effectivePeriodLabel);
  const effectiveRange = formatRange(effectiveFromUtc, effectiveToUtc);
  const observedRange = formatRange(observedFromUtc, observedToUtc);
  const parts: string[] = [];

  if (effectiveLabel) {
    parts.push(effectiveLabel);
  } else if (effectiveRange) {
    parts.push(`Efektivni period: ${effectiveRange}`);
  }

  if (effectiveRange && effectiveRange !== effectiveLabel) {
    parts.push(`Efektivni opseg: ${effectiveRange}`);
  }

  if (observedRange && observedRange !== effectiveRange) {
    parts.push(`Posmatrani podaci: ${observedRange}`);
  }

  return parts.length > 0 ? parts.join(" · ") : null;
}

export function resolveLineagePeriod(
  requestedFromUtc?: string | null,
  requestedToUtc?: string | null,
  effectiveFromUtc?: string | null,
  effectiveToUtc?: string | null,
  observedFromUtc?: string | null,
  observedToUtc?: string | null,
): { periodFrom: string | null; periodTo: string | null } {
  if (requestedFromUtc && requestedToUtc) {
    return { periodFrom: requestedFromUtc, periodTo: requestedToUtc };
  }

  if (effectiveFromUtc && effectiveToUtc) {
    return { periodFrom: effectiveFromUtc, periodTo: effectiveToUtc };
  }

  if (observedFromUtc && observedToUtc) {
    return { periodFrom: observedFromUtc, periodTo: observedToUtc };
  }

  return { periodFrom: null, periodTo: null };
}
