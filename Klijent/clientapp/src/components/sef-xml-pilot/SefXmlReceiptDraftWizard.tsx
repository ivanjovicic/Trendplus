import { useEffect, useRef, useState, type ChangeEvent } from "react";
import {
  previewSefXmlInvoice,
  reconcileSefXmlInvoice,
  SefXmlPilotApiError,
  type SefXmlInvoicePreview,
  type SefXmlManualMatchSelection,
  type SefXmlReconciliationLine,
  type SefXmlReconciliationPreview,
} from "../../services/sefXmlPilotApi";
import {
  buildSefXmlDraftCsv,
  type SefXmlDraftMetadata,
} from "./sefXmlDraftCsv";
import { getSefXmlMatchPresentation, isValidPhysicalQuantity } from "./sefXmlDraftPresentation";
import "./SefXmlReceiptDraftWizard.css";

type WizardPhase = "empty" | "previewing" | "reconciling" | "ready" | "error";
type FailedStep = "preview" | "reconcile" | null;

interface SefXmlReceiptDraftWizardProps {
  /** Supplied by an existing authorized parent; this component never persists the key. */
  adminKey?: string;
}

const MAX_XML_BYTES = 5 * 1024 * 1024;
const DEFAULT_METADATA: SefXmlDraftMetadata = {
  documentNumber: "",
  issueDate: "",
  supplierName: "",
  storeLocation: "",
};

function safeErrorMessage(error: unknown): string {
  return error instanceof SefXmlPilotApiError
    ? error.message
    : "SEF XML pregled nije uspeo. Pokušajte ponovo ili proverite okruženje.";
}

function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === "AbortError";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object";
}

function isInvoicePreview(value: unknown): value is SefXmlInvoicePreview {
  if (!isRecord(value) || value.isReadOnlyPreview !== true || !Array.isArray(value.lines)) return false;
  if (value.documentKind !== 0 && value.documentKind !== "Invoice") return false;
  const totals = value.totals;
  if (!isRecord(value.supplier) || !isRecord(totals)) return false;
  return ["lineExtensionAmount", "taxExclusiveAmount", "taxInclusiveAmount", "payableAmount"]
    .every((key) => isRecord(totals[key]));
}

function isReadOnlyReconciliation(value: unknown): value is SefXmlReconciliationPreview {
  return isRecord(value)
    && value.isReadOnlyPreview === true
    && value.canPostInventory === false
    && Array.isArray(value.lines)
    && isRecord(value.supplier);
}

function csvCellValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === "" ? "—" : String(value);
}

function formatAmount(value: number | null | undefined, currency: string | null | undefined): string {
  if (value === null || value === undefined) return "Nije navedeno";
  return `${value}${currency ? ` ${currency}` : " (valuta nije navedena)"}`;
}

function statusFor(line: SefXmlReconciliationLine) {
  return getSefXmlMatchPresentation(line.status);
}

function rowTitle(line: SefXmlReconciliationLine): string {
  return `Stavka ${line.sourceOrder}${line.sourceName ? `: ${line.sourceName}` : ""}`;
}

function manualSelectionsFor(
  selections: Record<number, number>,
  sourceOrder: number,
  catalogArticleId?: number,
): SefXmlManualMatchSelection[] {
  return Object.entries({ ...selections, [sourceOrder]: catalogArticleId })
    .flatMap(([order, articleId]) => articleId === undefined ? [] : [{ sourceOrder: Number(order), catalogArticleId: articleId }])
    .sort((left, right) => left.sourceOrder - right.sourceOrder);
}

function downloadCsv(csv: string): void {
  const blobUrl = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = blobUrl;
  link.download = "sef-xml-nacrt.csv";
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(blobUrl);
}

