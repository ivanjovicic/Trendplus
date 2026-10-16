import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { Sezona } from "../types/Sezona";
import { clearArtikliClientCaches } from "../services/artikliApi";
import { getSezone } from "../services/sezoneApi";
import { apiUrl } from "../utils/apiUrl";

export interface GoodsReceiptHeaderDraft {
  supplierId: number;
  supplierName: string;
  documentNumber: string;
  documentType: "invoice" | "delivery-note";
  documentDate: string;
  receivedDate: string;
  storeId: number;
  storeName: string;
  comment: string;
  requestKey: string;
}

interface ArtikalStavka {
  articleId: number | null;
  articleCode: string;
  articleName: string;
  quantity: string;
  purchaseUnitPrice: string;
  salesUnitPrice: string;
  shoeTypeId: string;
  seasonId: string;
  comment: string;
}

interface UnosArtikalaFormProps {
  header: GoodsReceiptHeaderDraft;
  tipoviObuce: { id: number; naziv: string }[];
}

interface ConfirmedReceipt {
  id: number;
  status: string;
  lineCount: number;
  totalQuantity: number;
  purchaseTotal: number;
}

interface ReceiptArticle {
  id: number;
  name: string;
  code: string | null;
  quantity: number | null;
  purchasePrice: number | null;
  salesPrice: number | null;
  storeId: number;
  dataOrigin: string;
}

const emptyLine = (): ArtikalStavka => ({
  articleId: null,
  articleCode: "",
  articleName: "",
  quantity: "1",
  purchaseUnitPrice: "",
  salesUnitPrice: "",
  shoeTypeId: "",
  seasonId: "",
  comment: "",
});

// Validate decimal text rather than multiplying IEEE-754 floats by 100: legitimate
// prices such as 1.13 and 0.29 fail an exact Math.round(price * 100) comparison.
function isValidUnitPrice(value: string): boolean {
  const normalized = value.trim();
  if (!/^\d+(?:\.\d{1,2})?$/.test(normalized)) return false;
  const price = Number(normalized);
  // Keep values within the precision at which JavaScript can serialize cents reliably.
  return Number.isFinite(price) && price <= 9_000_000_000_000;
}

function errorMessage(body: any, status: number): string {
  return body?.detail ?? body?.message ?? body?.title ?? `Zahtev nije uspeo (HTTP ${status}).`;
}

async function readJson(response: Response): Promise<any> {
  try {
    return await response.json();
  } catch {
    throw new Error("Server je vratio neispravan odgovor. Proverite istoriju prijema pre novog pokušaja.");
  }
}

