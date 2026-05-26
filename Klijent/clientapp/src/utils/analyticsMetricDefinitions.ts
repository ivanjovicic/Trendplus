export type AnalyticsMetricKey =
  | "revenue"
  | "marginContribution"
  | "unitsSold"
  | "stockAtRisk"
  | "stockCoverDays"
  | "lostSalesEstimate"
  | "dataReadinessScore"
  | "missingCostCount"
  | "missingSupplierCount"
  | "sellThrough"
  | "velocity"
  | "confidencePct"
  | "reliabilityPct"
  | "markdownDependency"
  | "slowStockCapital"
  | "outOfStockRisk"
  | "totalRevenue"
  | "soldUnits"
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
  | "quantity"
  | "stockRiskCapital"
  | "blockedRecommendationsCount"
  | "ignoredRowsCount"
  | "grossMarginPct"
  | "inventoryTurnover"
  | "missingCostRevenueShare"
  | "unknownSupplierRevenueShare";

export const canonicalMetricKeys = [
  "revenue",
  "marginContribution",
  "unitsSold",
  "stockAtRisk",
  "stockCoverDays",
  "slowStockCapital",
  "lostSalesEstimate",
  "dataReadinessScore",
  "missingCostCount",
  "missingSupplierCount",
  "sellThrough",
  "velocity",
  "confidencePct",
  "reliabilityPct",
  "markdownDependency",
  "outOfStockRisk",
  "lowStockCount",
  "replenishCount",
  "boostCount",
  "markdownCount",
  "doNotOrderCount",
  "fixDataCount",
  "topSupplierRevenueShare",
  "fullPriceShareChange",
  "activeSkuShare",
  "inventoryHealthScore",
  "skuCount",
  "avgUnitsPerSku",
  "onHandUnits",
  "inventoryTotalValue",
  "revenueWithoutCost",
  "blockedRecommendationsCount",
  "ignoredRowsCount",
  "grossMarginPct",
  "inventoryTurnover",
  "unknownSupplierRevenueShare",
] as const;

type CanonicalMetricKey = (typeof canonicalMetricKeys)[number];

export type AnalyticsMetricDefinition = {
  key: AnalyticsMetricKey;
  label: string;
  shortDescription: string;
  formula: string;
  dataSource: string;
  interpretation: string;
  limitations: string[];
  dataQualityDependencies: string[];
  relatedScreens: string[];
  title: string;
  source: string;
  description: string;
  qualityNote?: string;
  businessMeaning: string;
  formulaText: string;
  inputs: string[];
  caveats: string[];
  blockedWhen: string[];
  relatedDataQualityChecks: string[];
};

type MetricSeed = {
  label: string;
  shortDescription: string;
  formula: string;
  dataSource: string;
  interpretation: string;
  limitations?: string[];
  dataQualityDependencies?: string[];
  relatedScreens?: string[];
  qualityNote?: string;
  inputs?: string[];
  blockedWhen?: string[];
};

function defineMetric(key: AnalyticsMetricKey, seed: MetricSeed): AnalyticsMetricDefinition {
  const limitations = seed.limitations ?? [];
  const dataQualityDependencies = seed.dataQualityDependencies ?? [];
  const qualityNote = seed.qualityNote ?? limitations[0];

  return {
    key,
    label: seed.label,
    shortDescription: seed.shortDescription,
    formula: seed.formula,
    dataSource: seed.dataSource,
    interpretation: seed.interpretation,
    limitations,
    dataQualityDependencies,
    relatedScreens: seed.relatedScreens ?? [],
    title: seed.label,
    source: seed.dataSource,
    description: seed.shortDescription,
    qualityNote,
    businessMeaning: seed.interpretation,
    formulaText: seed.formula,
    inputs: seed.inputs ?? [],
    caveats: limitations,
    blockedWhen: seed.blockedWhen ?? [],
    relatedDataQualityChecks: dataQualityDependencies,
  };
}

