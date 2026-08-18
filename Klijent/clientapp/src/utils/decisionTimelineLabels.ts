export function timelineEmptyReasonLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "outside_period") return "Nema događaja u izabranom periodu.";
  if (normalized === "no_measurement") return "Nema merenog ishoda.";
  if (normalized === "no_events") return "Nema događaja za izabrani entitet ili porodicu.";
  return "Istorija odluke je prazna.";
}

export function timelineEventTypeLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "recommendation_issued") return "Preporuka izdata";
  if (normalized === "action_accepted") return "Akcija prihvaćena";
  if (normalized === "action_rejected") return "Akcija odbijena";
  if (normalized === "action_executed") return "Akcija izvršena";
  if (normalized === "outcome_measured") return "Ishod izmeren";
  if (normalized === "outcome_not_measured") return "Ishod nije izmeren";
  return value?.trim() ? value.replaceAll("_", " ") : "Nije dostupno";
}

export function timelineGapReasonLabel(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().toLowerCase();
  if (normalized === "no_acceptance_record") return "Nema zapisa o prihvatanju";
  if (normalized === "no_execution_proof") return "Nema dokaza o izvršenju";
  if (normalized === "no_measurement_evidence") return "Nema merenog dokaza";
  if (normalized === "legacy_partial_history") return "Nepotpun stariji istorijat";
  return value?.trim() ? value.replaceAll("_", " ") : "Nije dostupno";
}
