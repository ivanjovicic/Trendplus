import type {
  SefXmlInvoicePreview,
  SefXmlReconciliationPreview,
} from "../../services/sefXmlPilotApi";
import { getSefXmlMatchPresentation, isValidPhysicalQuantity } from "./sefXmlDraftPresentation";

export interface SefXmlDraftMetadata {
  documentNumber: string;
  issueDate: string;
  supplierName: string;
  storeLocation: string;
}

export interface SefXmlDraftCsvInput {
  invoice: SefXmlInvoicePreview;
  reconciliation: SefXmlReconciliationPreview;
  metadata: SefXmlDraftMetadata;
  physicalQuantities: Record<number, string>;
  physicalQuantityConfirmed: Record<number, boolean>;
}

const CSV_HEADERS = [
  "Status",
  "Red XML-a",
  "Broj dokumenta u nacrtu",
  "Datum fakture u nacrtu",
  "Dobavljač u nacrtu",
  "Prodavnica ili lokacija u nacrtu",
  "Broj izvorne fakture",
  "Datum izvorne fakture",
  "Šifra vrste dokumenta iz XML-a",
  "Valuta dokumenta iz XML-a",
  "PIB dobavljača iz XML-a",
  "Izvorni broj dokumenta stavke",
  "ID stavke u XML-u",
  "Šifra dobavljača iz XML-a",
  "Šifra kupca iz XML-a",
  "Standardna šifra iz XML-a",
  "Naziv artikla iz XML-a",
  "Jedinica mere iz XML-a",
  "Fakturisana količina iz XML-a",
  "Stvarno primljena količina u nacrtu",
  "Status fizičke količine",
  "Status uparivanja",
  "Način uparivanja",
  "ID izabranog artikla u katalogu",
  "Spoljna šifra iz kataloga",
  "Naziv artikla iz kataloga",
  "Razlog uparivanja",
  "Cena iz XML-a",
  "Valuta cene iz XML-a",
  "Osnovna količina cene iz XML-a",
  "Objašnjenje cene iz izvora",
  "Neto iznos stavke iz XML-a",
  "Valuta neto iznosa iz XML-a",
  "Rabati i dodaci iz izvora JSON",
  "Razlog poređenja cene",
  "Osnovica stavki iz XML-a",
  "Valuta osnovice stavki",
  "Osnovica bez PDV-a iz XML-a",
  "Valuta osnovice bez PDV-a",
  "Ukupno sa PDV-om iz XML-a",
  "Valuta ukupnog iznosa sa PDV-om",
  "Za plaćanje iz XML-a",
  "Valuta iznosa za plaćanje",
  "Poreske stavke iz XML-a JSON",
].join(",");

function raw(value: string | number | boolean | null | undefined): string {
  return value === null || value === undefined ? "" : String(value);
}

export function protectSpreadsheetFormula(value: string): string {
  const formulaAfterIgnoredPrefix = /^[\s\u0000-\u001f\u007f-\u009f]*[=+\-@]/u.test(value);
  const leadingControlCharacter = /^[\u0000-\u001f\u007f-\u009f]/u.test(value);
  return formulaAfterIgnoredPrefix || leadingControlCharacter ? `'${value}` : value;
}

export function quoteRfc4180Cell(value: string): string {
  const safeValue = protectSpreadsheetFormula(value).replace(/"/g, '""');
  return /[",\r\n]/u.test(safeValue) ? `"${safeValue}"` : safeValue;
}

function allowanceData(value: unknown): string {
  return JSON.stringify(value ?? []);
}

export function buildSefXmlDraftCsv(input: SefXmlDraftCsvInput): string {
  const invoiceLines = new Map(input.invoice.lines.map((line) => [line.sourceOrder, line]));
  const rows = [...input.reconciliation.lines]
    .sort((left, right) => left.sourceOrder - right.sourceOrder)
    .map((line) => {
      const source = invoiceLines.get(line.sourceOrder);
      const match = getSefXmlMatchPresentation(line.status);
      const quantity = input.physicalQuantities[line.sourceOrder] ?? "";
      const quantityConfirmed = Boolean(input.physicalQuantityConfirmed[line.sourceOrder] && isValidPhysicalQuantity(quantity));
      return [
        "NEKNJIŽENO — NACRT",
        raw(line.sourceOrder),
        input.metadata.documentNumber,
        input.metadata.issueDate,
        input.metadata.supplierName,
        input.metadata.storeLocation,
        raw(input.invoice.invoiceNumber),
        raw(input.invoice.issueDate),
        raw(input.invoice.invoiceTypeCode),
        raw(input.invoice.documentCurrencyCode),
        input.invoice.supplier.companyId,
        raw(line.sourceDocumentNumber),
        raw(line.sourceXmlLineId),
        raw(line.sourceSupplierItemId),
        raw(line.sourceBuyerItemId),
        raw(line.sourceStandardItemId),
        raw(line.sourceName),
        raw(line.rawUnitCode),
        raw(line.invoicedQuantity),
        quantity,
        quantityConfirmed ? "POTVRĐENO OD OPERATERA" : "NEPOTVRĐENO — NACRT",
        match.label,
        match.exportSource,
        raw(line.selectedCatalogArticleId),
        raw(line.selectedCatalogExternalArticleId),
        raw(line.selectedCatalogName),
        raw(line.matchReason),
        raw(line.priceAmount),
        raw(line.priceCurrencyCode),
        raw(source?.priceBaseQuantity),
        raw(line.sourcePriceBasisExplanation),
        raw(line.netLineAmount),
        raw(line.netLineCurrencyCode),
        allowanceData(line.allowances),
        raw(line.priceComparison.reason),
        raw(input.invoice.totals.lineExtensionAmount.value),
        raw(input.invoice.totals.lineExtensionAmount.currencyCode),
        raw(input.invoice.totals.taxExclusiveAmount.value),
        raw(input.invoice.totals.taxExclusiveAmount.currencyCode),
        raw(input.invoice.totals.taxInclusiveAmount.value),
        raw(input.invoice.totals.taxInclusiveAmount.currencyCode),
        raw(input.invoice.totals.payableAmount.value),
        raw(input.invoice.totals.payableAmount.currencyCode),
        allowanceData(input.invoice.taxSubtotals),
      ].map((cell) => quoteRfc4180Cell(raw(cell))).join(",");
    });

  return `\uFEFF${CSV_HEADERS}\r\n${rows.length ? `${rows.join("\r\n")}\r\n` : ""}`;
}
