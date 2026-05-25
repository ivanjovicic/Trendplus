export type AnalyticsMetricKey =
  | "totalRevenue"
  | "marginContribution"
  | "soldUnits"
  | "stockAtRisk"
  | "slowStock"
  | "lostSales"
  | "dataReadiness"
  | "revenueWithoutCost"
  | "revenueUnknownSupplier"
  | "totalInventoryValue"
  | "stockUnits"
  | "lowStockCount"
  | "replenishCount"
  | "boostCount"
  | "markdownCount"
  | "doNotOrderCount"
  | "fixDataCount"
  | "topSupplierRevenueShare"
  | "fullPriceShareChange"
  | "activeSkuShare"
  | "inventoryHealthScore"
  | "skuCount"
  | "avgUnitsPerSku"
  | "onHandUnits"
  | "inventoryTotalValue"
  | "revenue"
  | "quantity"
  | "stockRiskCapital"
  | "dataReadinessScore"
  | "missingCostRevenueShare"
  | "unknownSupplierRevenueShare"
  | "lostSalesEstimate"
  | "slowStockCapital";

export type AnalyticsMetricDefinition = {
  title: string;
  formula: string;
  source: string;
  description: string;
  qualityNote?: string;
  label: string;
  businessMeaning: string;
  formulaText: string;
  inputs: string[];
  dataSource: string[];
  caveats: string[];
  blockedWhen: string[];
  relatedDataQualityChecks: string[];
};

type MetricSeed = {
  title: string;
  formula: string;
  source: string;
  description: string;
  qualityNote?: string;
  inputs?: string[];
  caveats?: string[];
  blockedWhen?: string[];
  relatedDataQualityChecks?: string[];
};

function defineMetric(seed: MetricSeed): AnalyticsMetricDefinition {
  return {
    title: seed.title,
    formula: seed.formula,
    source: seed.source,
    description: seed.description,
    qualityNote: seed.qualityNote,
    label: seed.title,
    businessMeaning: seed.description,
    formulaText: seed.formula,
    inputs: seed.inputs ?? [],
    dataSource: [seed.source],
    caveats: seed.caveats ?? (seed.qualityNote ? [seed.qualityNote] : []),
    blockedWhen: seed.blockedWhen ?? [],
    relatedDataQualityChecks: seed.relatedDataQualityChecks ?? [],
  };
}