export default function UnosArtikalaForm({ header, tipoviObuce }: UnosArtikalaFormProps) {
  const navigate = useNavigate();
  const [seasons, setSeasons] = useState<Sezona[]>([]);
  const [articleResults, setArticleResults] = useState<Record<number, ReceiptArticle[]>>({});
  const [articleErrors, setArticleErrors] = useState<Record<number, string>>({});
  const [optionsLoading, setOptionsLoading] = useState(true);
  const [optionsError, setOptionsError] = useState<string | null>(null);
  const [lines, setLines] = useState<ArtikalStavka[]>([emptyLine()]);
  const [search, setSearch] = useState<Record<number, string>>({});
  const [openSearch, setOpenSearch] = useState<Record<number, boolean>>({});
  const [confirmDuplicates, setConfirmDuplicates] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [unknownOutcome, setUnknownOutcome] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [confirmedReceipt, setConfirmedReceipt] = useState<ConfirmedReceipt | null>(null);
  const submittingRef = useRef(false);
  const requestIdentity = useRef<{ fingerprint: string; key: string } | null>(null);
  const adminKeyRef = useRef("");
  const [operatorId, setOperatorId] = useState("");
  const [operatorKey, setOperatorKey] = useState("");

  useEffect(() => {
    let active = true;
    setOptionsLoading(true);
    setOptionsError(null);
    void getSezone().then((seasonRows) => {
      if (!active) return;
      setSeasons(seasonRows);
    }).catch(() => {
      if (active) setOptionsError("Sezone nisu učitane. Unos nije moguće potvrditi.");
    }).finally(() => {
      if (active) setOptionsLoading(false);
    });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    const checkRequest = async () => {
      try {
        const url = apiUrl(`/api/goods-receipts/request/${encodeURIComponent(header.requestKey)}`);
        const send = () => fetch(url, {
          headers: adminKeyRef.current ? { "X-Admin-Key": adminKeyRef.current } : {},
          signal: controller.signal,
        });
        let response = await send();
        if (response.status === 401) {
          const entered = window.prompt("Za proveru prethodnog prijema potreban je admin ključ.");
          if (!entered?.trim()) return;
          adminKeyRef.current = entered.trim();
          response = await send();
        }
        if (response.status === 404) return;
        const body = await readJson(response);
        if (!response.ok) throw new Error(body?.detail ?? `HTTP ${response.status}`);
        if (!body || !Number.isSafeInteger(body.id) || body.status !== "confirmed" ||
            !Number.isInteger(body.lineCount) || body.lineCount < 1 || !Number.isInteger(body.totalQuantity))
          throw new Error("Server nije vratio potpunu potvrdu prijema.");
        if (active) {
          setUnknownOutcome(false);
          setConfirmedReceipt(body as ConfirmedReceipt);
        }
      } catch (checkError) {
        if (!active || (checkError instanceof DOMException && checkError.name === "AbortError")) return;
        setError("Nije uspela provera da li je raniji zahtev već sačuvan. Unos neće biti potvrđen bez idempotentne serverske provere.");
      }
    };
    void checkRequest();
    return () => {
      active = false;
      controller.abort();
    };
  }, [header.requestKey]);

  useEffect(() => {
    let active = true;
    const controllers: AbortController[] = [];
    const timers: ReturnType<typeof setTimeout>[] = [];
    for (const [rawIndex, value] of Object.entries(search)) {
      const index = Number(rawIndex);
      const query = value.trim();
      if (!openSearch[index] || query.length < 1) continue;
      const controller = new AbortController();
      controllers.push(controller);
      timers.push(setTimeout(() => {
        const load = async () => {
          try {
            const url = new URL(apiUrl("/api/goods-receipts/articles"), window.location.origin);
            url.searchParams.set("storeId", String(header.storeId));
            url.searchParams.set("query", query);
            const send = () => fetch(url.toString(), {
              headers: adminKeyRef.current ? { "X-Admin-Key": adminKeyRef.current } : {},
              signal: controller.signal,
            });
            let response = await send();
            if (response.status === 401) {
              const entered = window.prompt("Za pregled zaliha potreban je admin ključ.");
              if (!entered?.trim()) throw new Error("Unos admin ključa je otkazan.");
              adminKeyRef.current = entered.trim();
              response = await send();
            }
            const body = await readJson(response);
            if (!response.ok) throw new Error(body?.detail ?? body?.message ?? `HTTP ${response.status}`);
            if (active) {
              setArticleResults((current) => ({ ...current, [index]: body as ReceiptArticle[] }));
              setArticleErrors((current) => ({ ...current, [index]: "" }));
            }
          } catch (searchError) {
            if (!active || (searchError instanceof DOMException && searchError.name === "AbortError")) return;
            setArticleErrors((current) => ({ ...current, [index]: searchError instanceof Error ? searchError.message : "Pretraga artikala nije uspela." }));
          }
        };
        void load();
      }, 220));
    }
    return () => {
      active = false;
      timers.forEach(clearTimeout);
      controllers.forEach((controller) => controller.abort());
    };
  }, [header.storeId, openSearch, search]);

  const duplicateArticleNames = useMemo(() => {
    const counts = new Map<number, number>();
    for (const line of lines) {
      if (line.articleId !== null) counts.set(line.articleId, (counts.get(line.articleId) ?? 0) + 1);
    }
    return [...counts.entries()].filter(([, count]) => count > 1)
      .map(([id]) => lines.find((line) => line.articleId === id)?.articleName ?? `ID ${id}`);
  }, [lines]);

  const hasUnsavedChanges = useMemo(() => lines.some((line) =>
    line.articleId !== null || line.articleCode.trim() !== "" || line.articleName.trim() !== "" ||
    line.quantity !== "1" || line.purchaseUnitPrice !== "" || line.salesUnitPrice !== "" || line.comment.trim() !== ""), [lines]);

  useEffect(() => {
    if (!hasUnsavedChanges || confirmedReceipt) return;
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", warnBeforeLeaving);
    return () => window.removeEventListener("beforeunload", warnBeforeLeaving);
  }, [hasUnsavedChanges, confirmedReceipt]);

  const preview = useMemo(() => lines.reduce((sum, line) => {
    const quantity = Number(line.quantity);
    const purchase = Number(line.purchaseUnitPrice);
    return sum + (Number.isFinite(quantity) && Number.isFinite(purchase) ? quantity * purchase : 0);
  }, 0), [lines]);

  const updateLine = (index: number, changes: Partial<ArtikalStavka>) => {
    setLines((current) => current.map((line, lineIndex) => lineIndex === index ? { ...line, ...changes } : line));
  };

  const chooseExisting = (index: number, article: ReceiptArticle) => {
    updateLine(index, {
      articleId: article.id,
      articleCode: "",
      articleName: article.name,
      purchaseUnitPrice: article.purchasePrice == null ? "" : String(article.purchasePrice),
      salesUnitPrice: article.salesPrice == null ? "" : String(article.salesPrice),
      shoeTypeId: "",
      seasonId: "",
    });
    setSearch((current) => ({ ...current, [index]: article.name }));
    setOpenSearch((current) => ({ ...current, [index]: false }));
    setConfirmDuplicates(false);
  };

  const filteredArticles = (index: number) => {
    const query = (search[index] ?? "").trim().toLocaleLowerCase("sr-RS");
    if (!query) return [];
    return articleResults[index] ?? [];
  };

  const addLine = () => setLines((current) => [...current, emptyLine()]);
  const removeLine = (index: number) => {
    setLines((current) => current.length > 1 ? current.filter((_, lineIndex) => lineIndex !== index) : current);
    setConfirmDuplicates(false);
  };

  const submit = async () => {
    if (submittingRef.current || confirmedReceipt) return;
    setError(null);
    if (!operatorId.trim() || operatorKey.length < 32) {
      setError("Za knjiženje je potreban namenski ID operatera i njegov bezbednosni ključ (minimum 32 karaktera). Zajednički admin ključ nije dovoljan.");
      return;
    }
    if (optionsLoading || optionsError) {
      setError(optionsError ?? "Sačekajte da se učitaju šifarnici.");
      return;
    }
    if (lines.length < 1) {
      setError("Prijem mora da sadrži najmanje jednu stavku.");
      return;
    }
    if (duplicateArticleNames.length && !confirmDuplicates) {
      setError("Potvrdite da su ponovljene stavke istog artikla namerne.");
      return;
    }

    const apiLines = [];
    for (let index = 0; index < lines.length; index++) {
      const line = lines[index];
      const quantity = Number(line.quantity);
      const purchase = Number(line.purchaseUnitPrice);
      const sales = Number(line.salesUnitPrice);
      if (!Number.isSafeInteger(quantity) || quantity <= 0 || quantity > 2_147_483_647) {
        setError(`Količina u stavci ${index + 1} mora biti ceo broj veći od nule.`);
        return;
      }
      if (!isValidUnitPrice(line.purchaseUnitPrice) || !isValidUnitPrice(line.salesUnitPrice)) {
        setError(`Unesite poznatu nenegativnu nabavnu i prodajnu cenu sa najviše dve decimale u stavci ${index + 1}.`);
        return;
      }
      if (line.articleId === null && !line.articleName.trim()) {
        setError(`Izaberite postojeći ili unesite naziv novog artikla u stavci ${index + 1}.`);
        return;
      }
      if (line.articleId === null && (!line.shoeTypeId || !line.seasonId)) {
        setError(`Izaberite tip obuće i sezonu za novi artikal u stavci ${index + 1}.`);
        return;
      }
      apiLines.push({
        articleId: line.articleId,
        articleCode: line.articleId === null ? (line.articleCode.trim() || null) : null,
        articleName: line.articleId === null ? line.articleName.trim() : null,
        quantity,
        purchaseUnitPrice: purchase,
        salesUnitPrice: sales,
        shoeTypeId: line.articleId === null ? Number(line.shoeTypeId) : null,
        seasonId: line.articleId === null ? Number(line.seasonId) : null,
        comment: line.comment.trim() || null,
      });
    }

    const businessPayload = {
      supplierId: header.supplierId,
      storeId: header.storeId,
      documentType: header.documentType,
      documentNumber: header.documentNumber,
      documentDate: header.documentDate,
      receivedDate: header.receivedDate,
      currency: "RSD",
      comment: header.comment || null,
      confirmDuplicateLines: confirmDuplicates,
      lines: apiLines,
    };
    const fingerprint = JSON.stringify(businessPayload);
    if (!requestIdentity.current || requestIdentity.current.fingerprint !== fingerprint) {
      requestIdentity.current = {
        fingerprint,
        key: requestIdentity.current ? crypto.randomUUID() : header.requestKey,
      };
    }
    const requestKey = requestIdentity.current.key;

    submittingRef.current = true;
    setIsSubmitting(true);
    try {
      const send = async (confirmSimilarDocument: boolean) => {
        let response: Response;
        try {
          response = await fetch(apiUrl("/api/goods-receipts"), {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              ...(adminKeyRef.current ? { "X-Admin-Key": adminKeyRef.current } : {}),
              "X-Receipt-Operator-Id": operatorId.trim(),
              "X-Receipt-Operator-Key": operatorKey,
            },
            body: JSON.stringify({ ...businessPayload, requestKey, confirmSimilarDocument }),
          });
        } catch {
          setUnknownOutcome(true);
          throw new Error("Veza je prekinuta i ishod prijema nije poznat. Ponovite isti zahtev da proverite rezultat; stavke su zaključane radi sprečavanja duplog knjiženja.");
        }
        if (response.status === 401) {
          const entered = window.prompt("Za potvrdu prijema potreban je admin ključ.");
          if (!entered?.trim()) throw new Error("Prijem nije potvrđen. Admin ključ je obavezan.");
          adminKeyRef.current = entered.trim();
          return send(confirmSimilarDocument);
        }
        if (response.status === 404) throw new Error("Serversko knjiženje prijema nije omogućeno. Obratite se administratoru; roba nije upisana.");
        const body = await readJson(response);
        if (response.status === 409 && body?.code === "similar_document") {
          if (!window.confirm(`${body.message}\n\nAko je ovo zasebna delimična isporuka, nastavite potvrdom.`))
            throw new Error("Prijem nije knjižen. Proverite postojeći dokument u istoriji.");
          return send(true);
        }
        if (response.status >= 500) {
          setUnknownOutcome(true);
          throw new Error("Server nije potvrdio ishod prijema. Ponovite isti zahtev da proverite rezultat; stavke su zaključane radi sprečavanja duplog knjiženja.");
        }
        if (!response.ok) throw new Error(errorMessage(body, response.status));
        return body;
      };

      const receipt = await send(false);
      if (!receipt || !Number.isSafeInteger(receipt.id) || receipt.status !== "confirmed" ||
          receipt.lineCount !== apiLines.length || receipt.totalQuantity < 1) {
        setUnknownOutcome(true);
        throw new Error("Server nije vratio potpunu potvrdu prijema. Proverite istoriju pre novog pokušaja.");
      }
      setUnknownOutcome(false);
      setConfirmedReceipt(receipt);
      clearArtikliClientCaches();
    } catch (submitError) {
      setError(submitError instanceof Error ? submitError.message : "Prijem nije potvrđen. Proverite istoriju pre novog pokušaja.");
    } finally {
      submittingRef.current = false;
      setIsSubmitting(false);
    }
  };

  if (confirmedReceipt) {
    return (
      <section className="rounded-xl border border-emerald-700 bg-surface p-5" role="status" aria-live="polite">
        <h2 className="text-xl font-semibold text-foreground">Prijem je potvrđen</h2>
        <p className="mt-2 text-foreground">ID prijema: <strong>{confirmedReceipt.id}</strong></p>
        <p className="mt-1 text-sm text-muted">{confirmedReceipt.lineCount} stavki · {confirmedReceipt.totalQuantity} komada · {confirmedReceipt.purchaseTotal.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD nabavne vrednosti</p>
        <p className="mt-2 text-xs text-muted">Vrednost je zbir unetih nabavnih cena bez obračuna PDV-a i rabata.</p>
        <button type="button" className="button-big mt-4 bg-primary text-on-primary" onClick={() => navigate("/unos-robe")}>
          Novi prijem
        </button>
      </section>
    );
  }

  return (
    <section className="rounded-xl border border-border bg-surface p-4 sm:p-5" aria-labelledby="receipt-lines-heading">
      <div className="mb-4 rounded-lg border border-info bg-info-10 p-4 text-sm text-info">
        <h2 className="font-semibold">{header.documentType === "invoice" ? "Račun" : "Otpremnica"}: {header.documentNumber}</h2>
        <p>{header.supplierName} · {header.documentDate} · prijem {header.receivedDate}</p>
        <p>{header.storeName} (ID {header.storeId})</p>
      </div>

      {unknownOutcome ? <p role="alert" className="mb-4 rounded-lg border border-amber-600 p-3 text-sm text-foreground">Ishod prethodnog zahteva nije poznat. Unos je zaključan. Ponovite isti zahtev; serverski idempotency ključ će vratiti postojeći prijem ako je već sačuvan.</p> : null}

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 id="receipt-lines-heading" className="text-xl font-semibold text-foreground">Stavke prijema ({lines.length})</h2>
        <button type="button" className="button-big border border-border bg-surface-elevated text-foreground" onClick={addLine}>+ Dodaj stavku</button>
      </div>

      {optionsLoading ? <p role="status" className="mb-4 text-sm text-muted">Učitavanje sezona…</p> : null}
      {optionsError ? <div role="alert" className="mb-4 rounded-lg border border-[var(--error)] p-3 text-sm text-[var(--error)]">{optionsError}<button type="button" className="ml-3 underline" onClick={() => window.location.reload()}>Pokušaj ponovo</button></div> : null}

      <fieldset disabled={isSubmitting || unknownOutcome} className="min-w-0 space-y-4 border-0 p-0">
        {lines.map((line, index) => (
          <article key={index} className="min-w-0 rounded-lg border border-border bg-surface-elevated p-3 sm:p-4">
            <div className="mb-3 flex items-center justify-between gap-2">
              <h3 className="font-semibold text-foreground">Stavka {index + 1}</h3>
              <button type="button" className="text-sm text-[var(--error)] underline" onClick={() => removeLine(index)} disabled={lines.length === 1}>Ukloni</button>
            </div>

            {line.articleId === null ? (
              <div className="grid min-w-0 grid-cols-1 gap-3 md:grid-cols-2">
                <div className="relative min-w-0 md:col-span-2">
                  <label className="grid gap-1 text-sm font-medium text-foreground">
                    Postojeći artikal ili naziv novog
                    <input className="input-big w-full min-w-0" value={line.articleName} placeholder="Pretražite artikle ili unesite novi naziv" onFocus={() => setOpenSearch((current) => ({ ...current, [index]: true }))} onChange={(event) => {
                      updateLine(index, { articleName: event.target.value, articleId: null });
                      setSearch((current) => ({ ...current, [index]: event.target.value }));
                      setOpenSearch((current) => ({ ...current, [index]: true }));
                      setArticleResults((current) => ({ ...current, [index]: [] }));
                      setArticleErrors((current) => ({ ...current, [index]: "" }));
                    }} />
                  </label>
                  {openSearch[index] && filteredArticles(index).length > 0 ? (
                    <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-border bg-surface shadow-lg" aria-label={`Rezultati pretrage stavke ${index + 1}`}>
                      {filteredArticles(index).map((article) => (
                        <li key={article.id}>
                          <button type="button" className="block w-full px-3 py-2 text-left text-sm text-foreground hover:bg-surface-elevated" onClick={() => chooseExisting(index, article)}>
                            {article.name} · ID {article.id} · zaliha {article.quantity ?? "nepoznata"}
                          </button>
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  {articleErrors[index] ? <p role="alert" className="mt-1 text-xs text-[var(--error)]">{articleErrors[index]}</p> : null}
                  {openSearch[index] && search[index]?.trim() && !articleErrors[index] && articleResults[index]?.length === 0 ? <p className="mt-1 text-xs text-muted">Nema postojećeg artikla na izabranoj lokaciji. Možete uneti novi artikal.</p> : null}
                </div>

                <label className="grid gap-1 text-sm font-medium text-foreground">Šifra (opciono)<input className="input-big w-full" maxLength={100} value={line.articleCode} onChange={(event) => updateLine(index, { articleCode: event.target.value })} /></label>
                <label className="grid gap-1 text-sm font-medium text-foreground">Tip obuće<select className="input-big w-full" value={line.shoeTypeId} onChange={(event) => updateLine(index, { shoeTypeId: event.target.value })}><option value="">Izaberite tip</option>{tipoviObuce.map((type) => <option key={type.id} value={type.id}>{type.naziv}</option>)}</select></label>
                <label className="grid gap-1 text-sm font-medium text-foreground">Sezona<select className="input-big w-full" value={line.seasonId} onChange={(event) => updateLine(index, { seasonId: event.target.value })}><option value="">Izaberite sezonu</option>{seasons.map((season) => <option key={season.id} value={season.id}>{season.naziv}</option>)}</select></label>
              </div>
            ) : (
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3 text-sm text-foreground">
                <span>{line.articleName} · ID {line.articleId}</span>
                <button type="button" className="underline" onClick={() => {
                  updateLine(index, { ...emptyLine(), quantity: line.quantity, purchaseUnitPrice: line.purchaseUnitPrice, salesUnitPrice: line.salesUnitPrice, comment: line.comment });
                  setSearch((current) => ({ ...current, [index]: "" }));
                }}>Promeni artikal</button>
              </div>
            )}

            <div className="mt-3 grid min-w-0 grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <label className="grid gap-1 text-sm font-medium text-foreground">Količina<input className="input-big w-full" type="number" min="1" step="1" inputMode="numeric" value={line.quantity} onChange={(event) => updateLine(index, { quantity: event.target.value })} /></label>
              <label className="grid gap-1 text-sm font-medium text-foreground">Nabavna cena po komadu (RSD)<input className="input-big w-full" type="number" min="0" step="0.01" inputMode="decimal" value={line.purchaseUnitPrice} onChange={(event) => updateLine(index, { purchaseUnitPrice: event.target.value })} /></label>
              <label className="grid gap-1 text-sm font-medium text-foreground">Prodajna cena po komadu (RSD)<input className="input-big w-full" type="number" min="0" step="0.01" inputMode="decimal" value={line.salesUnitPrice} onChange={(event) => updateLine(index, { salesUnitPrice: event.target.value })} /></label>
              <label className="grid gap-1 text-sm font-medium text-foreground">Komentar<input className="input-big w-full" maxLength={500} value={line.comment} onChange={(event) => updateLine(index, { comment: event.target.value })} /></label>
            </div>
          </article>
        ))}
      </fieldset>

      {duplicateArticleNames.length > 0 ? (
        <label className="mt-4 flex items-start gap-2 rounded-lg border border-amber-600 p-3 text-sm text-foreground">
          <input type="checkbox" disabled={isSubmitting || unknownOutcome} checked={confirmDuplicates} onChange={(event) => setConfirmDuplicates(event.target.checked)} />
          <span>Artikli su ponovljeni: {duplicateArticleNames.join(", ")}. Potvrđujem da su to namerne odvojene stavke.</span>
        </label>
      ) : null}

      <div className="mt-5 rounded-lg border border-border p-4 text-sm text-foreground">
        <p>Broj stavki: <strong>{lines.length}</strong> · Ukupno komada: <strong>{lines.reduce((sum, line) => sum + (Number.isInteger(Number(line.quantity)) ? Number(line.quantity) : 0), 0)}</strong></p>
        <p className="mt-1">Zbir unetih nabavnih vrednosti: <strong>{preview.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD</strong></p>
        <p className="mt-1 text-xs text-muted">Orijentacioni zbir bez obračuna PDV-a i rabata. Nabavne i prodajne cene prijema neće prepisati cene artikla u šifarniku.</p>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-3 rounded-lg border border-border p-4 sm:grid-cols-2">
        <p className="text-sm text-muted sm:col-span-2">Knjiženje je po pravilu isključeno. Čak i kada je omogućeno, samo pojedinačno ovlašćeni operater može potvrditi prijem. Ključ se ne čuva u pregledaču.</p>
        <label className="grid gap-1 text-sm text-foreground">ID ovlašćenog operatera
          <input className="input-big" autoComplete="off" maxLength={80} value={operatorId} onChange={(event) => setOperatorId(event.target.value)} />
        </label>
        <label className="grid gap-1 text-sm text-foreground">Lični ključ operatera
          <input className="input-big" type="password" autoComplete="off" value={operatorKey} onChange={(event) => setOperatorKey(event.target.value)} />
        </label>
      </div>

      {error ? <p role="alert" className="mt-4 text-sm text-[var(--error)]">{error}</p> : null}
      <div className="mt-4 flex flex-col gap-3 sm:flex-row">
        <button type="button" className="button-big border border-border bg-surface-elevated text-foreground" disabled={isSubmitting || unknownOutcome} onClick={() => {
          if (!hasUnsavedChanges || window.confirm("Napustiti prijem? Unete stavke neće biti sačuvane.")) navigate("/unos-robe");
        }}>Nazad na zaglavlje</button>
        <button type="button" className="button-big bg-primary text-on-primary sm:min-w-64" disabled={isSubmitting || optionsLoading || !!optionsError || !!confirmedReceipt || (!unknownOutcome && duplicateArticleNames.length > 0 && !confirmDuplicates)} onClick={() => void submit()}>
          {isSubmitting ? "Potvrđujem prijem…" : unknownOutcome ? "Ponovi isti zahtev i proveri ishod" : `Pregledaj i potvrdi prijem (${lines.length})`}
        </button>
      </div>
    </section>
  );
}
