export type AnalyticsMetricKey =
  | "revenue"
  | "marginContribution"
  | "quantity"
  | "stockRiskCapital"
  | "dataReadinessScore"
  | "replenishCount"
  | "boostCount"
  | "markdownCount"
  | "doNotOrderCount"
  | "fixDataCount"
  | "lostSalesEstimate"
  | "slowStockCapital"
  | "topSupplierRevenueShare"
  | "fullPriceShareChange"
  | "activeSkuShare"
  | "inventoryHealthScore"
  | "skuCount"
  | "onHandUnits"
  | "lowStockCount"
  | "avgUnitsPerSku"
  | "inventoryTotalValue"
  | "missingCostRevenueShare"
  | "unknownSupplierRevenueShare";

export type AnalyticsMetricDefinition = {
  label: string;
  businessMeaning: string;
  formulaText: string;
  inputs: string[];
  dataSource: string[];
  caveats: string[];
  blockedWhen: string[];
  relatedDataQualityChecks: string[];
};

export const analyticsMetricDefinitions: Record<AnalyticsMetricKey, AnalyticsMetricDefinition> = {
  revenue: {
    label: "Prihod",
    businessMeaning: "Ukupna prodajna vrednost u izabranom periodu i filteru. Koristi se kao osnovni signal obima prodaje.",
    formulaText: "Suma prodajnih cena puta količina za prodajne stavke u izabranom periodu.",
    inputs: ["prodajne stavke", "prodajna cena", "količina", "period", "filter prodavnice/dobavljača"],
    dataSource: ["Analytics sales snapshot", "prodaja_stavke"],
    caveats: [
      "Povrati, popusti i nivelacije zavise od dostupnih polja u izvoru.",
      "Ako je payload partial ili fallback, prihod je indikativan dok se refresh ne stabilizuje.",
    ],
    blockedWhen: [
      "Sales snapshot nije osvežen ili je API vratio error payload.",
      "Period ili filter vraća prazan skup bez potvrde da je to stvarno poslovna nula.",
    ],
    relatedDataQualityChecks: ["Stale refresh", "Missing sales history", "Fallback dataset warning"],
  },
  marginContribution: {
    label: "Maržni doprinos",
    businessMeaning: "Procena koliko prodaja doprinosi zaradi pre operativnih troškova. Nije isto što i neto profit.",
    formulaText: "Prihod minus procenjena ili direktna nabavna vrednost za promet sa dostupnim troškom.",
    inputs: ["prodajna cena", "nabavna cena", "količina", "cost coverage"],
    dataSource: ["Analytics margin snapshot", "historical cost", "fallback item cost"],
    caveats: [
      "Operativni troškovi, rabati van transakcije i marketinški troškovi nisu uključeni.",
      "Ako je deo troška procenjen, signal treba čitati uz oprez.",
    ],
    blockedWhen: [
      "Nedostaje nabavna cena za značajan deo prometa.",
      "Cost coverage je prenizak za pouzdano poređenje.",
    ],
    relatedDataQualityChecks: ["Promet bez nabavne cene", "Direct cost coverage", "Estimated cost coverage"],
  },
  quantity: {
    label: "Prodate jedinice",
    businessMeaning: "Ukupan broj prodatih komada u izabranom periodu.",
    formulaText: "Suma količine svih prodajnih stavki u periodu i filteru.",
    inputs: ["prodajne stavke", "količina", "period", "filter"],
    dataSource: ["Analytics sales snapshot", "prodaja_stavke"],
    caveats: ["Ne predstavlja broj računa niti broj različitih SKU.", "Storniranja i povrati zavise od ulaznog modela prodaje."],
    blockedWhen: ["Sales snapshot nije dostupan za izabrani period."],
    relatedDataQualityChecks: ["Stale refresh", "Missing sales history"],
  },
  stockRiskCapital: {
    label: "Kapital u riziku",
    businessMeaning: "Procena vrednosti kapitala vezanog u zalihi koja sporo rotira, traži sniženje ili je izložena OOS/overstock riziku.",
    formulaText: "Količina na lageru puta nabavna ili procenjena vrednost za artikle označene kao rizične u aktivnom signalu.",
    inputs: ["količina na lageru", "nabavna cena ili fallback cena", "risk flag", "sporost prodaje"],
    dataSource: ["Inventory analytics snapshot", "inventory risk model"],
    caveats: [
      "Nije knjigovodstvena vrednost zalihe nego operativna procena rizika.",
      "Ako je cena fallback ili signal parcijalan, broj je indikativan.",
    ],
    blockedWhen: ["Nema pouzdane nabavne cene ili risk signal nije izračunat.", "Inventory snapshot je stale ili partial."],
    relatedDataQualityChecks: ["Missing cost", "Inventory snapshot freshness", "Unknown supplier mapping"],
  },
  dataReadinessScore: {
    label: "Spremnost podataka",
    businessMeaning: "Sažeti signal koliko su ulazni podaci dovoljno kompletni, sveži i povezani da analitika može da daje pouzdane KPI-jeve i preporuke.",
    formulaText: "Ponderisani skor kompletnosti dobavljača, nabavnih cena, kategorija, prodajne istorije i stanja refresh-a.",
    inputs: ["missing supplier count", "missing cost share", "missing category count", "sales history", "refresh freshness"],
    dataSource: ["Data quality checks", "analytics refresh status", "health snapshot"],
    caveats: [
      "Skor je sažetak, ne zamena za pregled pojedinačnih problema.",
      "Dobar skor ne znači da nijedan pojedinačni KPI nema lokalno ograničenje.",
    ],
    blockedWhen: ["Health snapshot nije osvežen.", "Data quality endpoint nije dostupan."],
    relatedDataQualityChecks: ["Artikli bez dobavljača", "Promet bez nabavne cene", "Stale refresh", "Missing category"],
  },
  replenishCount: {
    label: "Za dopunu",
    businessMeaning: "Broj proizvoda za koje backend decision engine preporučuje dopunu kako bi se smanjio rizik propuštene prodaje.",
    formulaText: "Broj redova sa recommendationStatus = REPLENISH u aktivnom filteru i periodu.",
    inputs: ["recommendationStatus", "period", "prodavnica", "dobavljač"],
    dataSource: ["Product decision snapshot"],
    caveats: ["Front-end samo prikazuje count iz backend payload-a.", "Broj se menja sa filterima i osvežavanjem decision engine-a."],
    blockedWhen: ["Decision snapshot nije dostupan.", "Data quality blokira finalnu preporuku za deo artikala."],
    relatedDataQualityChecks: ["Missing supplier", "Missing cost", "Insufficient sales history"],
  },
  boostCount: {
    label: "Za pojačanje",
    businessMeaning: "Broj proizvoda koji zaslužuju veći fokus ili dodatnu nabavku zbog dobrog signala prodaje i kvaliteta.",
    formulaText: "Broj redova sa recommendationStatus = BOOST u aktivnom filteru i periodu.",
    inputs: ["recommendationStatus", "trend", "velocity", "confidence", "reliability"],
    dataSource: ["Product decision snapshot"],
    caveats: ["Ne znači automatsku narudžbinu bez poslovne provere.", "Count je validan samo za trenutno aktivne filtere."],
    blockedWhen: ["Recommendation signal nije dovoljno pouzdan.", "Decision engine vraća insufficient_data za veliki deo skupa."],
    relatedDataQualityChecks: ["Insufficient history", "Stale refresh", "Missing cost"],
  },
  markdownCount: {
    label: "Za sniženje",
    businessMeaning: "Broj proizvoda za koje signal ukazuje da kapital treba rasteretiti kroz nižu cenu ili bržu rotaciju.",
    formulaText: "Broj redova sa recommendationStatus = MARKDOWN u aktivnom filteru i periodu.",
    inputs: ["recommendationStatus", "slow stock signal", "trend", "margin guardrails"],
    dataSource: ["Product decision snapshot", "inventory risk model"],
    caveats: ["Ne uključuje automatski procenu optimalne nove cene.", "Count ne govori koliki je ukupan kapital u riziku bez dodatnog KPI-ja."],
    blockedWhen: ["Slow stock signal nije izračunat.", "Inventory ili pricing snapshot je parcijalan."],
    relatedDataQualityChecks: ["Inventory freshness", "Missing cost", "Price history gaps"],
  },
  doNotOrderCount: {
    label: "Ne naručivati",
    businessMeaning: "Broj proizvoda gde backend signal smatra da nova narudžbina nije opravdana u aktuelnom periodu.",
    formulaText: "Broj redova sa recommendationStatus = DO_NOT_ORDER u aktivnom filteru i periodu.",
    inputs: ["recommendationStatus", "stock on hand", "velocity", "trend", "confidence"],
    dataSource: ["Product decision snapshot", "inventory snapshot"],
    caveats: ["Ne znači da proizvod treba ugasiti, već da trenutni signal ne podržava novu narudžbinu.", "Treba ga čitati zajedno sa lagerom i trendom."],
    blockedWhen: ["Decision payload je fallback ili signal nije finalan."],
    relatedDataQualityChecks: ["Stale inventory snapshot", "Missing supplier", "Low confidence signal"],
  },
  fixDataCount: {
    label: "Proveriti podatke",
    businessMeaning: "Broj proizvoda kod kojih backend eksplicitno označava da kvalitet ulaza blokira poslovnu preporuku.",
    formulaText: "Broj redova sa recommendationStatus = FIX_DATA u aktivnom filteru i periodu.",
    inputs: ["recommendationStatus", "dataQualityStatus", "reasonCodes"],
    dataSource: ["Product decision snapshot", "data quality checks"],
    caveats: ["Ovo nije prodajni signal nego radni red za ispravku podataka.", "Count može pasti bez promene prodaje ako se isprave ulazi."],
    blockedWhen: ["Data quality checks nisu dostupni."],
    relatedDataQualityChecks: ["Missing supplier", "Missing cost", "Missing category", "Invalid product naming"],
  },
  lostSalesEstimate: {
    label: "Procena izgubljene prodaje",
    businessMeaning: "Procena koliko je prihoda verovatno izgubljeno zbog OOS ili preniske zalihe.",
    formulaText: "Modelirana procena propuštenog prihoda na osnovu potražnje, istorije prodaje i stanja zalihe.",
    inputs: ["istorija prodaje", "OOS signal", "velocity", "trenutna zaliha"],
    dataSource: ["Product decision snapshot", "inventory forecast"],
    caveats: ["Procena je model, ne direktno knjižena prodaja.", "Osetljiva je na kratke periode i nekompletne OOS događaje."],
    blockedWhen: ["Nema dovoljno istorije prodaje.", "OOS signal ili demand forecast nisu dostupni."],
    relatedDataQualityChecks: ["Missing sales history", "Inventory freshness", "Store coverage gaps"],
  },
  slowStockCapital: {
    label: "Kapital u sporoj zalihi",
    businessMeaning: "Procena novca vezanog u artiklima koji sporo rotiraju i traže proveru akcije, transfera ili sniženja.",
    formulaText: "Količina na zalihi puta nabavna ili procenjena cena za artikle označene kao slow stock.",
    inputs: ["količina", "nabavna cena", "slow stock flag"],
    dataSource: ["Product decision snapshot", "inventory analytics snapshot"],
    caveats: ["Nije ista stvar kao ukupna vrednost zalihe.", "Ako je nabavna cena fallback, procena je konzervativna."],
    blockedWhen: ["Slow stock signal ili cost coverage nisu dostupni."],
    relatedDataQualityChecks: ["Missing cost", "Inventory freshness", "Unknown supplier mapping"],
  },
  topSupplierRevenueShare: {
    label: "Udeo top 5 dobavljača",
    businessMeaning: "Pokazuje koliki deo prihoda u scorecard skupu dolazi od pet najvećih dobavljača, odnosno koliko je prihod koncentrisan.",
    formulaText: "Prihod top 5 dobavljača podeljen sa ukupnim prihodom svih dobavljača u scorecard skupu puta 100.",
    inputs: ["supplier revenue", "scorecard dataset", "period"],
    dataSource: ["Supplier decision scorecard snapshot"],
    caveats: ["Scorecard skup može biti uži od ukupnog supplier pregleda.", "Visoka koncentracija ne znači automatski problem, ali povećava zavisnost."],
    blockedWhen: ["Scorecard dataset je fallback ili nema dovoljno redova."],
    relatedDataQualityChecks: ["Missing supplier names", "Fallback dataset warning"],
  },
  fullPriceShareChange: {
    label: "Promena udela pune cene",
    businessMeaning: "Meri da li se veći ili manji deo prodaje ostvaruje po punoj ceni u odnosu na prethodni uporedivi period.",
    formulaText: "Udeo prodaje po punoj ceni u tekućem periodu minus udeo prodaje po punoj ceni u prethodnom periodu.",
    inputs: ["full-price sales share", "previous comparable period", "period filter"],
    dataSource: ["Supplier decision scorecard snapshot", "price/markdown history"],
    caveats: ["Pozitivna promena ne mora značiti rast ukupnog prihoda.", "Ako prethodni period nije reprezentativan, signal slabi."],
    blockedWhen: ["Prethodni uporedivi period nije dostupan.", "Markdown history nije potpuna."],
    relatedDataQualityChecks: ["Price history gaps", "Fallback dataset warning", "Stale refresh"],
  },
  activeSkuShare: {
    label: "Aktivni SKU",
    businessMeaning: "Udeo artikala koji trenutno nisu bez zalihe i operativno su raspoloživi za prodaju.",
    formulaText: "Broj SKU sa pozitivnim raspoloživim stanjem podeljen sa ukupnim brojem SKU u filteru puta 100.",
    inputs: ["on-hand quantity", "sku count", "store filter", "supplier filter"],
    dataSource: ["Inventory balance snapshot"],
    caveats: ["Ne govori ništa o tražnji ni o kvalitetu asortimana.", "Može izgledati dobro čak i kad su ključni SKU bez zalihe."],
    blockedWhen: ["Inventory balance snapshot nije dostupan."],
    relatedDataQualityChecks: ["Inventory freshness", "Store coverage gaps"],
  },
  inventoryHealthScore: {
    label: "Stanje fonda",
    businessMeaning: "Sažeti operativni skor koji kombinuje aktivne SKU, rizik praznih polica i strukturu zalihe.",
    formulaText: "Kompozitni score od 0 do 100 iz više inventory signala: aktivni SKU, OOS rizik, low stock i struktura fonda.",
    inputs: ["active SKU share", "out-of-stock count", "low stock count", "inventory mix"],
    dataSource: ["Inventory balance snapshot", "inventory alerts"],
    caveats: ["Skor služi za brzi pregled, ne za zamenu detaljne analize.", "Poređenja između veoma različitih filtera treba raditi oprezno."],
    blockedWhen: ["Nedostaje inventory snapshot ili alert signal."],
    relatedDataQualityChecks: ["Inventory freshness", "Alert snapshot freshness"],
  },
  skuCount: {
    label: "Ukupno SKU",
    businessMeaning: "Broj jedinstvenih artikala u aktivnom inventory filteru.",
    formulaText: "Prebroj jedinstvene SKU u filtriranom skupu inventara.",
    inputs: ["sku", "prodavnica", "dobavljač", "inventory filter"],
    dataSource: ["Inventory list snapshot"],
    caveats: ["Pokazuje širinu asortimana, ne količinu robe."],
    blockedWhen: ["Inventory list nije dostupan."],
    relatedDataQualityChecks: ["Store coverage gaps", "Unknown supplier mapping"],
  },
  onHandUnits: {
    label: "Ukupno na stanju",
    businessMeaning: "Ukupna pozitivna raspoloživa količina robe u aktivnom opsegu.",
    formulaText: "Suma pozitivnih količina na stanju za sve SKU u filteru.",
    inputs: ["on-hand quantity", "sku filter"],
    dataSource: ["Inventory list snapshot"],
    caveats: ["Ne uključuje rezervacije, negativne korekcije ili planirane isporuke."],
    blockedWhen: ["Inventory list snapshot nije dostupan."],
    relatedDataQualityChecks: ["Inventory freshness"],
  },
  lowStockCount: {
    label: "Niska zaliha",
    businessMeaning: "Broj artikala koji su na ili ispod minimalnog bezbednog nivoa zalihe.",
    formulaText: "Prebroj SKU gde je trenutna količina manja ili jednaka minimalnom nivou ili fallback pragu.",
    inputs: ["on-hand quantity", "minimum stock threshold"],
    dataSource: ["Inventory list snapshot", "inventory thresholds"],
    caveats: ["Fallback prag nije isto što i ručno postavljen minimum po artiklu."],
    blockedWhen: ["Nedostaju threshold pravila ili inventory snapshot."],
    relatedDataQualityChecks: ["Inventory freshness", "Threshold coverage"],
  },
  avgUnitsPerSku: {
    label: "Prosečno po SKU",
    businessMeaning: "Srednja količina robe po jedinstvenom artiklu u aktivnom filteru.",
    formulaText: "Ukupno na stanju podeljeno sa ukupnim brojem SKU.",
    inputs: ["ukupno na stanju", "ukupno SKU"],
    dataSource: ["Inventory balance snapshot"],
    caveats: ["Prosek može sakriti ekstremno visoke ili ekstremno niske SKU vrednosti."],
    blockedWhen: ["Ukupno SKU je nula ili inventory balance nije dostupan."],
    relatedDataQualityChecks: ["Inventory freshness"],
  },
  inventoryTotalValue: {
    label: "Procena vrednosti",
    businessMeaning: "Procena nabavne vrednosti pozitivne raspoložive zalihe u aktivnom opsegu.",
    formulaText: "Količina puta nabavna cena po SKU, sabrano za pozitivnu zalihu.",
    inputs: ["on-hand quantity", "nabavna cena", "fallback item cost"],
    dataSource: ["Inventory list snapshot", "historical cost", "fallback item cost"],
    caveats: ["Vrednost zavisi od kvaliteta i dostupnosti nabavne cene.", "Nije knjigovodstveni zaključak niti finalna valuacija."],
    blockedWhen: ["Nema pouzdane nabavne cene za značajan deo asortimana."],
    relatedDataQualityChecks: ["Missing cost", "Estimated cost coverage", "Inventory freshness"],
  },
  missingCostRevenueShare: {
    label: "Promet bez nabavne cene",
    businessMeaning: "Pokazuje koliki deo prihoda nema pouzdanu nabavnu cenu i zato slabi maržu i preporuke.",
    formulaText: "Prihod bez dostupne nabavne cene podeljen sa ukupnim prihodom puta 100.",
    inputs: ["revenue without cost", "total revenue"],
    dataSource: ["Data quality health snapshot", "margin coverage checks"],
    caveats: ["Visok procenat direktno smanjuje pouzdanost maržnog doprinosa.", "Može varirati po filteru i po osvežavanju troškova."],
    blockedWhen: ["Health snapshot nije dostupan."],
    relatedDataQualityChecks: ["Promet bez nabavne cene", "Direct cost coverage", "Estimated cost coverage"],
  },
  unknownSupplierRevenueShare: {
    label: "Promet nepoznatog dobavljača",
    businessMeaning: "Koliki deo prihoda odlazi u unknown supplier bucket i zato se ne može pouzdano raspodeliti po dobavljačima.",
    formulaText: "Prihod artikala bez povezanog dobavljača podeljen sa ukupnim prihodom puta 100.",
    inputs: ["unknown supplier revenue", "total revenue"],
    dataSource: ["Data quality health snapshot", "supplier mapping checks"],
    caveats: ["Može narušiti supplier scorecard i supplier pregled čak i kada je ukupan prihod tačan.", "Problem je često rešiv mapiranjem master podataka."],
    blockedWhen: ["Supplier mapping checks nisu dostupni."],
    relatedDataQualityChecks: ["Artikli bez dobavljača", "Promet nepoznatog dobavljača"],
  },
};

