/** Mirrors Application/Analytics/SupplierAssortmentSizeCurveEvidenceContract.cs (RQ532). */

export const SUPPLIER_ASSORTMENT_SIZE_CURVE_CONTRACT_VERSION = "supplier_assortment_size_curve_v1";

export const SUPPLIER_FOOTWEAR_SIZE_CURVE_AVAILABLE = false;

export const SUPPLIER_CONTROLLED_MARKDOWN_UPLIFT_AVAILABLE = false;

export const SUPPLIER_SIZE_CURVE_UNAVAILABLE_TITLE =
  "Raspodela veličina po dobavljaču × tip obuće nije dostupna";

export const SUPPLIER_SIZE_CURVE_UNAVAILABLE_SUMMARY =
  "Inventarski size-curve signal postoji samo na nivou SKU/prodavnice (udeo veličine), bez prodato/primljeno/stanje po veličini agregirano po dobavljaču i tipu obuće.";

export const SUPPLIER_SIZE_CURVE_UNAVAILABLE_REASONS = [
  "Nema repozitorijumski definisanog izvora analytics_size_curve_snapshot.",
  "Postojeći endpoint vraća udeo veličine i kvalitet krive, ne količine prodato/primljeno/stanje.",
  "Nedostaje agregacioni grain dobavljač × tip obuće × veličina.",
] as const;

export const SUPPLIER_CONTROLLED_MARKDOWN_EVIDENCE_TITLE =
  "Kontrolisani efekat nivelacije (DiD) nije prikazan kao uzročan uplift";

export const SUPPLIER_CONTROLLED_MARKDOWN_EVIDENCE_SUMMARY =
  "Tab Asortiman koristi opisni pre/post signal sa zrelošću prozora i kvalitetom podataka. Sirovi pre/post se ne tretira kao uzročan efekat; DiD zahteva odobren pogled, populaciju kontrole i metapodatke poverenja.";

export const SUPPLIER_CONTROLLED_MARKDOWN_EVIDENCE_REASONS = [
  "Efekat promene cene ostaje descriptive signal (recommendationAllowed=false).",
  "vw_nivelacija_did i populacija kontrole nisu deo ovog Supplier Assortment ugovora.",
  "Bez maturity/control/confidence metapodataka DiD prihod/količina se ne prikazuju kao uzročan uplift.",
] as const;
