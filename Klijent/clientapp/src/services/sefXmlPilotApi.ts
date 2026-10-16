import { apiUrl } from "../utils/apiUrl";

export type SefXmlEnumValue = number | string;

export interface SefXmlDiagnostic {
  code: string;
  message: string;
}

export interface SefXmlAmount {
  value: number | null;
  currencyCode: string | null;
}

export interface SefXmlAllowanceCharge {
  sourceOrder: number;
  isCharge: boolean | null;
  amount: number | null;
  currencyCode: string | null;
  baseAmount: number | null;
  multiplierFactor: number | null;
}

export interface SefXmlInvoiceLine {
  sourceOrder: number;
  sourceLineId: string | null;
  supplierItemId: string | null;
  buyerItemId: string | null;
  standardItemId: string | null;
  name: string | null;
  rawUnitCode: string | null;
  invoicedQuantity: number | null;
  physicallyReceivedQuantity: number | null;
  priceAmount: number | null;
  priceCurrencyCode: string | null;
  priceBaseQuantity: number | null;
  netLineAmount: number | null;
  netLineCurrencyCode: string | null;
  allowances: SefXmlAllowanceCharge[];
  priceBasisExplanation: string;
  diagnostics: SefXmlDiagnostic[];
}

export interface SefXmlInvoicePreview {
  isReadOnlyPreview: boolean;
  documentKind: SefXmlEnumValue;
  invoiceNumber: string | null;
  issueDate: string | null;
  invoiceTypeCode: string | null;
  documentCurrencyCode: string | null;
  supplier: { name: string | null; companyId: string | null };
  lines: SefXmlInvoiceLine[];
  totals: {
    lineExtensionAmount: SefXmlAmount;
    taxExclusiveAmount: SefXmlAmount;
    taxInclusiveAmount: SefXmlAmount;
    payableAmount: SefXmlAmount;
  };
  taxSubtotals: Array<{
    sourceOrder: number;
    taxableAmount: SefXmlAmount;
    taxAmount: SefXmlAmount;
    percent: number | null;
    taxCategoryCode: string | null;
  }>;
  diagnostics: SefXmlDiagnostic[];
}

export interface SefXmlManualMatchSelection {
  sourceOrder: number;
  catalogArticleId: number;
}

export interface SefXmlReconciliationRequest {
  invoice: SefXmlInvoicePreview;
  manualSelections: SefXmlManualMatchSelection[];
  dataScope: "all" | "imported" | "existing";
}

export interface SefXmlReconciliationLine {
  sourceOrder: number;
  sourceXmlLineId: string | null;
  sourceDocumentNumber: string | null;
  sourceSupplierItemId: string | null;
  sourceBuyerItemId: string | null;
  sourceStandardItemId: string | null;
  sourceName: string | null;
  rawUnitCode: string | null;
  invoicedQuantity: number | null;
  physicallyReceivedQuantity: number | null;
  priceAmount: number | null;
  priceCurrencyCode: string | null;
  netLineAmount: number | null;
  netLineCurrencyCode: string | null;
  allowances: SefXmlAllowanceCharge[];
  sourcePriceBasisExplanation: string;
  status: SefXmlEnumValue;
  confidence: SefXmlEnumValue;
  matchReasonCode: string;
  matchReason: string;
  selectedCatalogArticleId: number | null;
  selectedCatalogExternalArticleId: string | null;
  selectedCatalogName: string | null;
  candidates: Array<{
    catalogArticleId: number;
    externalArticleId: string | null;
    name: string | null;
  }>;
  priceComparison: {
    status: SefXmlEnumValue;
    reason: string;
    difference: number | null;
  };
}

export interface SefXmlReconciliationPreview {
  isReadOnlyPreview: boolean;
  canPostInventory: boolean;
  sourceDocumentNumber: string | null;
  sourceDocumentCurrencyCode: string | null;
  supplier: { name: string | null; companyId: string | null };
  effectiveDataScope: string;
  accessReceiptVerificationStatus: SefXmlEnumValue;
  accessReceiptVerificationReason: string;
  lines: SefXmlReconciliationLine[];
  diagnostics: SefXmlDiagnostic[];
}

export class SefXmlPilotApiError extends Error {
  constructor(
    readonly status: number | null,
    message: string,
  ) {
    super(message);
    this.name = "SefXmlPilotApiError";
  }
}

const SAFE_HTTP_MESSAGES: Record<number, string> = {
  400: "XML faktura nije podržana ili nije moguće napraviti pregled. Proverite fajl.",
  401: "Niste ovlašćeni za SEF XML pilot.",
  403: "Niste ovlašćeni za SEF XML pilot.",
  404: "SEF XML pilot nije uključen u ovom okruženju.",
  413: "XML fajl je veći od dozvoljene granice pilota.",
  422: "XML faktura nije podržana ili nije moguće napraviti pregled. Proverite fajl.",
  429: "Previše zahteva za SEF XML pilot. Sačekajte pa pokušajte ponovo.",
  503: "SEF XML pilot trenutno nije dostupan. Pokušajte ponovo.",
};

const NETWORK_ERROR_MESSAGE = "Veza sa SEF XML servisom nije dostupna. Proverite vezu pa pokušajte ponovo.";
const INVALID_RESPONSE_MESSAGE = "SEF XML servis je vratio odgovor koji nije moguće bezbedno prikazati.";

function adminHeaders(adminKey?: string): Record<string, string> {
  const key = adminKey?.trim();
  return key ? { "X-Admin-Key": key } : {};
}

async function postJson<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(apiUrl(path), init);
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    throw new SefXmlPilotApiError(null, NETWORK_ERROR_MESSAGE);
  }

  if (!response.ok) {
    throw new SefXmlPilotApiError(
      response.status,
      SAFE_HTTP_MESSAGES[response.status] ?? "SEF XML pregled nije uspeo. Pokušajte ponovo ili proverite okruženje.",
    );
  }

  try {
    return await response.json() as T;
  } catch {
    throw new SefXmlPilotApiError(response.status, INVALID_RESPONSE_MESSAGE);
  }
}

export function previewSefXmlInvoice(
  file: File,
  adminKey?: string,
  signal?: AbortSignal,
): Promise<SefXmlInvoicePreview> {
  const body = new FormData();
  body.append("file", file, file.name);

  return postJson<SefXmlInvoicePreview>("/api/pilot/sef-xml/preview", {
    method: "POST",
    headers: adminHeaders(adminKey),
    body,
    signal,
    cache: "no-store",
  });
}

export function reconcileSefXmlInvoice(
  request: SefXmlReconciliationRequest,
  adminKey?: string,
  signal?: AbortSignal,
): Promise<SefXmlReconciliationPreview> {
  return postJson<SefXmlReconciliationPreview>("/api/pilot/sef-xml/reconcile", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...adminHeaders(adminKey),
    },
    body: JSON.stringify(request),
    signal,
    cache: "no-store",
  });
}