const baseMetrics = {
  revenue: defineMetric("revenue", {
    label: "Prihod",
    shortDescription: "Ukupna prodajna vrednost stavki prodaje za izabrani period i filtere.",
    formula: "SUM(prodajna_vrednost_stavke)",
    dataSource: "Sales facts analytics",
    interpretation: "Pokazuje obim prodaje, ali ne govori o profitabilnosti.",
    limitations: ["Ako je refresh zastareo ili parcijalan, metrika je indikativna."],
    dataQualityDependencies: ["Svežina refresh-a", "Potpunost sales facts podataka"],
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/supplier"],
    inputs: ["prodajna_vrednost_stavke", "period", "filteri"],
  }),
  marginContribution: defineMetric("marginContribution", {
    label: "Maržni doprinos",
    shortDescription: "Bruto doprinos: prihod umanjen za procenjeni nabavni trošak gde je trošak dostupan.",
    formula: "SUM(prodajna_vrednost - nabavni_trošak)",
    dataSource: "Sales facts analytics + cost coverage",
    interpretation: "Signal profitabilnosti pre operativnih troškova, nije neto profit.",
    limitations: ["Ako nedostaje nabavna cena, signal je manje pouzdan."],
    dataQualityDependencies: ["Pokrivenost nabavne cene", "Fallback cost procene"],
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/supplier"],
    inputs: ["prodajna_vrednost", "nabavna_cena", "količina"],
  }),
  unitsSold: defineMetric("unitsSold", {
    label: "Prodate jedinice",
    shortDescription: "Ukupan broj prodatih komada u periodu.",
    formula: "SUM(količina)",
    dataSource: "Sales facts analytics",
    interpretation: "Meri promet po količini, nezavisno od cene.",
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/supplier"],
    inputs: ["količina", "period", "filteri"],
  }),
  stockAtRisk: defineMetric("stockAtRisk", {
    label: "Lager u riziku",
    shortDescription: "Kapital vezan u zalihama sa povišenim rizikom spore rotacije ili OOS problema.",
    formula: "SUM(količina * nabavna_cena) za rizične SKU",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Pokazuje gde je kapital blokiran i gde postoji potreba za akcijom.",
    limitations: ["Nije knjigovodstvena valuacija."],
    dataQualityDependencies: ["Nabavna cena", "Tačnost stanja zaliha", "Svežina inventory snapshot-a"],
    relatedScreens: ["/analytics", "/analytics/inventory"],
    inputs: ["količina", "nabavna_cena", "risk_signal"],
  }),
  stockCoverDays: defineMetric("stockCoverDays", {
    label: "Pokrivenost zalihe",
    shortDescription: "Procena dana pokrivenosti zalihe na osnovu trenutnog stanja i prosečne dnevne prodaje.",
    formula: "currentOnHandUnits / avgDailySalesUnits",
    dataSource: "Inventory analytics + Product decision snapshot",
    interpretation: "Niža pokrivenost signalizira potrebu za dopunom, a previsoka pokrivenost može ukazati na spor obrt.",
    limitations: ["Ako nema pouzdanog velocity signala, metrika prelazi u insufficient_data/no_velocity status."],
    dataQualityDependencies: ["Istorija prodaje", "Tačnost stanja zaliha", "Svežina snapshot-a"],
    relatedScreens: ["/analytics/inventory", "/analytics/products"],
    blockedWhen: ["avgDailySalesUnits <= 0 bez stabilnog signala", "Nedostaje dovoljno istorije prodaje"],
    inputs: ["currentOnHandUnits", "avgDailySalesUnits"],
  }),
  slowStockCapital: defineMetric("slowStockCapital", {
    label: "Kapital u sporoj zalihi",
    shortDescription: "Procena kapitala vezanog u artiklima sa sporom rotacijom.",
    formula: "SUM(količina * nabavna_cena) za slow_stock artikle",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Pomaže odluci gde su potrebni markdown ili transfer akcije.",
    limitations: ["Ako se koristi fallback nabavna cena, procena je indikativna."],
    dataQualityDependencies: ["Nabavna cena", "Signal sporosti prodaje"],
    relatedScreens: ["/analytics", "/analytics/inventory", "/analytics/products"],
  }),
  lostSalesEstimate: defineMetric("lostSalesEstimate", {
    label: "Procena izgubljene prodaje",
    shortDescription: "Modelirana procena propuštenog prihoda zbog out-of-stock ili preniske dostupnosti.",
    formula: "SUM((procenjena_potražnja - realizacija) * prosečna_cena)",
    dataSource: "Product decision snapshot",
    interpretation: "Signal potencijalnog rasta kroz bolju dostupnost.",
    limitations: ["Modelska procena, nije knjižena prodaja."],
    dataQualityDependencies: ["Istorija prodaje", "OOS signal", "Svežina snapshot-a"],
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/inventory"],
  }),
  dataReadinessScore: defineMetric("dataReadinessScore", {
    label: "Spremnost podataka",
    shortDescription: "Kompozitni skor koji pokazuje koliko su podaci pogodni za pouzdane preporuke.",
    formula: "Ponderisani skor kvaliteta master i transakcionih podataka",
    dataSource: "Data quality checks",
    interpretation: "Viši skor znači manji rizik pogrešne preporuke.",
    limitations: ["Visok skor ne garantuje da su sve pojedinačne metrike bez problema."],
    dataQualityDependencies: ["missing_cost", "missing_supplier", "insufficient_signal", "refresh_freshness"],
    relatedScreens: ["/analytics/data-quality", "/analytics"],
  }),
  missingCostCount: defineMetric("missingCostCount", {
    label: "Redovi bez nabavne cene",
    shortDescription: "Broj redova ili artikala bez potvrđene nabavne cene.",
    formula: "COUNT(redovi gde nabavna_cena IS NULL)",
    dataSource: "Data quality checks",
    interpretation: "Direktno utiče na pouzdanost marže i maržnih preporuka.",
    dataQualityDependencies: ["Mapiranje nabavnih cena"],
    relatedScreens: ["/analytics/data-quality", "/analytics/products"],
  }),
  missingSupplierCount: defineMetric("missingSupplierCount", {
    label: "Artikli bez dobavljača",
    shortDescription: "Broj artikala koji nemaju validno mapiranog dobavljača.",
    formula: "COUNT(artikli gde supplier_id IS NULL)",
    dataSource: "Data quality checks",
    interpretation: "Utiče na supplier scorecard i dobavljačke preporuke.",
    dataQualityDependencies: ["Supplier mapping kvalitet"],
    relatedScreens: ["/analytics/data-quality", "/analytics/supplier"],
  }),
  sellThrough: defineMetric("sellThrough", {
    label: "Sell-through",
    shortDescription: "Udeo prodate količine u odnosu na dostupnu količinu u posmatranom periodu.",
    formula: "(prodate_jedinice / dostupne_jedinice) * 100",
    dataSource: "Sales facts + inventory snapshot",
    interpretation: "Viši sell-through ukazuje na zdraviju rotaciju asortimana.",
    limitations: ["Zavisi od kvaliteta početnog i završnog stanja zaliha."],
    dataQualityDependencies: ["Tačnost stanja zaliha"],
    relatedScreens: ["/analytics/inventory", "/analytics/products"],
  }),
  velocity: defineMetric("velocity", {
    label: "Brzina prodaje",
    shortDescription: "Prosečan broj prodatih jedinica po danu.",
    formula: "prodate_jedinice / broj_dana",
    dataSource: "Sales facts analytics",
    interpretation: "Pomaže proceni dopune i prioriteta nabavke.",
    limitations: ["Kod kratkog perioda signal može biti nestabilan."],
    dataQualityDependencies: ["Dužina perioda", "Potpunost prodaje"],
    relatedScreens: ["/analytics/products", "/analytics/inventory"],
  }),
  confidencePct: defineMetric("confidencePct", {
    label: "Sigurnost preporuke",
    shortDescription: "Stepen sigurnosti da je preporučena akcija validna za izabrani kontekst.",
    formula: "Model confidence score * 100",
    dataSource: "Recommendation engine metadata",
    interpretation: "Niža vrednost znači veći rizik pogrešne odluke.",
    limitations: ["Nije apsolutna garancija ishoda."],
    dataQualityDependencies: ["Insufficient history", "Missing cost/supplier"],
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/supplier"],
  }),
  reliabilityPct: defineMetric("reliabilityPct", {
    label: "Pouzdanost signala",
    shortDescription: "Kvalitet ulaznih podataka i stabilnost signala na osnovu kog je data preporuka.",
    formula: "Signal reliability score * 100",
    dataSource: "Recommendation engine metadata",
    interpretation: "Niža pouzdanost znači da signal treba čitati opreznije.",
    limitations: ["Može biti visoko osetljivo na mali uzorak."],
    dataQualityDependencies: ["Insufficient signal", "Partial dataset", "Fallback dataset"],
    relatedScreens: ["/analytics/products", "/analytics/supplier", "/analytics/nivelacije-pre-post"],
  }),
  markdownDependency: defineMetric("markdownDependency", {
    label: "Zavisnost od nivelacija",
    shortDescription: "Koliko prodaja zavisi od sniženja i nivoa markdown aktivnosti.",
    formula: "(prihod_iz_markdowna / ukupan_prihod) * 100",
    dataSource: "Supplier decision materialized view",
    interpretation: "Viša vrednost može signalizirati pritisak na maržu.",
    limitations: ["Potrebna dosledna oznaka markdown transakcija."],
    dataQualityDependencies: ["Tačnost oznake promo/nivelacije"],
    relatedScreens: ["/analytics/supplier", "/analytics/nivelacije-pre-post"],
  }),
  outOfStockRisk: defineMetric("outOfStockRisk", {
    label: "Rizik nestanka zalihe",
    shortDescription: "Signal verovatnoće da će artikal ostati bez zalihe u kratkom roku.",
    formula: "Model OOS risk score",
    dataSource: "Inventory recommendations",
    interpretation: "Pomaže prioritizaciji dopune i transfera.",
    limitations: ["Model zavisi od kvaliteta ulaznih trendova i zaliha."],
    dataQualityDependencies: ["Svežina stanja zaliha", "Istorija prodaje"],
    relatedScreens: ["/analytics/inventory", "/analytics/products"],
  }),
} as const;

