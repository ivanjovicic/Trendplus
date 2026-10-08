export interface UnappliedFilterDraftChip {
  key: "draft-status";
  label: "Filteri";
  value: "Nije primenjeno";
  tone: "warning";
}

export function getUnappliedFilterDraftChip<T extends object>(draft: T, active: T): UnappliedFilterDraftChip | null {
  const differs = (Object.keys(active) as Array<keyof T>).some((key) => draft[key] !== active[key]);
  return differs
    ? { key: "draft-status", label: "Filteri", value: "Nije primenjeno", tone: "warning" }
    : null;
}
