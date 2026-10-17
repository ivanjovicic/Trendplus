import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import GoodsReceiptHistoryPanel from "./GoodsReceiptHistoryPanel";

const summary = {
  id: 81, status: "confirmed", supplierId: -3, supplierName: "Dobavljač",
  storeId: -4, documentType: "invoice", documentNumber: "FA-81",
  documentDate: "2026-10-16", receivedDate: "2026-10-16", lineCount: 1,
  totalQuantity: 2, purchaseTotal: 27, currency: "RSD",
};
const detail = {
  ...summary, createdBy: "stock-clerk-1", createdAtUtc: "2026-10-16T08:00:00Z",
  lines: [{ id: 10, lineNumber: 1, articleId: -7, articleName: "Čizma 42",
    quantity: 2, purchaseUnitPrice: 13.5, purchaseLineTotal: 27, currency: "RSD" }],
};
const ok = (data: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => data });

describe("GoodsReceiptHistoryPanel storno workflow", () => {
  beforeEach(() => {
    vi.stubGlobal("prompt", vi.fn().mockReturnValue("admin-test-key"));
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));
  });
  afterEach(() => vi.unstubAllGlobals());

  it("shows the linked audited storno after an authorized confirmation", async () => {
    const reversed = { ...detail, status: "reversed",
      reversal: { reversedBy: "stock-clerk-1", reversedAtUtc: "2026-10-16T09:00:00Z",
        reason: "Pogrešno evidentirana količina" } };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(ok(null, 401))
      .mockResolvedValueOnce(ok([summary]))
      .mockResolvedValueOnce(ok(detail))
      .mockResolvedValueOnce(ok({ id: 81, status: "reversed", replayed: false }))
      .mockResolvedValueOnce(ok(reversed));
    vi.stubGlobal("fetch", fetchMock);
    render(<GoodsReceiptHistoryPanel />);
    fireEvent.click(screen.getByText("Istorija prijema robe"));
    fireEvent.click(screen.getByRole("button", { name: "Pretraži" }));
    fireEvent.click(await screen.findByRole("button", { name: "Otvori" }));

    fireEvent.change(await screen.findByLabelText("ID operatera"), { target: { value: "stock-clerk-1" } });
    fireEvent.change(screen.getByLabelText("Lični ključ operatera"), {
      target: { value: "named-operator-only-test-key-over-32-characters" },
    });
    fireEvent.change(screen.getByLabelText(/Razlog storna/), {
      target: { value: "Pogrešno evidentirana količina" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Storniraj potvrđeni prijem" }));

    expect(await screen.findByText("Razlog: Pogrešno evidentirana količina")).toBeInTheDocument();
    const post = fetchMock.mock.calls.find(([url, options]) =>
      String(url).includes("/api/goods-receipts/81/reverse") && options?.method === "POST");
    expect(post).toBeDefined();
    expect(post![1].headers["X-Receipt-Operator-Id"]).toBe("stock-clerk-1");
    expect(post![1].headers["X-Receipt-Operator-Key"]).toBe("named-operator-only-test-key-over-32-characters");
    expect(JSON.parse(post![1].body).reason).toBe("Pogrešno evidentirana količina");
    expect(screen.queryByRole("button", { name: "Storniraj potvrđeni prijem" })).not.toBeInTheDocument();
  });

  it("does not attempt storno when reason or operator credentials are incomplete", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(ok([summary]))
      .mockResolvedValueOnce(ok(detail));
    vi.stubGlobal("fetch", fetchMock);
    render(<GoodsReceiptHistoryPanel />);
    fireEvent.click(screen.getByText("Istorija prijema robe"));
    fireEvent.click(screen.getByRole("button", { name: "Pretraži" }));
    fireEvent.click(await screen.findByRole("button", { name: "Otvori" }));

    expect(await screen.findByRole("button", { name: "Storniraj potvrđeni prijem" })).toBeDisabled();
    await waitFor(() => expect(fetchMock.mock.calls.filter(([, options]) =>
      options?.method === "POST")).toHaveLength(0));
  });
});
