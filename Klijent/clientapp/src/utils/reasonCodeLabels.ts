/**
 * Short Serbian chip labels for backend reason codes. Backend codes stay the contract;
 * the UI only formats them. Unknown codes never surface as raw English snake_case.
 */
const REASON_CODE_CHIP_LABELS: Record<string, string> = {
  replenish_needed: "Potrebna dopuna",
  replenishment_minimum_gap: "Ispod minimuma",
  sales_velocity_observed: "Prodaja zabeležena",
  configured_minimum_no_sales_in_source_window: "Minimum bez prodaje u periodu",
  slow_stock: "Spora roba",
  opening_stock_unavailable: "Početna zaliha nije poznata",
  stock_below_minimum: "Ispod minimuma",
  stock_cover_insufficient_data: "Pokrivenost: nedovoljno podataka",
  stock_cover_out_of_stock_risk: "Rizik nestanka zalihe",
  stock_cover_no_velocity: "Nema brzine prodaje",
  sell_through_insufficient_data: "Stopa prodaje: nedovoljno podataka",
  sell_through_insufficient_denominator_data: "Stopa prodaje: nedovoljna osnova",
  sell_through_denominator_zero: "Stopa prodaje: osnova je nula",
  sell_through_invalid_denominator_input: "Stopa prodaje: neispravna osnova",
  store_identity_unknown: "Objekat nije poznat",
  missing_store_identity: "Objekat nije poznat",
  non_retail_store: "Objekat nije maloprodaja",
  non_footwear: "Artikal nije obuća",
  footwear_type_unknown: "Vrsta obuće nije poznata",
  outcome_result_not_measured: "Ishod nije izmeren",
  missing_cost: "Nedostaje nabavna cena",
  missing_supplier: "Nedostaje dobavljač",
  insufficient_signal: "Nedovoljan signal",
  insufficient_data: "Nedovoljno podataka",
  freshness: "Zastareli podaci",
  small_measured_sample: "Mali uzorak ishoda",
  high_velocity: "Brza prodaja",
  low_stock: "Niska zaliha",
  poor_margin: "Slaba marža",
  stale_stock: "Dugo bez prodaje",
  high_stock_risk: "Rizik viška zalihe",
  data_quality_blocker: "Kvalitet podataka blokira",
  data_quality_critical: "Kritičan kvalitet podataka",
};

export const UNKNOWN_REASON_CODE_LABEL = "Dodatni razlog";

export function hasReasonCodeChipLabel(code: string | null | undefined): boolean {
  return (code ?? "").trim().toLowerCase() in REASON_CODE_CHIP_LABELS;
}

export function reasonCodeChipLabel(code: string | null | undefined): string {
  const normalized = (code ?? "").trim().toLowerCase();
  return REASON_CODE_CHIP_LABELS[normalized] ?? UNKNOWN_REASON_CODE_LABEL;
}
