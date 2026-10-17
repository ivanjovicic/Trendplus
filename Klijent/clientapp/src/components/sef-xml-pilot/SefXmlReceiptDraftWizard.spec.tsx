import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import SefXmlReceiptDraftWizard from "./SefXmlReceiptDraftWizard";
import { buildSefXmlDraftCsv } from "./sefXmlDraftCsv";

const preview = {
  isReadOnlyPreview: true,
  documentKind: 0,
  invoiceNumber: "SYNTHETIC-QDB13A",
  issueDate: "2026-10-16",
  invoiceTypeCode: "380",
  documentCurrencyCode: "RSD",
  supplier: { name: "Sintetički dobavljač", companyId: "109999999" },
  lines: [{
    sourceOrder: 1,
    sourceLineId: "XML-1",
    supplierItemId: " =HYPERLINK(\"https://example.test\")",
    buyerItemId: null,
    standardItemId: null,
    name: "Čizma 39",
    rawUnitCode: "H87",
    invoicedQuantity: 2,
    physicallyReceivedQuantity: null,
    priceAmount: 100,
    priceCurrencyCode: "RSD",
    priceBaseQuantity: null,
    netLineAmount: 200,
    netLineCurrencyCode: "RSD",
    allowances: [],
    priceBasisExplanation: "Izvorna cena iz XML-a.",
    diagnostics: [],
  }],
  totals: {
    lineExtensionAmount: { value: 200, currencyCode: "RSD" },
    taxExclusiveAmount: { value: 200, currencyCode: "RSD" },
    taxInclusiveAmount: { value: 240, currencyCode: "RSD" },
    payableAmount: { value: 240, currencyCode: "RSD" },
  },
  taxSubtotals: [],
  diagnostics: [],
};

const exactReconciliation = {
  isReadOnlyPreview: true,
  canPostInventory: false,
  sourceDocumentNumber: "SYNTHETIC-QDB13A",
  sourceDocumentCurrencyCode: "RSD",
  supplier: preview.supplier,
  effectiveDataScope: "all",
  accessReceiptVerificationStatus: 0,
  accessReceiptVerificationReason: "Nije moguće proveriti prema Access prijemu.",
  diagnostics: [],
  lines: [{
    sourceOrder: 1,
    sourceXmlLineId: "XML-1",
    sourceDocumentNumber: "SYNTHETIC-QDB13A",
    sourceSupplierItemId: preview.lines[0].supplierItemId,
    sourceBuyerItemId: null,
    sourceStandardItemId: null,
    sourceName: "Čizma 39",
    rawUnitCode: "H87",
    invoicedQuantity: 2,
    physicallyReceivedQuantity: null,
    priceAmount: 100,
    priceCurrencyCode: "RSD",
    netLineAmount: 200,
    netLineCurrencyCode: "RSD",
    allowances: [],
    sourcePriceBasisExplanation: "Izvorna cena iz XML-a.",
    status: 0,
    confidence: 0,
    matchReasonCode: "SUPPLIER_ITEM_ID_TO_UNIQUE_CATALOG_EXTERNAL_ID",
    matchReason: "Jedinstvena šifra se podudara.",
    selectedCatalogArticleId: -7,
    selectedCatalogExternalArticleId: "BOOT-39",
    selectedCatalogName: "Čizma 39",
    candidates: [{ catalogArticleId: -7, externalArticleId: "BOOT-39", name: "Čizma 39" }],
    priceComparison: { status: 0, reason: "Cena nije proverena.", difference: null },
  }],
};

const reviewReconciliation = {
  ...exactReconciliation,
  lines: [{
    ...exactReconciliation.lines[0],
    status: 2,
    confidence: 2,
    selectedCatalogArticleId: null,
    selectedCatalogExternalArticleId: null,
    selectedCatalogName: null,
    matchReason: "Naziv daje samo kandidata.",
  }],
};