const analyticsMetricAliases: Partial<Record<AnalyticsMetricKey, string[]>> = {
  revenue: ["Ukupan prihod", "Prihod", "Promet"],
  marginContribution: ["Ukupan maržni doprinos", "Maržni doprinos"],
  stockRiskCapital: ["Kapital u riziku", "Lager u riziku"],
  dataReadinessScore: ["Kvalitet podataka", "Data quality score", "Spremnost podataka"],
  topSupplierRevenueShare: ["Udeo top 5 dobavljača"],
  fullPriceShareChange: ["Promena udela pune cene"],
  lostSalesEstimate: ["Procena izgubljene prodaje"],
  slowStockCapital: ["Kapital u sporoj zalihi"],
  activeSkuShare: ["Aktivni SKU"],
  inventoryHealthScore: ["Stanje fonda"],
  skuCount: ["Ukupno SKU"],
  onHandUnits: ["Ukupno na stanju"],
  lowStockCount: ["Niska zaliha"],
  avgUnitsPerSku: ["Prosečno po SKU", "Prosecno po SKU"],
  inventoryTotalValue: ["Procena vrednosti"],
  missingCostRevenueShare: ["Promet bez nabavne cene"],
  unknownSupplierRevenueShare: ["Promet nepoznatog dobavljača"],
};

export function getAnalyticsMetricDefinition(metricKey: AnalyticsMetricKey): AnalyticsMetricDefinition {
  return analyticsMetricDefinitions[metricKey];
}

export function findAnalyticsMetricKeyByLabel(label: string | null | undefined): AnalyticsMetricKey | null {
  if (!label) return null;
  const normalized = label.trim().toLocaleLowerCase("sr-Latn-RS");
  const directMatch = (Object.keys(analyticsMetricDefinitions) as AnalyticsMetricKey[]).find(
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