const operationalMetrics: Record<string, AnalyticsMetricDefinition> = {
  lowStockCount: defineMetric("lowStockCount", {
    label: "Niska zaliha",
    shortDescription: "Broj SKU koji su na ili ispod bezbednog minimuma.",
    formula: "COUNT(SKU gde količina <= minimalni_prag)",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Signal operativnog rizika i potrebe za dopunom.",
    relatedScreens: ["/analytics/inventory"],
  }),
  replenishCount: defineMetric("replenishCount", {
    label: "Za dopunu",
    shortDescription: "Broj proizvoda sa statusom preporuke REPLENISH.",
    formula: "COUNT(status = REPLENISH)",
    dataSource: "Product decision snapshot",
    interpretation: "Pokazuje obim dopune koje zahtevaju akciju.",
    relatedScreens: ["/analytics/products"],
  }),
  boostCount: defineMetric("boostCount", {
    label: "Za pojačanje",
    shortDescription: "Broj proizvoda sa statusom preporuke BOOST.",
    formula: "COUNT(status = BOOST)",
    dataSource: "Product decision snapshot",
    interpretation: "Artikli sa potencijalom za jače ulaganje ili vidljivost.",
    relatedScreens: ["/analytics/products"],
  }),
  markdownCount: defineMetric("markdownCount", {
    label: "Za sniženje",
    shortDescription: "Broj proizvoda sa statusom preporuke MARKDOWN.",
    formula: "COUNT(status = MARKDOWN)",
    dataSource: "Product decision snapshot",
    interpretation: "Artikli za koje je potrebno smanjenje cene radi oslobađanja kapitala.",
    relatedScreens: ["/analytics/products"],
  }),
  doNotOrderCount: defineMetric("doNotOrderCount", {
    label: "Ne naručivati",
    shortDescription: "Broj proizvoda sa statusom preporuke DO_NOT_ORDER.",
    formula: "COUNT(status = DO_NOT_ORDER)",
    dataSource: "Product decision snapshot",
    interpretation: "Sprečava gomilanje zaliha sa slabom perspektivom prodaje.",
    relatedScreens: ["/analytics/products"],
  }),
  fixDataCount: defineMetric("fixDataCount", {
    label: "Proveriti podatke",
    shortDescription: "Broj proizvoda sa statusom preporuke FIX_DATA.",
    formula: "COUNT(status = FIX_DATA)",
    dataSource: "Product decision snapshot",
    interpretation: "Pokazuje gde data quality blokira pouzdanu odluku.",
    relatedScreens: ["/analytics/products", "/analytics/data-quality"],
  }),
  topSupplierRevenueShare: defineMetric("topSupplierRevenueShare", {
    label: "Udeo top dobavljača",
    shortDescription: "Udeo prihoda koji dolazi od top dobavljača u scorecard periodu.",
    formula: "(prihod_top_dobavljača / ukupan_prihod) * 100",
    dataSource: "Supplier decision materialized view",
    interpretation: "Meri koncentraciju i zavisnost od malog broja dobavljača.",
    relatedScreens: ["/analytics/supplier"],
  }),
  fullPriceShareChange: defineMetric("fullPriceShareChange", {
    label: "Promena udela pune cene",
    shortDescription: "Razlika udela prodaje po punoj ceni između tekućeg i prethodnog perioda.",
    formula: "full_price_share_now - full_price_share_previous",
    dataSource: "Supplier decision materialized view",
    interpretation: "Signal promene kvaliteta prodaje bez promo oslanjanja.",
    relatedScreens: ["/analytics/supplier"],
  }),
  activeSkuShare: defineMetric("activeSkuShare", {
    label: "Udeo aktivnih SKU",
    shortDescription: "Udeo SKU sa pozitivnom zalihom.",
    formula: "(SKU_sa_stanjem / ukupan_SKU) * 100",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Pokazuje širinu aktivnog asortimana.",
    relatedScreens: ["/analytics/inventory"],
  }),
  inventoryHealthScore: defineMetric("inventoryHealthScore", {
    label: "Stanje fonda",
    shortDescription: "Kompozitni skor stanja zaliha na osnovu više inventory signala.",
    formula: "Kompozitni inventory score",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Brza procena operativnog zdravlja zaliha.",
    relatedScreens: ["/analytics/inventory"],
  }),
  skuCount: defineMetric("skuCount", {
    label: "Ukupno SKU",
    shortDescription: "Broj jedinstvenih artikala u izabranom opsegu.",
    formula: "COUNT(DISTINCT sku)",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Meri širinu asortimana.",
    relatedScreens: ["/analytics/inventory"],
  }),
  avgUnitsPerSku: defineMetric("avgUnitsPerSku", {
    label: "Prosečno po SKU",
    shortDescription: "Prosečna količina robe po artiklu.",
    formula: "ukupno_na_stanju / ukupno_sku",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Pomaže proceni prezasićenosti ili deficita zaliha.",
    relatedScreens: ["/analytics/inventory"],
  }),
  onHandUnits: defineMetric("onHandUnits", {
    label: "Ukupno na stanju",
    shortDescription: "Ukupna raspoloživa količina robe na stanju.",
    formula: "SUM(količina_na_stanju)",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Operativni obim zaliha za planiranje dopune.",
    relatedScreens: ["/analytics/inventory"],
  }),
  inventoryTotalValue: defineMetric("inventoryTotalValue", {
    label: "Procena vrednosti zaliha",
    shortDescription: "Procenjena nabavna vrednost raspoložive zalihe.",
    formula: "SUM(količina * nabavna_cena)",
    dataSource: "Inventory analytics snapshot",
    interpretation: "Pokazuje kapital vezan u robi na stanju.",
    limitations: ["Ako je nabavna cena parcijalna, procena je indikativna."],
    dataQualityDependencies: ["Pokrivenost nabavne cene"],
    relatedScreens: ["/analytics/inventory"],
  }),
  revenueWithoutCost: defineMetric("revenueWithoutCost", {
    label: "Prihod bez nabavne cene",
    shortDescription: "Udeo prihoda za koji ne postoji potvrđena nabavna cena.",
    formula: "(prihod_bez_nabavne_cene / ukupan_prihod) * 100",
    dataSource: "Data quality checks",
    interpretation: "Viši udeo smanjuje pouzdanost maržnih metrika i preporuka.",
    relatedScreens: ["/analytics/data-quality", "/analytics"],
  }),
  blockedRecommendationsCount: defineMetric("blockedRecommendationsCount", {
    label: "Blokirane preporuke",
    shortDescription: "Broj preporuka koje su blokirane zbog problema kvaliteta podataka.",
    formula: "COUNT(preporuka gde je recommendation_allowed = false zbog data quality razloga)",
    dataSource: "Data quality checks",
    interpretation: "Pokazuje operativni uticaj data quality problema na recommendation tok.",
    relatedScreens: ["/analytics/data-quality", "/analytics/reports/pilot-intake"],
  }),
  ignoredRowsCount: defineMetric("ignoredRowsCount", {
    label: "Ignorisani redovi",
    shortDescription: "Broj redova isključenih iz analitike zbog nevalidnih ili nepotpunih podataka.",
    formula: "COUNT(redova označenih kao ignored u quality pipeline-u)",
    dataSource: "Data quality checks",
    interpretation: "Veći broj može značajno uticati na reprezentativnost signala.",
    relatedScreens: ["/analytics/data-quality", "/analytics", "/analytics/reports/pilot-intake"],
  }),
  grossMarginPct: defineMetric("grossMarginPct", {
    label: "Bruto marža %",
    shortDescription: "Procenat bruto marže u odnosu na prihod.",
    formula: "((prihod - nabavni_trošak) / prihod) * 100",
    dataSource: "Sales facts analytics + cost coverage",
    interpretation: "Pomaže poređenju profitabilnosti kroz periode i segmente.",
    limitations: ["Ako nedostaje nabavna cena, rezultat je indikativan."],
    dataQualityDependencies: ["Pokrivenost nabavne cene"],
    relatedScreens: ["/analytics", "/analytics/products", "/analytics/supplier"],
  }),
  inventoryTurnover: defineMetric("inventoryTurnover", {
    label: "Obrt zaliha",
    shortDescription: "Koliko puta se prosečna zaliha obrne kroz period.",
    formula: "trošak_prodate_robe / prosečna_vrednost_zaliha",
    dataSource: "Inventory analytics snapshot + sales facts",
    interpretation: "Viši obrt uglavnom znači efikasnije upravljanje zalihama.",
    limitations: ["Trenutno indikativna metrika; zavisi od kvaliteta cost i stock podataka."],
    dataQualityDependencies: ["Pokrivenost nabavne cene", "Tačnost stanja zaliha"],
    relatedScreens: ["/analytics/inventory"],
  }),
  // TODO(analytics-methodology): Dodati GMROI kada backend iznese stabilnu metriku i DTO polja.
} as const;

