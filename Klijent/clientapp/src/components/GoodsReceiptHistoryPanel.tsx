import { useRef, useState } from "react";
import { apiUrl } from "../utils/apiUrl";

interface ReceiptSummary {
  id: number;
  status: string;
  supplierId: number;
  supplierName: string;
  storeId: number;
  documentType: string;
  documentNumber: string;
  documentDate: string;
  receivedDate: string;
  lineCount: number;
  totalQuantity: number;
  purchaseTotal: number;
  currency: string;
}

interface ReceiptDetail extends ReceiptSummary {
  createdBy: string;
  createdAtUtc: string;
  reversal?: { reversedBy: string; reversedAtUtc: string; reason: string } | null;
  lines: { id: number; lineNumber: number; articleId: number; articleName: string; quantity: number; purchaseUnitPrice: number; purchaseLineTotal: number; currency: string }[];
}

async function readResponse(response: Response): Promise<any> {
  let body: any;
  try {
    body = await response.json();
  } catch {
    throw new Error("Server je vratio neispravan odgovor za istoriju prijema.");
  }
  if (!response.ok) {
    const error = new Error(body?.detail ?? body?.message ?? `HTTP ${response.status}`);
    (error as Error & { status?: number }).status = response.status;
    throw error;
  }
  return body;
}