export default function SefXmlReceiptDraftWizard({ adminKey }: SefXmlReceiptDraftWizardProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const requestControllerRef = useRef<AbortController | null>(null);
  const requestSequenceRef = useRef(0);
  const [phase, setPhase] = useState<WizardPhase>("empty");
  const [failedStep, setFailedStep] = useState<FailedStep>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [invoice, setInvoice] = useState<SefXmlInvoicePreview | null>(null);
  const [reconciliation, setReconciliation] = useState<SefXmlReconciliationPreview | null>(null);
  const [metadata, setMetadata] = useState<SefXmlDraftMetadata>(DEFAULT_METADATA);
  const [physicalQuantities, setPhysicalQuantities] = useState<Record<number, string>>({});
  const [physicalQuantityConfirmed, setPhysicalQuantityConfirmed] = useState<Record<number, boolean>>({});
  const [candidateSelections, setCandidateSelections] = useState<Record<number, string>>({});
  const [manualSelections, setManualSelections] = useState<Record<number, number>>({});
  const [pendingManualSelections, setPendingManualSelections] = useState<SefXmlManualMatchSelection[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isLoading = phase === "previewing" || phase === "reconciling";
  const hasAdminKey = Boolean(adminKey?.trim());

  useEffect(() => () => requestControllerRef.current?.abort(), []);

  function clearDraft(clearFile = true) {
    requestSequenceRef.current += 1;
    requestControllerRef.current?.abort();
    requestControllerRef.current = null;
    setPhase("empty");
    setFailedStep(null);
    setInvoice(null);
    setReconciliation(null);
    setMetadata(DEFAULT_METADATA);
    setPhysicalQuantities({});
    setPhysicalQuantityConfirmed({});
    setCandidateSelections({});
    setManualSelections({});
    setPendingManualSelections([]);
    setErrorMessage(null);
    if (clearFile) {
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  function handleFileSelection(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0] ?? null;
    event.currentTarget.value = "";
    clearDraft(false);
    if (!file) {
      setSelectedFile(null);
      return;
    }

    if (!file.name.toLowerCase().endsWith(".xml")) {
      setSelectedFile(null);
      setPhase("error");
      setFailedStep("preview");
      setErrorMessage("Izaberite jedan XML fajl sa nastavkom .xml.");
      return;
    }
    if (file.size <= 0) {
      setSelectedFile(null);
      setPhase("error");
      setFailedStep("preview");
      setErrorMessage("Izabrani XML fajl je prazan.");
      return;
    }
    if (file.size > MAX_XML_BYTES) {
      setSelectedFile(null);
      setPhase("error");
      setFailedStep("preview");
      setErrorMessage("XML fajl je veći od dozvoljene granice pilota.");
      return;
    }
    setSelectedFile(file);
  }

  async function reconcilePreview(
    nextInvoice: SefXmlInvoicePreview,
    selections: SefXmlManualMatchSelection[],
    controller: AbortController,
    requestId: number,
  ): Promise<boolean> {
    setPhase("reconciling");
    setFailedStep(null);
    setErrorMessage(null);
    try {
      const result = await reconcileSefXmlInvoice({
        invoice: nextInvoice,
        manualSelections: selections,
        dataScope: "all",
      }, adminKey, controller.signal);
      if (requestSequenceRef.current !== requestId) return false;
      if (!isReadOnlyReconciliation(result)) {
        throw new SefXmlPilotApiError(null, "Odgovor nije potvrđen kao read-only pregled; nacrt nije nastavljen.");
      }
      setReconciliation(result);
      setManualSelections(Object.fromEntries(selections.map((selection) => [selection.sourceOrder, selection.catalogArticleId])));
      setPendingManualSelections(selections);
      setPhase("ready");
      setFailedStep(null);
      setErrorMessage(null);
      return true;
    } catch (error) {
      if (isAbortError(error) || requestSequenceRef.current !== requestId) return false;
      setPhase("error");
      setFailedStep("reconcile");
      setErrorMessage(safeErrorMessage(error));
      return false;
    }
  }

  async function startPreview() {
    if (!selectedFile || !hasAdminKey || isLoading) return;
    requestControllerRef.current?.abort();
    const controller = new AbortController();
    requestControllerRef.current = controller;
    const requestId = ++requestSequenceRef.current;
    setPhase("previewing");
    setFailedStep(null);
    setErrorMessage(null);
    try {
      const result = await previewSefXmlInvoice(selectedFile, adminKey, controller.signal);
      if (requestSequenceRef.current !== requestId) return;
      if (!isInvoicePreview(result)) {
        throw new SefXmlPilotApiError(null, "XML pregled nije u očekivanom obliku; nacrt nije nastavljen.");
      }
      setInvoice(result);
      setMetadata({
        documentNumber: result.invoiceNumber ?? "",
        issueDate: result.issueDate ?? "",
        supplierName: result.supplier.name ?? "",
        storeLocation: "",
      });
      setPhase("reconciling");
      await reconcilePreview(result, [], controller, requestId);
    } catch (error) {
      if (isAbortError(error) || requestSequenceRef.current !== requestId) return;
      setPhase("error");
      setFailedStep("preview");
      setErrorMessage(safeErrorMessage(error));
    }
  }

  async function retry() {
    if (!hasAdminKey || isLoading) return;
    if (invoice && failedStep === "reconcile") {
      requestControllerRef.current?.abort();
      const controller = new AbortController();
      requestControllerRef.current = controller;
      const requestId = ++requestSequenceRef.current;
      await reconcilePreview(invoice, pendingManualSelections, controller, requestId);
      return;
    }
    await startPreview();
  }

  async function applyManualSelection(line: SefXmlReconciliationLine) {
    if (!invoice || !reconciliation || !hasAdminKey || isLoading) return;
    const selectedValue = candidateSelections[line.sourceOrder];
    const selectedCandidate = line.candidates.find((candidate) => String(candidate.catalogArticleId) === selectedValue);
    if (!selectedCandidate) return;

    const selections = manualSelectionsFor(manualSelections, line.sourceOrder, selectedCandidate.catalogArticleId);
    setPendingManualSelections(selections);
    requestControllerRef.current?.abort();
    const controller = new AbortController();
    requestControllerRef.current = controller;
    const requestId = ++requestSequenceRef.current;
    await reconcilePreview(invoice, selections, controller, requestId);
  }

  function updateMetadata(field: keyof SefXmlDraftMetadata, value: string) {
    setMetadata((current) => ({ ...current, [field]: value }));
  }

  function updateQuantity(sourceOrder: number, value: string) {
    setPhysicalQuantities((current) => ({ ...current, [sourceOrder]: value }));
    setPhysicalQuantityConfirmed((current) => ({ ...current, [sourceOrder]: false }));
  }

  function exportDraft() {
    if (!invoice || !reconciliation || phase !== "ready") return;
    const csv = buildSefXmlDraftCsv({
      invoice,
      reconciliation,
      metadata,
      physicalQuantities,
      physicalQuantityConfirmed,
    });
    downloadCsv(csv);
  }

  const orderedLines = reconciliation
    ? [...reconciliation.lines].sort((left, right) => left.sourceOrder - right.sourceOrder)
    : [];

  return (
    <section className="sef-xml-pilot form-page" aria-labelledby="sef-xml-pilot-title">
      <header className="form-page__header sef-xml-pilot__header">
        <div>
          <h2 className="form-page__title" id="sef-xml-pilot-title">Uvezi iz XML-a</h2>
          <p className="form-page__subtitle">
            Pregled fakture i lokalni nacrt za proveru. Ovaj pilot ne knjiži robu i ne menja Access.
          </p>
        </div>
        <span className="form-section__status form-section__status--warning">NEKNJIŽENO — NACRT</span>
      </header>

      <div className="sef-xml-pilot__safety" role="note">
        <strong>Pregled i nacrt — bez knjiženja.</strong>
        <span>Ne upisuje prijem, artikle, zalihe, dnevnik promena niti produkcioni Access.</span>
      </div>

      {!hasAdminKey && (
        <p className="sef-xml-pilot__notice" role="status">
          Za pregled je potrebna postojeća autorizacija aplikacije. Ova komponenta ne traži niti čuva admin ključ.
        </p>
      )}

      <section className="form-section sef-xml-pilot__upload" aria-labelledby="sef-xml-upload-title">
        <div className="form-section__header">
          <h3 className="form-section__title" id="sef-xml-upload-title">1. Izaberite jednu XML fakturu</h3>
          <span className="form-section__status">Najviše 5 MiB</span>
        </div>
        <label className="form-field" htmlFor="sef-xml-pilot-file">
          <span className="form-label">SEF XML fajl</span>
          <input
            ref={fileInputRef}
            className="form-control"
            id="sef-xml-pilot-file"
            type="file"
            accept=".xml,application/xml,text/xml"
            aria-label="SEF XML fajl"
            disabled={isLoading}
            onChange={handleFileSelection}
          />
          <span className="form-helper">Datoteka se šalje samo radi ovog read-only pregleda; ne čuva se u pregledaču.</span>
        </label>
        {selectedFile && <p className="sef-xml-pilot__file-name">Izabrano: {selectedFile.name}</p>}
        <div className="sef-xml-pilot__actions">
          <button className="btn btn--primary" type="button" disabled={!selectedFile || !hasAdminKey || isLoading} onClick={() => void startPreview()}>
            {phase === "previewing" ? "Učitavam XML…" : "Pregledaj XML"}
          </button>
          {selectedFile && (
            <button className="btn btn--secondary" type="button" disabled={isLoading} onClick={() => clearDraft()}>
              Obriši nacrt
            </button>
          )}
        </div>
      </section>

      {errorMessage && (
        <div className="sef-xml-pilot__error" role="alert">
          <p>{errorMessage}</p>
          {selectedFile && hasAdminKey && (
            <button className="btn btn--secondary" type="button" disabled={isLoading} onClick={() => void retry()}>
              {failedStep === "reconcile" ? "Ponovi uparivanje" : "Pokušaj ponovo"}
            </button>
          )}
        </div>
      )}

      {isLoading && (
        <p className="sef-xml-pilot__progress" role="status" aria-live="polite">
          {phase === "previewing" ? "Čitam XML za privremeni pregled…" : "Proveravam kandidate u katalogu…"}
        </p>
      )}

      {phase === "ready" && (
        <p className="sef-xml-pilot__progress" role="status" aria-live="polite">
          XML je privremeno pregledan. Nacrt nije proknjižen.
        </p>
      )}

      {invoice && reconciliation && (
        <div className="sef-xml-pilot__draft">
          <section className="form-section" aria-labelledby="sef-xml-metadata-title">
            <div className="form-section__header">
              <h3 className="form-section__title" id="sef-xml-metadata-title">2. Podaci nacrta</h3>
              <span className="form-section__status form-section__status--warning">Nije prijem robe</span>
            </div>
            <p className="form-section__description">Izmenljivo samo u ovoj otvorenoj stranici; izvorne vrednosti fakture ostaju odvojene.</p>
            <div className="form-grid form-grid--two sef-xml-pilot__metadata-grid">
              <label className="form-field">
                <span className="form-label">Broj dokumenta u nacrtu</span>
                <input className="form-control" aria-label="Broj dokumenta u nacrtu" value={metadata.documentNumber} onChange={(event) => updateMetadata("documentNumber", event.target.value)} />
              </label>
              <label className="form-field">
                <span className="form-label">Datum fakture u nacrtu</span>
                <input className="form-control" aria-label="Datum fakture u nacrtu" type="date" value={metadata.issueDate} onChange={(event) => updateMetadata("issueDate", event.target.value)} />
              </label>
              <label className="form-field">
                <span className="form-label">Dobavljač u nacrtu</span>
                <input className="form-control" aria-label="Dobavljač u nacrtu" value={metadata.supplierName} onChange={(event) => updateMetadata("supplierName", event.target.value)} />
              </label>
              <label className="form-field">
                <span className="form-label">Prodavnica / lokacija (ručni unos)</span>
                <input className="form-control" aria-label="Prodavnica / lokacija (ručni unos)" value={metadata.storeLocation} onChange={(event) => updateMetadata("storeLocation", event.target.value)} />
              </label>
            </div>
            <div className="sef-xml-pilot__source-summary">
              <h4>Izvorni iznosi sa fakture — bez preračuna</h4>
              <dl className="sef-xml-pilot__summary-grid">
                <div><dt>Valuta dokumenta</dt><dd>{csvCellValue(invoice.documentCurrencyCode)}</dd></div>
                <div><dt>Osnovica stavki</dt><dd>{formatAmount(invoice.totals.lineExtensionAmount.value, invoice.totals.lineExtensionAmount.currencyCode)}</dd></div>
                <div><dt>Osnovica bez PDV-a</dt><dd>{formatAmount(invoice.totals.taxExclusiveAmount.value, invoice.totals.taxExclusiveAmount.currencyCode)}</dd></div>
                <div><dt>Ukupno sa PDV-om</dt><dd>{formatAmount(invoice.totals.taxInclusiveAmount.value, invoice.totals.taxInclusiveAmount.currencyCode)}</dd></div>
                <div><dt>Za plaćanje</dt><dd>{formatAmount(invoice.totals.payableAmount.value, invoice.totals.payableAmount.currencyCode)}</dd></div>
              </dl>
              <p className="form-helper">Cene, valute, osnovice, porezi, popusti i jedinice mere nisu međusobno preračunati.</p>
            </div>
          </section>

          <section className="form-section" aria-labelledby="sef-xml-lines-title">
            <div className="form-section__header">
              <h3 className="form-section__title" id="sef-xml-lines-title">3. Stavke za proveru</h3>
              <span className="form-section__status">{orderedLines.length} stavki</span>
            </div>
            {reconciliation.accessReceiptVerificationReason && (
              <p className="sef-xml-pilot__notice">{reconciliation.accessReceiptVerificationReason}</p>
            )}
            <div className="sef-xml-pilot__line-list">
              {orderedLines.map((line) => {
                const presentation = statusFor(line);
                const sourceLine = invoice.lines.find((source) => source.sourceOrder === line.sourceOrder);
                const quantity = physicalQuantities[line.sourceOrder] ?? "";
                const quantityConfirmed = Boolean(physicalQuantityConfirmed[line.sourceOrder] && isValidPhysicalQuantity(quantity));
                const candidateValue = candidateSelections[line.sourceOrder]
                  ?? (manualSelections[line.sourceOrder] === undefined ? "" : String(manualSelections[line.sourceOrder]));
                const hasCandidates = line.candidates.length > 0;
                return (
                  <article className="sef-xml-pilot__line" key={`${line.sourceOrder}-${line.sourceXmlLineId ?? "source"}`} aria-labelledby={`sef-xml-line-${line.sourceOrder}`}>
                    <div className="sef-xml-pilot__line-heading">
                      <div>
                        <h4 id={`sef-xml-line-${line.sourceOrder}`}>{rowTitle(line)}</h4>
                        <p className="form-helper">Izvorni XML red {line.sourceOrder}{line.sourceXmlLineId ? ` · ID ${line.sourceXmlLineId}` : ""}</p>
                      </div>
                      <span className={`form-section__status ${presentation.exactCodeNeedsReview ? "form-section__status--warning" : ""}`}>
                        {presentation.label}
                      </span>
                    </div>

                    {presentation.exactCodeNeedsReview && (
                      <p className="sef-xml-pilot__identity-warning" role="note">
                        Ovo je samo tekstualno poklapanje. Nije dokaz da dobavljač i Trendplus koriste isti identifikacioni sistem, niti da su Trendplus PLU, veličina i model isti. Proverite artikal i fizički par.
                      </p>
                    )}
                    {String(line.status) === "1" || String(line.status) === "ExplicitManualMatch" ? (
                      <p className="sef-xml-pilot__notice" role="note">
                        Izbor je ručni; i dalje proverite identitet, veličinu i model prema robi i dokumentu.
                      </p>
                    ) : null}

                    <dl className="sef-xml-pilot__line-data">
                      <div><dt>Šifra dobavljača</dt><dd>{csvCellValue(line.sourceSupplierItemId)}</dd></div>
                      <div><dt>Šifra kupca / standard</dt><dd>{[line.sourceBuyerItemId, line.sourceStandardItemId].filter(Boolean).join(" / ") || "Nije navedeno"}</dd></div>
                      <div><dt>Jedinica mere iz XML-a</dt><dd>{csvCellValue(line.rawUnitCode)}</dd></div>
                      <div><dt>Fakturisana količina</dt><dd>{csvCellValue(line.invoicedQuantity)}</dd></div>
                      <div><dt>Cena po osnovi iz izvora</dt><dd>{formatAmount(line.priceAmount, line.priceCurrencyCode)}{sourceLine?.priceBaseQuantity !== null && sourceLine?.priceBaseQuantity !== undefined ? ` / ${sourceLine.priceBaseQuantity} jedinica` : ""}</dd></div>
                      <div><dt>Neto iznos stavke</dt><dd>{formatAmount(line.netLineAmount, line.netLineCurrencyCode)}</dd></div>
                      <div className="sef-xml-pilot__line-wide"><dt>Osnova cene iz XML-a</dt><dd>{line.sourcePriceBasisExplanation || "Nije navedena"}</dd></div>
                      <div className="sef-xml-pilot__line-wide"><dt>Poređenje cene</dt><dd>{line.priceComparison.reason || "Cena nije poređena."}</dd></div>
                      {line.allowances.length > 0 && (
                        <div className="sef-xml-pilot__line-wide">
                          <dt>Rabati / dodatni iznosi iz izvora</dt>
                          <dd>{line.allowances.map((allowance) => `${allowance.isCharge === true ? "Dodatak" : allowance.isCharge === false ? "Rabat" : "Iznos"}: ${formatAmount(allowance.amount, allowance.currencyCode)}${allowance.baseAmount === null ? "" : ` na osnovu ${allowance.baseAmount}`}`).join("; ")}</dd>
                        </div>
                      )}
                    </dl>

                    <div className="sef-xml-pilot__match-reason">
                      <strong>Razlog:</strong> {line.matchReason || "Nema dodatnog objašnjenja."}
                      {line.selectedCatalogName && <span> · Izabrani katalog: {line.selectedCatalogName}{line.selectedCatalogExternalArticleId ? ` (${line.selectedCatalogExternalArticleId})` : ""}</span>}
                    </div>

                    <div className="sef-xml-pilot__manual-match">
                      <label className="form-field" htmlFor={`sef-xml-candidate-${line.sourceOrder}`}>
                        <span className="form-label">Ručni izbor artikla za XML red {line.sourceOrder}</span>
                        <select
                          className="form-control"
                          id={`sef-xml-candidate-${line.sourceOrder}`}
                          value={candidateValue}
                          aria-label={`Ručni izbor artikla za XML red ${line.sourceOrder}`}
                          disabled={!hasAdminKey || isLoading || !hasCandidates}
                          onChange={(event) => setCandidateSelections((current) => ({ ...current, [line.sourceOrder]: event.target.value }))}
                        >
                          <option value="">{hasCandidates ? "Izaberite kataloškog kandidata" : "Nema dostupnih kandidata"}</option>
                          {line.candidates.map((candidate) => (
                            <option key={candidate.catalogArticleId} value={candidate.catalogArticleId}>
                              {candidate.name || "Artikal bez naziva"}{candidate.externalArticleId ? ` · šifra ${candidate.externalArticleId}` : ""} · ID {candidate.catalogArticleId}
                            </option>
                          ))}
                        </select>
                        {!hasCandidates && <span className="form-helper">Ne birajte artikal po nazivu ili veličini bez provere; katalog nije vratio izbor.</span>}
                      </label>
                      <button
                        className="btn btn--secondary"
                        type="button"
                        disabled={!candidateValue || !hasCandidates || !hasAdminKey || isLoading}
                        onClick={() => void applyManualSelection(line)}
                      >
                        Primeni ručni izbor
                      </button>
                    </div>

                    <div className="sef-xml-pilot__quantity">
                      <label className="form-field" htmlFor={`sef-xml-quantity-${line.sourceOrder}`}>
                        <span className="form-label">Stvarno primljeno za XML red {line.sourceOrder}</span>
                        <input
                          className="form-control"
                          id={`sef-xml-quantity-${line.sourceOrder}`}
                          type="text"
                          inputMode="decimal"
                          value={quantity}
                          aria-label={`Stvarno primljeno za XML red ${line.sourceOrder}`}
                          placeholder="Unesite nakon fizičke provere"
                          aria-describedby={`sef-xml-quantity-help-${line.sourceOrder}`}
                          onChange={(event) => updateQuantity(line.sourceOrder, event.target.value)}
                        />
                        <span className="form-helper" id={`sef-xml-quantity-help-${line.sourceOrder}`}>
                          Odvojeno od fakturisane količine; nema automatskog popunjavanja ni pretvaranja jedinica.
                        </span>
                      </label>
                      <label className="sef-xml-pilot__confirmation">
                        <input
                          type="checkbox"
                          checked={quantityConfirmed}
                          disabled={!isValidPhysicalQuantity(quantity)}
                          onChange={(event) => setPhysicalQuantityConfirmed((current) => ({ ...current, [line.sourceOrder]: event.target.checked }))}
                        />
                        <span>Potvrđujem fizički prebrojanu količinu</span>
                      </label>
                      <p className={`sef-xml-pilot__quantity-status ${quantityConfirmed ? "is-confirmed" : ""}`}>
                        {quantityConfirmed ? `Operater je potvrdio: ${quantity}` : "Nije potvrđeno"}
                      </p>
                    </div>

                    <p className="sef-xml-pilot__line-draft-status">NEKNJIŽENO — NACRT</p>
                  </article>
                );
              })}
            </div>
          </section>

          <div className="sef-xml-pilot__footer">
            <p>Ovaj nacrt je privremen i postoji samo u memoriji ove stranice. Nijedan podatak nije sačuvan u prijemu robe ili zalihama.</p>
            <button className="btn btn--primary" type="button" disabled={phase !== "ready" || orderedLines.length === 0} onClick={exportDraft}>
              Preuzmi CSV nacrt
            </button>
          </div>
        </div>
      )}
    </section>
  );
}
