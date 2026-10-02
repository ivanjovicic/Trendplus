import type { SupplierTab } from "../pages/supplierSharedState";

/** Compact cross-tab role cue (RQ500). */
export const SUPPLIER_TAB_ROLE_CUE: Record<SupplierTab, string> = {
  overview:
    "Pregled meri sertifikovanu prodaju u periodu i nosi konačnu preporuku.",
  scorecard:
    "Skorkarta meri kvalitet prodaje (puna cena, nivelacije, rizik) u kohorti prve nivelacije — pomoćni signal.",
  assortment:
    "Asortiman meri tip obuće, uporedivu pre/post kohortu nivelacija i elastičnost — objašnjenje, ne finalna odluka.",
};

export const SUPPLIER_SCORECARD_SECONDARY_KPI_TITLE = "Kontekst kohorte skorkarte (sekundarno)";

export const SUPPLIER_ASSORTMENT_SECONDARY_KPI_TITLE = "Generički promet asortimana (sekundarno)";