const canonicalMetricDefinitions = {
  totalRevenue: defineMetric({
    title: "Prihod",
    formula: "Suma polja ukupna_cena kroz sve prodajne stavke u izabranom periodu i filteru.",
    source: "MV: sales_facts_mv",
    description: "Ukupna prodajna vrednost u izabranom periodu i filteru.",
    qualityNote: "Ako je meta partial/stale ili success=false, broj nije finalni poslovni rezultat.",
    inputs: ["ukupna_cena", "period", "store/supplier filter"],
    relatedDataQualityChecks: ["Svežina refresh-a", "Pokrivenost prodajnih stavki"],
  }),
  marginContribution: defineMetric({
    title: "Maržni doprinos",
    formula: "Suma izraza (ukupna_cena - nabavna_cena) za stavke sa dostupnim troškom.",
    source: "MV: sales_facts_mv",
    description: "Bruto doprinos pre operativnih troškova; nije neto profit.",
    qualityNote: "Visok udeo prometa bez nabavne cene smanjuje pouzdanost metrike.",
    inputs: ["ukupna_cena", "nabavna_cena", "količina"],
    relatedDataQualityChecks: ["Promet bez nabavne cene", "Fallback nabavna cena"],
  }),
  soldUnits: defineMetric({
    title: "Prodate jedinice",
    formula: "Suma polja količina kroz prodajne stavke u periodu i filteru.",
    source: "MV: sales_facts_mv",
    description: "Ukupan broj prodatih komada; nije broj računa.",
    inputs: ["količina", "period", "filteri"],
  }),
  stockAtRisk: defineMetric({
    title: "Lager u riziku",
    formula: "Suma izraza (količina * nabavna_cena) za artikle sa signalom niske rotacije ili OOS rizika.",
    source: "MV: inventory_mv",
    description: "Procenjeni kapital vezan u zalihama sa povišenim operativnim rizikom.",
    qualityNote: "Nije knjigovodstvena valuacija; zavisi od kvaliteta cost i risk signala.",
    relatedDataQualityChecks: ["Nabavne cene", "Svežina inventory snapshot-a"],
  }),
  slowStock: defineMetric({
    title: "Kapital u sporoj zalihi",
    formula: "Suma izraza (količina * nabavna_cena) za artikle označene kao slow stock.",
    source: "MV: inventory_mv",
    description: "Kapital zaključan u spororotirajućoj robi.",
    qualityNote: "Kod fallback troška metrika je indikativna.",
  }),
  lostSales: defineMetric({
    title: "Procena izgubljene prodaje",
    formula: "Suma izraza ((procenjena_potražnja - realizovana_prodaja) * cena) uz OOS signal.",
    source: "MV: product_decision_snapshot",
    description: "Modelirana procena propuštenog prihoda zbog nedostupnosti artikla.",
    qualityNote: "Modelski signal; nije knjižena prodaja.",
    relatedDataQualityChecks: ["Istorija prodaje", "OOS signal", "Svežina refresh-a"],
  }),
  dataReadiness: defineMetric({
    title: "Spremnost podataka",
    formula: "Ponderisani skor kvaliteta signala i kritičnih nedostajućih polja.",
    source: "Data quality snapshot + analytics refresh status",
    description: "Sažetak koliko su ulazni podaci spremni za pouzdanu analitiku i preporuke.",
    qualityNote: "Dobar skor ne isključuje lokalne probleme na pojedinačnim KPI-jevima.",
    relatedDataQualityChecks: ["Bez dobavljača", "Bez nabavne cene", "Insufficient signal", "Svežina refresh-a"],
  }),
  revenueWithoutCost: defineMetric({
    title: "Promet bez nabavne cene",
    formula: "(prihod bez troška / ukupan prihod) * 100.",
    source: "MV: analytics_data_quality_history",
    description: "Udeo prihoda za koji ne postoji pouzdan trošak.",
    qualityNote: "Direktno utiče na pouzdanost maržnih KPI-jeva.",
  }),
  revenueUnknownSupplier: defineMetric({
    title: "Promet nepoznatog dobavljača",
    formula: "(prihod bez mapiranog dobavljača / ukupan prihod) * 100.",
    source: "MV: analytics_data_quality_history",
    description: "Udeo prihoda koji nije moguće validno pripisati dobavljaču.",
    qualityNote: "Narušava scorecard i supplier poređenja.",
  }),
  totalInventoryValue: defineMetric({
    title: "Ukupna vrednost zaliha",
    formula: "Suma izraza (količina * nabavna_cena) za pozitivnu zalihu.",
    source: "MV: inventory_mv",
    description: "Procenjena nabavna vrednost raspoložive zalihe.",
    qualityNote: "Ako deo artikala koristi fallback cost, metrika je indikativna.",
  }),
  stockUnits: defineMetric({
    title: "Ukupno na stanju",
    formula: "Suma polja količina za pozitivnu raspoloživu zalihu.",
    source: "MV: inventory_mv",
    description: "Ukupna količina robe na stanju u aktivnom filteru.",
  }),
  lowStockCount: defineMetric({
    title: "Niska zaliha",
    formula: "Broj SKU gde je količina <= minimalni prag.",
    source: "MV: inventory_mv",
    description: "Broj artikala koji su na ili ispod bezbednog nivoa zalihe.",
  }),
  replenishCount: defineMetric({
    title: "Za dopunu",
    formula: "Broj redova sa recommendationStatus = REPLENISH.",
    source: "MV: product_decision_snapshot",
    description: "Broj proizvoda sa signalom da treba dopunu.",
  }),
  boostCount: defineMetric({
    title: "Za pojačanje",
    formula: "Broj redova sa recommendationStatus = BOOST.",
    source: "MV: product_decision_snapshot",
    description: "Broj proizvoda sa jakim pozitivnim signalom.",
  }),
  markdownCount: defineMetric({
    title: "Za sniženje",
    formula: "Broj redova sa recommendationStatus = MARKDOWN.",
    source: "MV: product_decision_snapshot",
    description: "Broj proizvoda sa signalom za sniženje.",
  }),
  doNotOrderCount: defineMetric({
    title: "Ne naručivati",
    formula: "Broj redova sa recommendationStatus = DO_NOT_ORDER.",
    source: "MV: product_decision_snapshot",
    description: "Broj proizvoda za koje sistem ne preporučuje novu narudžbinu.",
  }),
  fixDataCount: defineMetric({
    title: "Proveriti podatke",
    formula: "Broj redova sa recommendationStatus = FIX_DATA.",
    source: "MV: product_decision_snapshot",
    description: "Broj proizvoda gde data quality blokira preporuku.",
  }),
  topSupplierRevenueShare: defineMetric({
    title: "Udeo top 5 dobavljača",
    formula: "(prihod top 5 dobavljača / ukupan scorecard prihod) * 100.",
    source: "MV: supplier_decision_score_cache",
    description: "Meri koncentraciju prihoda na najveće dobavljače.",
  }),
  fullPriceShareChange: defineMetric({
    title: "Promena udela pune cene",
    formula: "Udeo pune cene u tekućem periodu minus udeo pune cene u prethodnom uporedivom periodu.",
    source: "MV: supplier_decision_score_cache",
    description: "Pokazuje smer promene zavisnosti od sniženja.",
  }),
  activeSkuShare: defineMetric({
    title: "Aktivni SKU",
    formula: "(broj SKU sa pozitivnom zalihom / ukupan broj SKU) * 100.",
    source: "MV: inventory_mv",
    description: "Udeo artikala koji nisu bez zaliha.",
  }),
  inventoryHealthScore: defineMetric({
    title: "Stanje fonda",
    formula: "Kompozitni skor iz active SKU, low stock, OOS i rizika rotacije.",
    source: "MV: inventory_mv + inventory alerts",
    description: "Sažeti operativni signal stanja zaliha.",
  }),
  skuCount: defineMetric({
    title: "Ukupno SKU",
    formula: "COUNT DISTINCT sku u aktivnom filteru.",
    source: "MV: inventory_mv",
    description: "Broj jedinstvenih artikala u opsegu filtera.",
  }),
  avgUnitsPerSku: defineMetric({
    title: "Prosečno po SKU",
    formula: "ukupno_na_stanju / ukupno_sku.",
    source: "MV: inventory_mv",
    description: "Srednja količina robe po SKU.",
  }),
};

