/** RQ530 — explicit unavailable buying metrics (must stay aligned with SupplierBuyingValueEvidenceContract). */
export const SUPPLIER_BUYING_UNAVAILABLE_METRIC_LINES = [
  "Dani pokrića i sell-through: dostupni su po artiklu u Inventory listi, ali nije potvrđen agregat po dobavljaču.",
  "Bruto stopa povrata: ReturnFact izvor nije spojen sa Supplier agregatom u ovom promptu.",
  "Trend marže i top/bottom artikli po prodaji: zahtevaju zaseban periodizovan Supplier izvor.",
  "PO, rok isporuke i lead-time: nema potvrđenog autoritativnog izvora, zato nisu prikazani.",
] as const;
