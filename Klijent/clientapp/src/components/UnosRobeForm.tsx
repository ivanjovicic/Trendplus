import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getStores } from "../services/analyticsApi";

interface Dobavljac {
  id: number;
  naziv: string;
}

interface UnosRobeFormProps {
  dobavljaci: Dobavljac[];
}

const LEGACY_RECENTS_KEY = "trendplus.unosRobe.recentDobavljaci";

function localToday(): string {
  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}

export default function UnosRobeForm({ dobavljaci }: UnosRobeFormProps) {
  const navigate = useNavigate();
  const [stores, setStores] = useState<{ storeId: number; storeName: string }[]>([]);
  const [storesLoading, setStoresLoading] = useState(true);
  const [storesError, setStoresError] = useState<string | null>(null);
  const [supplierId, setSupplierId] = useState<number | null>(null);
  const [documentNumber, setDocumentNumber] = useState("");
  const [documentType, setDocumentType] = useState<"invoice" | "delivery-note">("invoice");
  const [documentDate, setDocumentDate] = useState(localToday);
  const [receivedDate, setReceivedDate] = useState(localToday);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [comment, setComment] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    try {
      // Remove supplier names, phone numbers and invoice references saved by the old flow.
      localStorage.removeItem(LEGACY_RECENTS_KEY);
    } catch {
      // Storage may be disabled; no receipt data is written to browser storage.
    }
  }, []);

  useEffect(() => {
    let active = true;
    void getStores(false).then((result) => {
      if (!active) return;
      setStores(result.filter((item) => Number.isInteger(item.storeId) && !!item.storeName));
      setStoresError(null);
    }).catch(() => {
      if (active) setStoresError("Lokacije nisu dostupne. Osvežite stranicu ili proverite vezu.");
    }).finally(() => {
      if (active) setStoresLoading(false);
    });
    return () => { active = false; };
  }, []);

  const selectedSupplier = useMemo(
    () => dobavljaci.find((item) => item.id === supplierId) ?? null,
    [dobavljaci, supplierId],
  );
  const documentReady = documentNumber.trim().length >= 1;
  const canContinue = !!selectedSupplier && documentReady && storeId !== null && !storesLoading;

  const handleContinue = () => {
    setError(null);
    if (!selectedSupplier) {
      setError("Izaberite dobavljača.");
      return;
    }
    if (!documentReady) {
      setError("Unesite broj računa ili otpremnice.");
      return;
    }
    if (storeId === null || !stores.some((store) => store.storeId === storeId)) {
      setError("Izaberite važeću prodavnicu ili skladište.");
      return;
    }
    if (!documentDate || !receivedDate || comment.length > 500) {
      setError("Proverite datume i komentar (najviše 500 znakova).");
      return;
    }

    navigate("/artikli", {
      state: {
        receiptHeader: {
          supplierId: selectedSupplier.id,
          supplierName: selectedSupplier.naziv,
          documentNumber: documentNumber.trim(),
          documentType,
          documentDate,
          receivedDate,
          storeId,
          storeName: stores.find((store) => store.storeId === storeId)?.storeName ?? "",
          comment: comment.trim(),
          requestKey: crypto.randomUUID(),
        },
      },
    });
  };

  return (
    <section className="mobile-entry-form rounded-xl border border-border bg-surface p-4 sm:p-5" aria-labelledby="goods-receipt-heading">
      <h2 id="goods-receipt-heading" className="mb-4 text-xl font-semibold text-foreground">Podaci o prijemu</h2>
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <label className="grid gap-1.5 text-sm font-medium text-foreground">
          Dobavljač <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <select className="input-big w-full" value={supplierId ?? ""} onChange={(event) => setSupplierId(event.target.value === "" ? null : Number(event.target.value))}>
            <option value="">Izaberite dobavljača</option>
            {dobavljaci.map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.naziv} (ID {supplier.id})</option>)}
          </select>
        </label>

        <label className="grid gap-1.5 text-sm font-medium text-foreground">
          Vrsta dokumenta <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <select className="input-big w-full" value={documentType} onChange={(event) => setDocumentType(event.target.value as "invoice" | "delivery-note")}>
            <option value="invoice">Račun</option>
            <option value="delivery-note">Otpremnica</option>
          </select>
        </label>

        <label className="grid gap-1.5 text-sm font-medium text-foreground md:col-span-2">
          Broj računa ili otpremnice <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <input className="input-big w-full" value={documentNumber} onChange={(event) => setDocumentNumber(event.target.value)} maxLength={100} autoComplete="off" />
        </label>

        <label className="grid gap-1.5 text-sm font-medium text-foreground">
          Datum dokumenta <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <input className="input-big w-full" type="date" value={documentDate} onChange={(event) => setDocumentDate(event.target.value)} />
        </label>
        <label className="grid gap-1.5 text-sm font-medium text-foreground">
          Datum fizičkog prijema <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <input className="input-big w-full" type="date" value={receivedDate} onChange={(event) => setReceivedDate(event.target.value)} />
        </label>

        <label className="grid gap-1.5 text-sm font-medium text-foreground md:col-span-2">
          Prodavnica ili skladište prijema <span className="text-[var(--error)]" aria-hidden="true">*</span>
          <select className="input-big w-full" value={storeId ?? ""} onChange={(event) => setStoreId(event.target.value === "" ? null : Number(event.target.value))} disabled={storesLoading || stores.length === 0}>
            <option value="">{storesLoading ? "Učitavanje lokacija…" : "Izaberite lokaciju"}</option>
            {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName} (ID {store.storeId})</option>)}
          </select>
          {storesError ? <span role="alert" className="text-sm text-[var(--error)]">{storesError}</span> : null}
          {!storesLoading && !storesError && stores.length === 0 ? <span role="alert" className="text-sm text-[var(--error)]">Nema potvrđenih lokacija; prijem nije moguće nastaviti.</span> : null}
        </label>

        <label className="grid gap-1.5 text-sm font-medium text-foreground md:col-span-2">
          Komentar (opciono)
          <textarea className="input-big w-full" rows={3} maxLength={500} value={comment} onChange={(event) => setComment(event.target.value)} />
        </label>
      </div>
      {error ? <p role="alert" className="mt-3 text-sm text-[var(--error)]">{error}</p> : null}
      <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-xs text-muted">Zaliha se knjiži tek nakon serverske potvrde celog prijema. Valuta: RSD.</p>
        <button type="button" className="button-big bg-primary text-on-primary" disabled={!canContinue} onClick={handleContinue}>
          Nastavi na stavke
        </button>
      </div>
    </section>
  );
}