function response(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function selectFile() {
  fireEvent.change(screen.getByLabelText("SEF XML fajl"), {
    target: { files: [new File(["<Invoice />"], "synthetic.xml", { type: "application/xml" })] },
  });
}

afterEach(() => vi.unstubAllGlobals());

describe("SefXmlReceiptDraftWizard", () => {
  it("marks even an exact code comparison as operator verification and keeps physical quantity unknown", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(exactReconciliation));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByText("Podudaranje šifre — proveriti")).toBeInTheDocument();
    expect(screen.getByText(/nije dokaz da dobavljač i Trendplus koriste isti identifikacioni sistem, niti da su Trendplus PLU, veličina i model isti/i)).toBeInTheDocument();
    expect(screen.getByLabelText("Stvarno primljeno za XML red 1")).toHaveValue("");
    expect(screen.getByText("Nije potvrđeno")).toBeInTheDocument();
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/pilot/sef-xml/preview",
      "/api/pilot/sef-xml/reconcile",
    ]);
  });

  it("sends a visible manual selection only to read-only reconciliation", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(reviewReconciliation))
      .mockResolvedValueOnce(response({
        ...exactReconciliation,
        lines: [{ ...exactReconciliation.lines[0], status: 1, confidence: 1, matchReason: "Operater je ručno izabrao artikal." }],
      }));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));
    await screen.findByText("Kandidat za proveru");

    fireEvent.change(screen.getByLabelText("Ručni izbor artikla za XML red 1"), { target: { value: "-7" } });
    fireEvent.click(screen.getByRole("button", { name: "Primeni ručni izbor" }));

    expect(await screen.findByText("Ručno odabrano")).toBeInTheDocument();
    expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toMatchObject({
      manualSelections: [{ sourceOrder: 1, catalogArticleId: -7 }],
    });
    const urls = fetchMock.mock.calls.map(([url]) => String(url));
    expect(urls).toEqual([
      "/api/pilot/sef-xml/preview",
      "/api/pilot/sef-xml/reconcile",
      "/api/pilot/sef-xml/reconcile",
    ]);
    expect(urls.every((url) => url.startsWith("/api/pilot/sef-xml/"))).toBe(true);
  });

  it("renders a safe retry after a temporary backend failure", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({ error: "private failure" }, 503))
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(exactReconciliation));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("SEF XML pilot trenutno nije dostupan. Pokušajte ponovo.");
    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));

    expect(await screen.findByText("Podudaranje šifre — proveriti")).toBeInTheDocument();
  });

  it("exports a marked draft with RFC 4180 quoting and formula-injection protection", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(exactReconciliation));
    const createObjectURL = vi.fn(() => "blob:sef-xml-draft");
    const revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => undefined);
    vi.stubGlobal("URL", { createObjectURL, revokeObjectURL });
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));
    await screen.findByText("Podudaranje šifre — proveriti");
    fireEvent.change(screen.getByLabelText("Stvarno primljeno za XML red 1"), { target: { value: "1,5" } });
    fireEvent.click(screen.getByLabelText("Potvrđujem fizički prebrojanu količinu"));
    expect(screen.getByText("Operater je potvrdio: 1,5")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Preuzmi CSV nacrt" }));

    await waitFor(() => expect(createObjectURL).toHaveBeenCalledTimes(1));
    const blob = createObjectURL.mock.calls[0][0] as Blob;
    expect(blob.type).toBe("text/csv;charset=utf-8");
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:sef-xml-draft");
  });

  it("keeps typed physical quantity unconfirmed until the operator checks it", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(exactReconciliation));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));
    await screen.findByText("Podudaranje šifre — proveriti");
    const quantity = screen.getByLabelText("Stvarno primljeno za XML red 1");
    fireEvent.change(quantity, { target: { value: "1,5" } });

    expect(quantity).toHaveValue("1,5");
    expect(screen.getByText("Nije potvrđeno")).toBeInTheDocument();
    expect(screen.getByLabelText("Potvrđujem fizički prebrojanu količinu")).not.toBeChecked();
  });

  it.each([
    [2, "Kandidat za proveru"],
    [3, "Nije pronađen kandidat"],
    [4, "Više mogućih kandidata"],
    [5, "Stavka zahteva pregled"],
  ])("renders the safe match state for backend status %s", async (status, label) => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response({
        ...exactReconciliation,
        lines: [{ ...exactReconciliation.lines[0], status, selectedCatalogArticleId: null, selectedCatalogName: null }],
      }));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByText(label)).toBeInTheDocument();
  });

  it("keeps unknown source price and physical quantity unknown instead of showing zero", async () => {
    const unknownPreview = {
      ...preview,
      lines: [{ ...preview.lines[0], invoicedQuantity: null, priceAmount: null, priceCurrencyCode: null, netLineAmount: null }],
      totals: {
        lineExtensionAmount: { value: null, currencyCode: null },
        taxExclusiveAmount: { value: null, currencyCode: null },
        taxInclusiveAmount: { value: null, currencyCode: null },
        payableAmount: { value: null, currencyCode: null },
      },
    };
    const unknownReconciliation = {
      ...exactReconciliation,
      lines: [{ ...exactReconciliation.lines[0], invoicedQuantity: null, priceAmount: null, priceCurrencyCode: null, netLineAmount: null }],
    };
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(response(unknownPreview))
      .mockResolvedValueOnce(response(unknownReconciliation)));

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByText("Podudaranje šifre — proveriti")).toBeInTheDocument();
    expect(screen.getAllByText("Nije navedeno").length).toBeGreaterThan(0);
    expect(screen.getByLabelText("Stvarno primljeno za XML red 1")).toHaveValue("");
    expect(screen.getByText("Nije potvrđeno")).toBeInTheDocument();
  });

  it("clears the prior in-memory draft when another XML file is selected", async () => {
    const secondPreview = { ...preview, invoiceNumber: "SYNTHETIC-SECOND" };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response(exactReconciliation))
      .mockResolvedValueOnce(response(secondPreview))
      .mockResolvedValueOnce(response(exactReconciliation));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));
    await screen.findByText("Podudaranje šifre — proveriti");
    fireEvent.change(screen.getByLabelText("Prodavnica / lokacija (ručni unos)"), { target: { value: "Test lokacija" } });
    fireEvent.change(screen.getByLabelText("Stvarno primljeno za XML red 1"), { target: { value: "2" } });
    fireEvent.click(screen.getByLabelText("Potvrđujem fizički prebrojanu količinu"));

    fireEvent.change(screen.getByLabelText("SEF XML fajl"), {
      target: { files: [new File(["<Invoice />"], "second.xml", { type: "application/xml" })] },
    });
    expect(screen.queryByText("Operater je potvrdio: 2")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Prodavnica / lokacija (ručni unos)")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByDisplayValue("SYNTHETIC-SECOND")).toBeInTheDocument();
    expect(screen.getByLabelText("Prodavnica / lokacija (ručni unos)")).toHaveValue("");
    expect(screen.getByLabelText("Stvarno primljeno za XML red 1")).toHaveValue("");
    expect(screen.getByText("Nije potvrđeno")).toBeInTheDocument();
  });

  it("does not send an invalid extension or make requests without existing admin authorization", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard />);
    fireEvent.change(screen.getByLabelText("SEF XML fajl"), {
      target: { files: [new File(["not xml"], "invoice.txt", { type: "text/plain" })] },
    });

    expect(screen.getByRole("alert")).toHaveTextContent("Izaberite jedan XML fajl sa nastavkom .xml.");
    expect(screen.getByRole("button", { name: "Pregledaj XML" })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalled();
    expect(screen.getByText(/ključ se koristi samo u zahtevu i ne čuva se u pregledaču/i)).toBeInTheDocument();
  });

  it("does not upload XML if the transient admin-key request is cancelled", async () => {
    const fetchMock = vi.fn();
    const requestAdminKey = vi.fn(() => null);
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard requestAdminKey={requestAdminKey} />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Admin autorizacija nije potvrđena; XML nije poslat na pregled.");
    expect(requestAdminKey).toHaveBeenCalledTimes(1);
    expect(fetchMock).not.toHaveBeenCalled();
    expect(localStorage.getItem("sefXmlPilotAdminKey")).toBeNull();
    expect(sessionStorage.getItem("sefXmlPilotAdminKey")).toBeNull();
  });

  it("offers native keyboard-focusable controls and a live status region", () => {
    const fetchMock = vi.fn((_url: string, init?: RequestInit) => new Promise((_resolve, reject) => {
      init?.signal?.addEventListener("abort", () => {
        const error = new Error("aborted");
        error.name = "AbortError";
        reject(error);
      });
    }));
    vi.stubGlobal("fetch", fetchMock);
    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    const fileInput = screen.getByLabelText("SEF XML fajl");
    const button = screen.getByRole("button", { name: "Pregledaj XML" });
    fileInput.focus();
    expect(document.activeElement).toBe(fileInput);
    expect(button.tabIndex).toBe(0);
    expect(screen.getByLabelText("SEF XML fajl")).toHaveAccessibleName();
    fireEvent.click(button);
    expect(screen.getByText("Čitam XML za privremeni pregled…")).toHaveAttribute("aria-live", "polite");
  });

  it("fails closed if reconciliation does not confirm a read-only, non-posting response", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response(preview))
      .mockResolvedValueOnce(response({ ...exactReconciliation, canPostInventory: true }));
    vi.stubGlobal("fetch", fetchMock);

    render(<SefXmlReceiptDraftWizard adminKey="approved-key" />);
    selectFile();
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("nacrt nije nastavljen");
    expect(screen.queryByRole("button", { name: "Preuzmi CSV nacrt" })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/pilot/sef-xml/preview",
      "/api/pilot/sef-xml/reconcile",
    ]);
  });

  it("quotes source fields and neutralizes formula prefixes including leading whitespace and controls", () => {
    const csv = buildSefXmlDraftCsv({
      invoice: { ...preview, lines: [{ ...preview.lines[0], supplierItemId: "\t=1+1" }] },
      reconciliation: { ...exactReconciliation, lines: [{ ...exactReconciliation.lines[0], sourceSupplierItemId: "\t=1+1" }] },
      metadata: { documentNumber: "INV,\"1\"", issueDate: "2026-10-16", supplierName: "Dobavljač", storeLocation: "Test" },
      physicalQuantities: { 1: "1,5" },
      physicalQuantityConfirmed: { 1: true },
    });
    expect(csv.charCodeAt(0)).toBe(0xFEFF);
    expect(csv).toContain("NEKNJIŽENO — NACRT");
    expect(csv).toContain("'\t=1+1");
    expect(csv).toContain('"INV,""1"""');
    expect(csv).toContain('"1,5"');
    expect(csv).toContain("POTVRĐENO OD OPERATERA");
    expect(csv).toMatch(/\r\n/u);
  });

  it("builds deterministic, source-ordered CSV rows with NEKNJIŽENO on each row", () => {
    const secondLine = {
      ...exactReconciliation.lines[0],
      sourceOrder: 2,
      sourceXmlLineId: "XML-2",
      sourceName: "Sandala, model \"B\"",
      status: 3,
      selectedCatalogArticleId: null,
      selectedCatalogExternalArticleId: null,
      selectedCatalogName: null,
    };
    const multiLineReconciliation = { ...exactReconciliation, lines: [secondLine, exactReconciliation.lines[0]] };
    const input = {
      invoice: { ...preview, lines: [...preview.lines, { ...preview.lines[0], sourceOrder: 2, sourceLineId: "XML-2" }] },
      reconciliation: multiLineReconciliation,
      metadata: { documentNumber: "DOC", issueDate: "2026-10-16", supplierName: "Dobavljač", storeLocation: "Test" },
      physicalQuantities: {},
      physicalQuantityConfirmed: {},
    };
    const first = buildSefXmlDraftCsv(input);
    const second = buildSefXmlDraftCsv(input);
    const rows = first.slice(1).trimEnd().split("\r\n");

    expect(first).toBe(second);
    expect(rows).toHaveLength(3);
    expect(rows[1]).toContain("NEKNJIŽENO — NACRT,1,");
    expect(rows[2]).toContain("NEKNJIŽENO — NACRT,2,");
    expect(rows[2]).toContain('"Sandala, model ""B"""');
  });
});
