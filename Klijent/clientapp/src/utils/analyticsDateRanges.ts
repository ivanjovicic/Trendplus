export function toUtcDateOnlyExclusive(dateOnly: string): string {
  const parsed = new Date(`${dateOnly}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) return `${dateOnly}T00:00:00Z`;

  parsed.setUTCDate(parsed.getUTCDate() + 1);
  return parsed.toISOString();
}

/** Convert an exclusive UTC endpoint back to the last included calendar date for display. */
export function toInclusiveCalendarDate(exclusiveUtc: string | null | undefined): string | null {
  const value = exclusiveUtc?.trim() ?? "";
  if (!value) return null;
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  return new Date(parsed.getTime() - 1).toISOString().slice(0, 10);
}