export const analyticsMetricDefinitions: Record<AnalyticsMetricKey, AnalyticsMetricDefinition> = {
  ...canonicalMetricDefinitions,
  revenue: canonicalMetricDefinitions.totalRevenue,
  quantity: canonicalMetricDefinitions.soldUnits,
  stockRiskCapital: canonicalMetricDefinitions.stockAtRisk,
  dataReadinessScore: canonicalMetricDefinitions.dataReadiness,
  missingCostRevenueShare: canonicalMetricDefinitions.revenueWithoutCost,
  unknownSupplierRevenueShare: canonicalMetricDefinitions.revenueUnknownSupplier,
  inventoryTotalValue: canonicalMetricDefinitions.totalInventoryValue,
  onHandUnits: canonicalMetricDefinitions.stockUnits,
  lostSalesEstimate: canonicalMetricDefinitions.lostSales,
  slowStockCapital: canonicalMetricDefinitions.slowStock,
};

const analyticsMetricAliases: Partial<Record<AnalyticsMetricKey, string[]>> = {
  totalRevenue: ["Prihod", "Ukupan prihod", "Ukupan promet", "Promet"],
  marginContribution: ["Maržni doprinos", "Ukupan maržni doprinos"],
  soldUnits: ["Prodate jedinice", "Jedinice", "Komadi"],
  stockAtRisk: ["Lager u riziku", "Kapital u riziku"],
  slowStock: ["Kapital u sporoj zalihi"],
  lostSales: ["Procena izgubljene prodaje"],
  dataReadiness: ["Spremnost podataka", "Data quality score", "Kvalitet podataka"],
  revenueWithoutCost: ["Promet bez nabavne cene"],
  revenueUnknownSupplier: ["Promet nepoznatog dobavljača"],
  totalInventoryValue: ["Ukupna vrednost zaliha", "Procena vrednosti"],
  stockUnits: ["Ukupno na stanju"],
  lowStockCount: ["Niska zaliha"],
  topSupplierRevenueShare: ["Udeo top 5 dobavljača"],
  fullPriceShareChange: ["Promena udela pune cene"],
};

export function getAnalyticsMetricDefinition(metricKey: AnalyticsMetricKey): AnalyticsMetricDefinition {
  return analyticsMetricDefinitions[metricKey];
}

export function findAnalyticsMetricKeyByLabel(label: string | null | undefined): AnalyticsMetricKey | null {
  if (!label) return null;
  const normalized = label.trim().toLocaleLowerCase("sr-Latn-RS");
  const keys = Object.keys(analyticsMetricDefinitions) as AnalyticsMetricKey[];
  const directMatch = keys.find(
    (key) => analyticsMetricDefinitions[key].label.toLocaleLowerCase("sr-Latn-RS") === normalized
  );
  if (directMatch) return directMatch;

  for (const [key, aliases] of Object.entries(analyticsMetricAliases) as Array<[AnalyticsMetricKey, string[] | undefined]>) {
    if (aliases?.some((alias) => alias.toLocaleLowerCase("sr-Latn-RS") === normalized)) {
      return key;
    }
  }

  return null;
}
