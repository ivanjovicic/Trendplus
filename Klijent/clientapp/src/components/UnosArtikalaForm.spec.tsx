import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import UnosArtikalaForm, { type GoodsReceiptHeaderDraft } from "./UnosArtikalaForm";
import { getSezone } from "../services/sezoneApi";

vi.mock("../services/sezoneApi", () => ({ getSezone: vi.fn() }));

const header: GoodsReceiptHeaderDraft = {
  supplierId: -3,
  supplierName: "Dobavljač",
  documentNumber: "FA-81",
  documentType: "invoice",
  documentDate: "2026-10-16",
  receivedDate: "2026-10-16",
  storeId: -4,
  storeName: "Prodavnica",
  comment: "",
  requestKey: "11111111-1111-4111-8111-111111111111",
};

function ok(body: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function renderForm() {
  return render(<MemoryRouter><UnosArtikalaForm header={header} tipoviObuce={[]} /></MemoryRouter>);
}

function searchExisting() {
  fireEvent.change(screen.getByPlaceholderText("Pretražite artikle ili unesite novi naziv"), {
    target: { value: "Čizma" },
  });
}

function fillPricesAndQuantity() {
  const inputs = screen.getAllByRole("spinbutton");
  fireEvent.change(inputs[0], { target: { value: "2" } });
  fireEvent.change(inputs[1], { target: { value: "13.50" } });
  fireEvent.change(inputs[2], { target: { value: "25" } });
  fireEvent.change(screen.getByLabelText(/ID ovlašćenog operatera/), {
    target: { value: "stock-clerk-1" },
  });
  fireEvent.change(screen.getByLabelText(/Lični ključ operatera/), {
    target: { value: "named-operator-only-test-key-over-32-characters" },
  });
}

function notFound() {
  return ok(null, 404);
}

describe("UnosArtikalaForm receipt regressions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getSezone).mockResolvedValue([]);
    vi.stubGlobal("prompt", vi.fn());
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));
  });

  afterEach(() => vi.unstubAllGlobals());

  it("posts an existing negative Access id in one confirmed receipt request", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: "C42", quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]))
      .mockResolvedValueOnce(ok({ id: 81, status: "confirmed", lineCount: 1, totalQuantity: 2, purchaseTotal: 27 }, 201));
    vi.stubGlobal("fetch", request);
    renderForm();

    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));

    await screen.findByText("Prijem je potvrđen");
    const post = request.mock.calls.find(([url, options]) => String(url).includes("/api/goods-receipts") && options?.method === "POST");
    expect(post).toBeDefined();
    expect(post?.[1]?.body).toContain('"articleId":-7');
    expect(post?.[1]?.body).toContain('"storeId":-4');
    expect(post?.[1]?.headers["X-Receipt-Operator-Id"]).toBe("stock-clerk-1");
    expect(post?.[1]?.headers["X-Receipt-Operator-Key"]).toBe("named-operator-only-test-key-over-32-characters");
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(1);
  });

  it("hides old article results as soon as the search query changes", async () => {
    const request = vi.fn((input: RequestInfo | URL) => {
      const url = new URL(String(input), window.location.origin);
      if (url.pathname.includes("/request/")) return Promise.resolve(notFound());
      if (url.searchParams.get("query") === "Čizma") return Promise.resolve(ok([{
        id: -7, name: "Čizma 42", code: "C42", quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]));
      return new Promise<Response>(() => {});
    });
    vi.stubGlobal("fetch", request);
    renderForm();

    searchExisting();
    expect(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ })).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText("Pretražite artikle ili unesite novi naziv"), {
      target: { value: "Patika" },
    });

    expect(screen.queryByRole("button", { name: /Čizma 42 · ID -7/ })).not.toBeInTheDocument();
  });

  it("prompts for authorization before parsing an empty 401 response and retries with the key", async () => {
    vi.mocked(window.prompt).mockReturnValue("test-admin-key");
    const unauthorized = { ok: false, status: 401, json: async () => { throw new Error("empty body"); } };
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]))
      .mockResolvedValueOnce(unauthorized)
      .mockResolvedValueOnce(ok({ id: 83, status: "confirmed", lineCount: 1, totalQuantity: 2, purchaseTotal: 27 }, 201));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));

    await screen.findByText("Prijem je potvrđen");
    expect(window.prompt).toHaveBeenCalledWith("Za potvrdu prijema potreban je admin ključ.");
    const posts = request.mock.calls.filter(([, options]) => options?.method === "POST");
    expect(posts).toHaveLength(2);
    expect(posts[1][1].headers["X-Admin-Key"]).toBe("test-admin-key");
  });

  it("reuses the same idempotency key after an unknown network result", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]))
      .mockRejectedValueOnce(new Error("network timeout"))
      .mockResolvedValueOnce(ok({ id: 82, status: "confirmed", lineCount: 1, totalQuantity: 2, purchaseTotal: 27 }, 201));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();

    const button = screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ });
    fireEvent.click(button);
    await screen.findByRole("alert");
    expect(screen.getAllByRole("spinbutton")[0]).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: /Ponovi isti zahtev/ }));
    await screen.findByText("Prijem je potvrđen");

    const posts = request.mock.calls.filter(([, options]) => options?.method === "POST");
    expect(posts).toHaveLength(2);
    expect(JSON.parse(posts[0][1].body).requestKey).toBe(JSON.parse(posts[1][1].body).requestKey);
  });

  it("does not send a second request on a rapid double click", async () => {
    let finishPost!: (response: ReturnType<typeof ok>) => void;
    const pendingPost = new Promise<ReturnType<typeof ok>>((resolve) => { finishPost = resolve; });
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]))
      .mockReturnValueOnce(pendingPost);
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    const button = screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ });
    fireEvent.click(button);
    fireEvent.click(button);
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(1);
    finishPost(ok({ id: 84, status: "confirmed", lineCount: 1, totalQuantity: 2, purchaseTotal: 27 }, 201));
    await screen.findByText("Prijem je potvrđen");
  });

  it("accepts ordinary two-decimal prices despite JavaScript floating-point rounding", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]))
      .mockResolvedValueOnce(ok({ id: 85, status: "confirmed", lineCount: 1, totalQuantity: 2, purchaseTotal: 2.26 }, 201));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    const inputs = screen.getAllByRole("spinbutton");
    fireEvent.change(inputs[1], { target: { value: "1.13" } });
    fireEvent.change(inputs[2], { target: { value: "2.07" } });
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));
    await screen.findByText("Prijem je potvrđen");
    const post = request.mock.calls.find(([, options]) => options?.method === "POST");
    expect(post).toBeDefined();
    const payload = JSON.parse(post![1].body);
    expect(payload.lines[0]).toMatchObject({ purchaseUnitPrice: 1.13, salesUnitPrice: 2.07 });
  });

  it("rejects prices with more than two decimal places without posting", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    fireEvent.change(screen.getAllByRole("spinbutton")[1], { target: { value: "1.234" } });
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent("najviše dve decimale");
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(0);
  });

  it("rejects negative or invalid quantities before posting", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
      id: -7, name: "Čizma 42", code: null, quantity: 4,
      purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    fireEvent.change(screen.getAllByRole("spinbutton")[0], { target: { value: "-1" } });
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent("ceo broj veći od nule");
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(0);
  });


  it("refuses a receipt POST without a named operator key even if admin credentials exist", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));
    fillPricesAndQuantity();
    fireEvent.change(screen.getByLabelText(/Lični ključ operatera/), { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: /Pregledaj i potvrdi prijem/ }));
    expect(await screen.findByRole("alert")).toHaveTextContent("namenski ID operatera");
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(0);
  });

  it("does not register an unused resize side effect", () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(notFound()));
    const addSpy = vi.spyOn(window, "addEventListener");
    const { unmount } = renderForm();
    unmount();
    expect(addSpy).not.toHaveBeenCalledWith("resize", expect.any(Function));
    addSpy.mockRestore();
  });

  it("recovers a committed receipt by the request key after navigation state is restored", async () => {
    const saved = {
      id: 91, status: "confirmed", requestKey: header.requestKey, supplierId: -3,
      storeId: -4, documentType: "invoice", documentNumber: "FA-81",
      lineCount: 1, totalQuantity: 2, purchaseTotal: 27, lines: [],
    };
    const request = vi.fn().mockResolvedValueOnce(ok(saved));
    vi.stubGlobal("fetch", request);
    renderForm();

    expect(await screen.findByText("Prijem je potvrđen")).toBeInTheDocument();
    expect(screen.getByText("ID prijema:").parentElement).toHaveTextContent("91");
    expect(request.mock.calls[0][0]).toContain(`/request/${header.requestKey}`);
    expect(request.mock.calls.filter(([, options]) => options?.method === "POST")).toHaveLength(0);
  });

  it("warns before closing a dirty receipt form without persisting its contents", async () => {
    const request = vi.fn()
      .mockResolvedValueOnce(notFound())
      .mockResolvedValueOnce(ok([{
        id: -7, name: "Čizma 42", code: null, quantity: 4,
        purchasePrice: 11, salesPrice: 25, storeId: -4, dataOrigin: "access",
      }]));
    vi.stubGlobal("fetch", request);
    renderForm();
    searchExisting();
    fireEvent.click(await screen.findByRole("button", { name: /Čizma 42 · ID -7/ }));

    const setItem = vi.spyOn(Storage.prototype, "setItem");
    const event = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(setItem).not.toHaveBeenCalled();
    setItem.mockRestore();
  });
});