type BaseMetricKey =
  | keyof typeof baseMetrics
  | keyof typeof operationalMetrics
  | "missingCostRevenueShare"
  | "unknownSupplierRevenueShare";

export const metricAliases = {
  totalRevenue: "revenue",
  soldUnits: "unitsSold",
  stockRiskCapital: "stockAtRisk",
  slowStock: "slowStockCapital",
  lostSales: "lostSalesEstimate",
  dataReadiness: "dataReadinessScore",
  missingCostRevenueShare: "revenueWithoutCost",
  revenueUnknownSupplier: "unknownSupplierRevenueShare",
  totalInventoryValue: "inventoryTotalValue",
  stockUnits: "onHandUnits",
  quantity: "unitsSold",
} as const satisfies Partial<Record<AnalyticsMetricKey, CanonicalMetricKey>>;

const canonicalMetricDefinitions = {
  ...baseMetrics,
  ...operationalMetrics,
  unknownSupplierRevenueShare: defineMetric("unknownSupplierRevenueShare", {
    label: "Promet nepoznatog dobavljača",
    shortDescription: "Udeo prihoda bez validnog mapiranja dobavljača.",
    formula: "(prihod_bez_dobavljača / ukupan_prihod) * 100",
    dataSource: "Data quality checks",
    interpretation: "Viši udeo smanjuje pouzdanost supplier analitike.",
    relatedScreens: ["/analytics/data-quality", "/analytics/supplier"],
  }),
} as Record<CanonicalMetricKey, AnalyticsMetricDefinition>;

