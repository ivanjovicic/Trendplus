import { afterEach, describe, expect, it, vi } from "vitest";
import {
  reconcileSefXmlInvoice,
  SefXmlPilotApiError,
  previewSefXmlInvoice,
  type SefXmlInvoicePreview,
} from "../sefXmlPilotApi";

vi.mock("../../utils/apiUrl", () => ({
  apiUrl: (path: string) => path,
}));

afterEach(() => {
  vi.unstubAllGlobals();
});

const invoice: SefXmlInvoicePreview = {
  isReadOnlyPreview: true,
  documentKind: 0,
  invoiceNumber: "SYNTHETIC-QDB13A",
  issueDate: "2026-10-16",
  invoiceTypeCode: "380",
  documentCurrencyCode: "RSD",
  supplier: { name: "Sintetički dobavljač", companyId: "109999999" },
  lines: [],
  totals: {
    lineExtensionAmount: { value: null, currencyCode: null },
    taxExclusiveAmount: { value: null, currencyCode: null },
    taxInclusiveAmount: { value: null, currencyCode: null },
    payableAmount: { value: null, currencyCode: null },
  },
  taxSubtotals: [],
  diagnostics: [],
};

describe("sefXmlPilotApi", () => {
  it("posts one local XML file to the read-only preview endpoint without putting the admin key in the body", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => invoice });
    vi.stubGlobal("fetch", fetchMock);

    await previewSefXmlInvoice(new File(["<Invoice />"], "sample.xml", { type: "application/xml" }), "approved-key");

    expect(fetchMock).toHaveBeenCalledWith("/api/pilot/sef-xml/preview", expect.objectContaining({
      method: "POST",
      headers: { "X-Admin-Key": "approved-key" },
    }));
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.body).toBeInstanceOf(FormData);
    expect((init.body as FormData).get("file")).toBeInstanceOf(File);
    expect(init.headers).not.toHaveProperty("Content-Type");
    expect(init.cache).toBe("no-store");
    expect((init.body as FormData).has("approved-key")).toBe(false);
  });

  it("posts only the parser preview and manual choices to reconciliation", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ isReadOnlyPreview: true, canPostInventory: false, lines: [] }) });
    vi.stubGlobal("fetch", fetchMock);

    await reconcileSefXmlInvoice({ invoice, manualSelections: [{ sourceOrder: 1, catalogArticleId: -7 }], dataScope: "all" }, "approved-key");

    expect(fetchMock).toHaveBeenCalledWith("/api/pilot/sef-xml/reconcile", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Admin-Key": "approved-key" },
      body: JSON.stringify({ invoice, manualSelections: [{ sourceOrder: 1, catalogArticleId: -7 }], dataScope: "all" }),
      signal: undefined,
      cache: "no-store",
    });
  });

  it.each([
    [400, "XML faktura nije podržana ili nije moguće napraviti pregled. Proverite fajl."],
    [401, "Niste ovlašćeni za SEF XML pilot."],
    [403, "Niste ovlašćeni za SEF XML pilot."],
    [404, "SEF XML pilot nije uključen u ovom okruženju."],
    [413, "XML fajl je veći od dozvoljene granice pilota."],
    [422, "XML faktura nije podržana ili nije moguće napraviti pregled. Proverite fajl."],
    [429, "Previše zahteva za SEF XML pilot. Sačekajte pa pokušajte ponovo."],
    [503, "SEF XML pilot trenutno nije dostupan. Pokušajte ponovo."],
  ])("maps HTTP %s to a safe operator message", async (status, message) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status,
      json: async () => ({ error: "internal detail that must not be shown" }),
    }));

    await expect(previewSefXmlInvoice(new File(["x"], "sample.xml"))).rejects.toEqual(
      expect.objectContaining<SefXmlPilotApiError>({ status, message }),
    );
  });

  it("does not surface server details for unknown HTTP errors or network failures", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({ error: "secret internals" }) }));
    await expect(previewSefXmlInvoice(new File(["x"], "sample.xml"))).rejects.toMatchObject({
      status: 500,
      message: "SEF XML pregled nije uspeo. Pokušajte ponovo ili proverite okruženje.",
    });

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("private network detail")));
    await expect(previewSefXmlInvoice(new File(["x"], "sample.xml"))).rejects.toMatchObject({
      status: null,
      message: "Veza sa SEF XML servisom nije dostupna. Proverite vezu pa pokušajte ponovo.",
    });
  });
});
