import type { SupplierTabBasis } from "../types/analytics";

export type SupplierTabBasisRow = {
  key: string;
  label: string;
  value: string;
};

const UNDESCRIBED_RULE = "Pravilo nije opisano u ovoj verziji ekrana.";

const ATTRIBUTION_LABELS: Record<string, string> = {
  sale_time_supplier: "Dobavljač u trenutku prodaje.",
  markdown_event_supplier_with_sale_time_returns:
    "Dobavljač iz nivelacije (inače trenutni dobavljač artikla); povraćaji po dobavljaču u trenutku prodaje.",
  markdown_event_supplier: "Dobavljač iz nivelacije (inače trenutni dobavljač artikla).",
  sale_time_shoe_type: "Tip obuće iz trenutka prodaje.",
  current_article_color: "Boja prema trenutnom artiklu.",
};

const COST_LABELS: Record<string, string> = {
  sale_line_then_snapshot_then_current_article_cost:
    "Nabavna cena sa stavke prodaje, zatim zamrznuti snimak, zatim trenutna nabavna cena artikla.",
  sale_line_then_current_article_cost: "Nabavna cena sa stavke prodaje, zatim trenutna nabavna cena artikla.",
  current_article_cost: "Trenutna nabavna cena artikla (ne istorijska).",
};

const RECEIPT_LABELS: Record<string, string> = {
  retail_receipts_excluding_dug_korekcija: "Maloprodajni računi, bez DUG i KOREKCIJA dokumenata.",
};

const COHORT_LABELS: Record<string, string> = {
  all_sales_in_period: "Sva prodaja u periodu.",
  first_markdown_per_article: "Artikli prema prvom sniženju.",
  latest_price_event_per_article_including_increases:
    "Poslednja promena cene po artiklu, uključujući poskupljenja.",
  articles_with_nivelacija_event_and_sales_in_period: "Artikli sa događajem nivelacije i prodajom u izabranom periodu.",
};

const EFFECT_LABELS: Record<string, string> = {
  equal_duration_observed_pre_post_revenue_and_units_change_pct:
    "Opisna promena prihoda i količine u jednako dugim susednim prozorima unutar izabranog perioda (najviše 30 dana po strani); ne dokazuje uzročni efekat nivelacije.",
  fixed_30d_pre_post_revenue_and_units_pct:
    "Procentualna razlika prihoda i količine kroz fiksne prozore od 30 dana pre i posle događaja.",
};

const EVENT_SELECTION_LABELS: Record<string, string> = {
  latest_nivelacija_before_period_end_per_article:
    "Poslednja nivelacija pre kraja perioda; ako je pre početka perioda, signal nema uporedivu pre-bazu.",
  first_markdown_per_article: "Prvo sniženje po artiklu.",
  latest_price_event_per_article: "Poslednja promena cene po artiklu.",
};

const PERIOD_LABELS: Record<string, string> = {
  sale_date_in_period: "Datum prodaje je u izabranom periodu.",
  all_time_markdowns_with_30d_pre_post: "Sva sniženja do danas; prodaja 30 dana pre i 30 dana posle sniženja.",
  first_markdown_in_rolling_window_with_30d_pre_post:
    "Prvo sniženje u kliznom prozoru; prodaja 30 dana pre i 30 dana posle sniženja.",
  price_event_date_in_period_with_30d_pre_post:
    "Datum promene cene je u periodu; prodaja 30 dana pre i posle može izaći van perioda.",
};

const STORE_LABELS: Record<string, string> = {
  receipt_store_with_chain_wide_markdowns: "Objekat sa računa; nivelacije bez objekta važe za ceo lanac.",
  store_filter_limits_sales_evidence: "Izbor objekta sužava prodajne dokaze na taj objekat.",
  store_filter_applies_to_events_and_sales:
    "Izbor objekta filtrira i nivelacije i prodaju; nivelacije bez objekta se tada ne prikazuju.",
};

const UNKNOWN_SUPPLIER_LABELS: Record<string, string> = {
  single_unknown_bucket: "Svi nepoznati dobavljači su spojeni u jedan red „Nepoznato”.",
  unresolved_supplier_not_collapsed: "Nerazrešeni dobavljači se ne spajaju u jedan red.",
  unknown_shoe_type_bucket: "Nepoznati tipovi obuće imaju poseban red.",
  unknown_color_bucket: "Nepoznate boje imaju poseban red.",
};

const TIMEZONE_LABELS: Record<string, string> = {
  UTC: "Kalendarski dan po UTC vremenu.",
};

function describe(map: Record<string, string>, code: string | null | undefined): string {
  if (typeof code !== "string") return UNDESCRIBED_RULE;
  return map[code.trim()] ?? UNDESCRIBED_RULE;
}

/** Maps the backend-owned counting basis to user-facing rows; never exposes raw codes. */
export function buildSupplierTabBasisRows(basis: SupplierTabBasis | null | undefined): SupplierTabBasisRow[] | null {
  if (!basis) return null;

  const asOfDate = typeof basis.asOfDate === "string" && basis.asOfDate.trim().length > 0
    ? basis.asOfDate.trim()
    : null;
  const timezoneText = describe(TIMEZONE_LABELS, basis.timezone);

  return [
    { key: "supplierAttribution", label: "Dobavljač", value: describe(ATTRIBUTION_LABELS, basis.supplierAttribution) },
    { key: "costBasis", label: "Nabavna cena", value: describe(COST_LABELS, basis.costBasis) },
    { key: "receiptPopulation", label: "Računi", value: describe(RECEIPT_LABELS, basis.receiptPopulation) },
    { key: "cohort", label: "Skup artikala", value: describe(COHORT_LABELS, basis.cohort) },
    { key: "periodSemantics", label: "Period", value: describe(PERIOD_LABELS, basis.periodSemantics) },
    { key: "effectMetric", label: "Efekat pre/posle", value: describe(EFFECT_LABELS, basis.effectMetric) },
    { key: "eventSelection", label: "Događaj", value: describe(EVENT_SELECTION_LABELS, basis.eventSelection) },
    { key: "storeScope", label: "Objekat", value: describe(STORE_LABELS, basis.storeScope) },
    { key: "unknownSupplierPolicy", label: "Nepoznat dobavljač", value: describe(UNKNOWN_SUPPLIER_LABELS, basis.unknownSupplierPolicy) },
    {
      key: "asOf",
      label: "Stanje na dan",
      value: asOfDate ? `${asOfDate} — ${timezoneText}` : timezoneText,
    },
  ];
}