export default function GoodsReceiptHistoryPanel() {
  const [query, setQuery] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [adminKey, setAdminKey] = useState("");
  const [receipts, setReceipts] = useState<ReceiptSummary[]>([]);
  const [selected, setSelected] = useState<ReceiptDetail | null>(null);
  const [busy, setBusy] = useState(false);
  const [operatorId, setOperatorId] = useState("");
  const [operatorKey, setOperatorKey] = useState("");
  const [reversalReason, setReversalReason] = useState("");
  const reversalAttempt = useRef<{ fingerprint: string; requestKey: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const authorizedFetch = async (url: string) => {
    const send = () => fetch(apiUrl(url), { headers: adminKey ? { "X-Admin-Key": adminKey } : {} });
    let response = await send();
    if (response.status === 401) {
      const entered = window.prompt("Za pregled istorije prijema potreban je admin ključ.");
      if (!entered?.trim()) throw new Error("Pregled istorije je otkazan.");
      setAdminKey(entered.trim());
      const retry = await fetch(apiUrl(url), { headers: { "X-Admin-Key": entered.trim() } });
      return readResponse(retry);
    }
    return readResponse(response);
  };

  const searchReceipts = async () => {
    setBusy(true);
    setError(null);
    setSelected(null);
    try {
      const params = new URLSearchParams();
      if (query.trim()) params.set("query", query.trim());
      if (fromDate) params.set("fromDate", fromDate);
      if (toDate) params.set("toDate", toDate);
      params.set("page", "1");
      params.set("pageSize", "50");
      setReceipts(await authorizedFetch(`/api/goods-receipts?${params.toString()}`));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Istorija prijema nije dostupna.");
    } finally {
      setBusy(false);
    }
  };

  const openReceipt = async (id: number) => {
    setBusy(true);
    setError(null);
    setReversalReason("");
    reversalAttempt.current = null;
    try {
      setSelected(await authorizedFetch(`/api/goods-receipts/${id}`));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Detalji prijema nisu dostupni.");
    } finally {
      setBusy(false);
    }
  };


  const reverseReceipt = async () => {
    if (!selected || selected.status !== "confirmed" || busy) return;
    const reason = reversalReason.trim();
    if (reason.length < 10 || reason.length > 500) {
      setError("Razlog storna mora imati od 10 do 500 znakova.");
      return;
    }
    if (!adminKey || !operatorId.trim() || operatorKey.length < 32) {
      setError("Za storno su potrebni admin ključ, ID pojedinačnog operatera i njegov lični ključ.");
      return;
    }
    if (!window.confirm(`Stornirati ceo prijem #${selected.id}? Zaliha će biti umanjena za sve njegove stavke. Ova radnja se beleži u dnevniku.`)) return;

    const fingerprint = `${selected.id}|${reason}`;
    if (reversalAttempt.current?.fingerprint !== fingerprint) {
      reversalAttempt.current = { fingerprint, requestKey: crypto.randomUUID() };
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(apiUrl(`/api/goods-receipts/${selected.id}/reverse`), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Admin-Key": adminKey,
          "X-Receipt-Operator-Id": operatorId.trim(),
          "X-Receipt-Operator-Key": operatorKey,
        },
        body: JSON.stringify({ requestKey: reversalAttempt.current.requestKey, reason }),
      });
      if (response.status === 404)
        throw new Error("Storno nije omogućen ili prijem nije pronađen. Obratite se administratoru.");
      await readResponse(response);
      const updated = await authorizedFetch(`/api/goods-receipts/${selected.id}`) as ReceiptDetail;
      setSelected(updated);
      setReceipts((current) => current.map((receipt) => receipt.id === updated.id
        ? { ...receipt, status: updated.status } : receipt));
      setReversalReason("");
      reversalAttempt.current = null;
    } catch (e) {
      // Keep the request key for a safe retry after an unknown network outcome.
      setError(e instanceof Error ? e.message : "Ishod storna nije poznat. Proverite istoriju ili ponovite isti zahtev.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <details className="rounded-xl border border-border bg-surface p-4">
      <summary className="cursor-pointer text-lg font-semibold text-foreground">Istorija prijema robe</summary>
      <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-[2fr_1fr_1fr_auto]">
        <label className="grid gap-1 text-sm font-medium text-foreground">Dokument, dobavljač ili ID prijema
          <input className="input-big w-full" value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") void searchReceipts(); }} />
        </label>
        <label className="grid gap-1 text-sm font-medium text-foreground">Od datuma prijema<input className="input-big w-full" type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} /></label>
        <label className="grid gap-1 text-sm font-medium text-foreground">Do datuma prijema<input className="input-big w-full" type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} /></label>
        <button type="button" className="button-big self-end bg-primary text-on-primary" onClick={() => void searchReceipts()} disabled={busy}>{busy ? "Učitavanje…" : "Pretraži"}</button>
      </div>
      {error ? <p role="alert" className="mt-3 text-sm text-[var(--error)]">{error}</p> : null}
      {receipts.length ? (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead><tr className="border-b border-border text-muted"><th className="p-2">ID</th><th className="p-2">Dokument</th><th className="p-2">Dobavljač</th><th className="p-2">Prijem</th><th className="p-2">Lokacija</th><th className="p-2">Stavke / komadi</th><th className="p-2">Status</th><th className="p-2">Detalji</th></tr></thead>
            <tbody>{receipts.map((receipt) => <tr key={receipt.id} className="border-b border-border text-foreground">
              <td className="p-2">{receipt.id}</td>
              <td className="p-2">{receipt.documentNumber} ({receipt.documentType === "invoice" ? "račun" : "otpremnica"})</td>
              <td className="p-2">{receipt.supplierName}</td>
              <td className="p-2">{receipt.receivedDate}</td>
              <td className="p-2">{receipt.storeId}</td>
              <td className="p-2">{receipt.lineCount} / {receipt.totalQuantity}</td>
              <td className="p-2">{receipt.status === "reversed" ? "Stornirano" : "Potvrđeno"}</td>
              <td className="p-2"><button type="button" className="underline" onClick={() => void openReceipt(receipt.id)}>Otvori</button></td>
            </tr>)}</tbody>
          </table>
        </div>
      ) : null}
      {selected ? (
        <section className="mt-4 rounded-lg border border-border p-4" aria-label={`Prijem ${selected.id}`}>
          <h3 className="font-semibold text-foreground">Prijem {selected.id} · {selected.status}</h3>
          <p className="mt-1 text-sm text-muted">Dokument {selected.documentNumber}, dobavljač {selected.supplierName}, datum {selected.documentDate}, prijem {selected.receivedDate}, lokacija {selected.storeId}; uneo {selected.createdBy} {new Date(selected.createdAtUtc).toLocaleString("sr-RS")}.</p>
          <ul className="mt-3 space-y-1 text-sm text-foreground">{selected.lines.map((line) => <li key={line.id}>{line.lineNumber}. {line.articleName} (ID {line.articleId}) — {line.quantity} × {line.purchaseUnitPrice.toLocaleString("sr-RS", { minimumFractionDigits: 2 })} {line.currency}; ukupno {line.purchaseLineTotal.toLocaleString("sr-RS", { minimumFractionDigits: 2 })}</li>)}</ul>
          {selected.reversal ? (
            <p role="status" className="mt-3 rounded-lg border border-border p-3 text-sm text-foreground">
              <strong>Stornirano</strong> · {selected.reversal.reversedBy} · {new Date(selected.reversal.reversedAtUtc).toLocaleString("sr-RS")}
              <span className="mt-1 block">Razlog: {selected.reversal.reason}</span>
            </p>
          ) : null}
          {selected.status === "confirmed" ? (
            <section className="mt-4 rounded-lg border border-border p-3">
              <h4 className="font-semibold text-foreground">Storno celog prijema</h4>
              <p className="mt-1 text-sm text-muted">Korekcija je dozvoljena samo ako trenutno stanje pokriva celu količinu. Originalni prijem i potpisana negativna kretanja ostaju u dnevniku. Knjiženje/storno su isključeni dok ih administrator izričito ne odobri.</p>
              <div className="mt-3 grid gap-3 sm:grid-cols-2">
                <label className="grid gap-1 text-sm text-foreground">ID operatera
                  <input className="input-big" autoComplete="off" maxLength={80} value={operatorId} onChange={(e) => setOperatorId(e.target.value)} />
                </label>
                <label className="grid gap-1 text-sm text-foreground">Lični ključ operatera
                  <input className="input-big" type="password" autoComplete="off" value={operatorKey} onChange={(e) => setOperatorKey(e.target.value)} />
                </label>
                <label className="grid gap-1 text-sm text-foreground sm:col-span-2">Razlog storna (10–500 karaktera)
                  <textarea className="input-big w-full" rows={2} maxLength={500} value={reversalReason} onChange={(e) => setReversalReason(e.target.value)} />
                </label>
              </div>
              <button type="button" className="button-big mt-3 border border-border bg-surface-elevated text-foreground" disabled={busy || reversalReason.trim().length < 10 || !operatorId.trim() || operatorKey.length < 32} onClick={() => void reverseReceipt()}>
                {busy ? "Obrađujem…" : "Storniraj potvrđeni prijem"}
              </button>
            </section>
          ) : null}
        </section>
      ) : null}
      {!busy && receipts.length === 0 ? <p className="mt-4 text-sm text-muted">Unesite kriterijum i izaberite „Pretraži” da pronađete sačuvane prijeme.</p> : null}
    </details>
  );
}