export const analyticsMetricDefinitions: Record<AnalyticsMetricKey, AnalyticsMetricDefinition> = Object.fromEntries(
  [
    ...canonicalMetricKeys.map((key) => [key, { ...canonicalMetricDefinitions[key], key }] as const),
    ...Object.entries(metricAliases).map(([alias, canonical]) => [alias, { ...canonicalMetricDefinitions[canonical], key: alias }] as const),
  ]
) as Record<AnalyticsMetricKey, AnalyticsMetricDefinition>;

const metricAliasesByLabel: Partial<Record<AnalyticsMetricKey, string[]>> = {
  revenue: ["Prihod", "Promet", "Ukupan prihod"],
  marginContribution: ["Maržni doprinos", "Ukupan maržni doprinos"],
  unitsSold: ["Prodate jedinice", "Komadi", "Količina"],
  stockAtRisk: ["Lager u riziku", "Kapital u riziku"],
  stockCoverDays: ["Pokrivenost zalihe", "Stock cover", "Days of supply"],
  slowStockCapital: ["Kapital u sporoj zalihi"],
  lostSalesEstimate: ["Procena izgubljene prodaje", "Izgubljena prodaja"],
  dataReadinessScore: ["Spremnost podataka", "Data readiness", "Data quality score"],
  missingCostCount: ["Bez nabavne cene", "Redovi bez nabavne cene"],
  missingSupplierCount: ["Bez dobavljača", "Artikli bez dobavljača"],
  sellThrough: ["Sell-through"],
  velocity: ["Brzina prodaje", "Velocity"],
  reliabilityPct: ["Pouzdanost signala", "Reliability"],
  confidencePct: ["Sigurnost preporuke", "Confidence"],
  markdownDependency: ["Zavisnost od nivelacija", "Markdown dependency"],
  outOfStockRisk: ["OOS rizik", "Rizik nestanka zalihe"],
  blockedRecommendationsCount: ["Blokirane preporuke"],
  ignoredRowsCount: ["Ignorisani redovi"],
  grossMarginPct: ["Bruto marža %", "Gross margin %"],
  inventoryTurnover: ["Obrt zaliha", "Inventory turnover"],
  revenueWithoutCost: ["Prihod bez nabavne cene", "Promet bez nabavne cene"],
  unknownSupplierRevenueShare: ["Promet nepoznatog dobavljača"],
};

export function normalizeMetricKey(key: string): AnalyticsMetricKey | string {
  const alias = metricAliases[key as keyof typeof metricAliases];
  if (alias) return alias;

  if ((canonicalMetricKeys as readonly string[]).includes(key)) return key as AnalyticsMetricKey;

  return key;
}

export function getAnalyticsMetricDefinition(metricKey: AnalyticsMetricKey): AnalyticsMetricDefinition {
  return getMetricDefinition(metricKey);
}

export function getMetricDefinition(metricKey: AnalyticsMetricKey | string): AnalyticsMetricDefinition {
  const normalizedKey = normalizeMetricKey(String(metricKey));
  if (normalizedKey in canonicalMetricDefinitions) {
    return canonicalMetricDefinitions[normalizedKey as CanonicalMetricKey];
  }

  return {
    key: String(metricKey) as AnalyticsMetricKey,
    label: String(metricKey),
    shortDescription: "Metodologija za ovu metriku još nije dokumentovana.",
    formula: "Metodologija za ovu metriku još nije dokumentovana.",
    dataSource: "Metodologija za ovu metriku još nije dokumentovana.",
    interpretation: "Metodologija za ovu metriku još nije dokumentovana.",
    limitations: [],
    dataQualityDependencies: [],
    relatedScreens: [],
    title: String(metricKey),
    source: "Metodologija za ovu metriku još nije dokumentovana.",
    description: "Metodologija za ovu metriku još nije dokumentovana.",
    qualityNote: "Metodologija za ovu metriku još nije dokumentovana.",
    businessMeaning: "Metodologija za ovu metriku još nije dokumentovana.",
    formulaText: "Metodologija za ovu metriku još nije dokumentovana.",
    inputs: [],
    caveats: [],
    blockedWhen: [],
    relatedDataQualityChecks: [],
  };
}

export function getMetricLabel(metricKey: AnalyticsMetricKey | string): string {
  return getMetricDefinition(metricKey)?.label ?? "Nepoznata metrika";
}

export function getMetricFormula(metricKey: AnalyticsMetricKey | string): string {
  return getMetricDefinition(metricKey)?.formula ?? "Formula za ovu metriku još nije dokumentovana.";
}

export function getMetricMethodologyItems(
  metricKeys: Array<AnalyticsMetricKey | string>
): Array<AnalyticsMetricDefinition | { key: string; label: string; isDocumented: false; message: string }> {
  return metricKeys.map((metricKey) => {
    const definition = getMetricDefinition(metricKey);
    if ("shortDescription" in definition && definition.shortDescription === "Metodologija za ovu metriku još nije dokumentovana.") {
      return {
        key: String(metricKey),
        label: String(metricKey),
        isDocumented: false as const,
        message: "Metodologija za ovu metriku još nije dokumentovana.",
      };
    }

    return definition;
  });
}

export function findAnalyticsMetricKeyByLabel(label: string | null | undefined): AnalyticsMetricKey | null {
  if (!label) return null;
  const normalized = label.trim().toLocaleLowerCase("sr-Latn-RS");
  const direct = canonicalMetricKeys.find((key) => canonicalMetricDefinitions[key].label.toLocaleLowerCase("sr-Latn-RS") === normalized);
  if (direct) return direct;

  for (const [key, aliases] of Object.entries(metricAliasesByLabel) as Array<[AnalyticsMetricKey, string[] | undefined]>) {
    if (aliases?.some((alias) => alias.toLocaleLowerCase("sr-Latn-RS") === normalized)) {
      return key;
    }
  }

  return null;
}
