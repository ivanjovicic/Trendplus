import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { getInventoryList, getStores } from "../services/analyticsApi";
import type { InventoryListItem, StoreOption } from "../types/analytics";
import {
  cancelTransfer,
  completeTransfer,
  confirmTransfer,
  createTransfer,
  getTransfer,
  listTransfers,
  type TransferCreateRequest,
  type TransferListItemProjection,
  type TransferResponse,
  type TransferUpdateRequest,
  updateTransfer,
} from "../services/transferApi";

type TransferLineDraft = {
  skuId: number;
  skuName: string;
  skuCode?: string;
  available: number;
  quantity: number;
  unit?: string;
};

const INITIAL_STATUS_TEXT = "Kreiraj draft, potvrdi transfer, pa ga zavrsi.";

function getStoreName(stores: StoreOption[], id: number): string {
  return stores.find((x) => x.storeId === id)?.storeName ?? `Objekat #${id}`;
}

export default function TransferPage() {
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [sourceId, setSourceId] = useState<number | null>(null);
  const [destinationId, setDestinationId] = useState<number | null>(null);
  const [reserve, setReserve] = useState(true);
  const [notes, setNotes] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<InventoryListItem[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [scannerMode, setScannerMode] = useState(true);
  const [scannerInput, setScannerInput] = useState("");
  const [scannerBusy, setScannerBusy] = useState(false);
  const scannerInputRef = useRef<HTMLInputElement | null>(null);
  const [lines, setLines] = useState<TransferLineDraft[]>([]);
  const [lineErrors, setLineErrors] = useState<Record<number, string>>({});
  const [transfers, setTransfers] = useState<TransferListItemProjection[]>([]);
  const [actorFilterInput, setActorFilterInput] = useState("");
  const [actorFilterApplied, setActorFilterApplied] = useState("");
  const [selectedTransfer, setSelectedTransfer] = useState<TransferResponse | null>(null);
  const [working, setWorking] = useState(false);
  const [statusText, setStatusText] = useState(INITIAL_STATUS_TEXT);

  const selectedTransferId = selectedTransfer?.id ?? null;
  const selectedTransferStatus = selectedTransfer?.status ?? "draft";
  const isDraftEditable = !selectedTransfer || selectedTransfer.status === "draft";

  const loadTransfers = useCallback(async () => {
    const actor = actorFilterApplied.trim();
    const response = await listTransfers({
      pageNumber: 1,
      pageSize: 30,
      actor: actor.length > 0 ? actor : undefined,
    });
    setTransfers(response.items);
  }, [actorFilterApplied]);

  const loadStores = useCallback(async () => {
    const result = await getStores();
    setStores(result);
    if (result.length > 0 && sourceId == null) setSourceId(result[0].storeId);
    if (result.length > 1 && destinationId == null) setDestinationId(result[1].storeId);
  }, [destinationId, sourceId]);

  useEffect(() => {
    void (async () => {
      try {
        await Promise.all([loadStores(), loadTransfers()]);
      } catch (err) {
        setStatusText(err instanceof Error ? err.message : "Neuspesno ucitavanje transfer stranice.");
      }
    })();
  }, [loadStores, loadTransfers]);

  useEffect(() => {
    if (!sourceId || !searchQuery.trim()) {
      setSearchResults([]);
      return;
    }

    const timeout = window.setTimeout(async () => {
      try {
        setSearchLoading(true);
        const response = await getInventoryList({
          pageNumber: 1,
          pageSize: 50,
          search: searchQuery.trim(),
          storeId: sourceId,
        });
        setSearchResults(response.items);
      } catch (err) {
        setStatusText(err instanceof Error ? err.message : "Neuspesna pretraga artikala.");
      } finally {
        setSearchLoading(false);
      }
    }, 250);

    return () => window.clearTimeout(timeout);
  }, [searchQuery, sourceId]);

  useEffect(() => {
    if (!scannerMode) return;
    scannerInputRef.current?.focus();
  }, [scannerMode]);

  const totalQty = useMemo(() => lines.reduce((sum, line) => sum + line.quantity, 0), [lines]);

  const addLine = useCallback((item: InventoryListItem) => {
    const available = Number(item.kolicina ?? 0);
    setLines((prev) => {
      const existing = prev.find((x) => x.skuId === item.id);
      if (existing) {
        return prev.map((x) =>
          x.skuId === item.id
            ? { ...x, quantity: Math.min(x.quantity + 1, Math.max(available, 1)) }
            : x
        );
      }

      return [
        ...prev,
        {
          skuId: item.id,
          skuName: item.naziv,
          skuCode: item.plu ?? undefined,
          available,
          quantity: available > 0 ? 1 : 0,
        },
      ];
    });
  }, []);

  const removeLine = useCallback((skuId: number) => {
    setLines((prev) => prev.filter((x) => x.skuId !== skuId));
    setLineErrors((prev) => {
      const next = { ...prev };
      delete next[skuId];
      return next;
    });
  }, []);

  const updateLineQuantity = useCallback((skuId: number, quantity: number) => {
    setLines((prev) => prev.map((x) => (x.skuId === skuId ? { ...x, quantity } : x)));
  }, []);

  const scanAndAddArticle = useCallback(async () => {
    const scanToken = scannerInput.trim();
    if (!scannerMode || !isDraftEditable || !scanToken) return;
    if (!sourceId) {
      setStatusText("Izaberi source radnju pre skeniranja.");
      return;
    }

    setScannerBusy(true);
    try {
      const response = await getInventoryList({
        pageNumber: 1,
        pageSize: 20,
        search: scanToken,
        storeId: sourceId,
      });

      const normalized = scanToken.toLowerCase();
      const exact = response.items.find((item) => (item.plu ?? "").trim().toLowerCase() === normalized)
        ?? response.items.find((item) => String(item.id) === scanToken);
      const picked = exact ?? response.items[0];

      if (!picked) {
        setStatusText(`Nije pronadjen artikal za sken '${scanToken}'.`);
        return;
      }

      addLine(picked);
      setScannerInput("");
      setStatusText(`Skenirano i dodato: ${picked.naziv} (${picked.plu ?? `SKU #${picked.id}`}).`);
    } catch (err) {
      setStatusText(err instanceof Error ? err.message : "Skeniranje artikla nije uspelo.");
    } finally {
      setScannerBusy(false);
      scannerInputRef.current?.focus();
    }
  }, [addLine, isDraftEditable, scannerInput, scannerMode, sourceId]);

  const validateDraft = useCallback((): boolean => {
    const errors: Record<number, string> = {};
    if (!sourceId || !destinationId) {
      setStatusText("Moras izabrati source i destination radnju.");
      return false;
    }

    if (sourceId === destinationId) {
      setStatusText("Source i destination radnja moraju biti razlicite.");
      return false;
    }

    if (lines.length === 0) {
      setStatusText("Dodaj bar jednu stavku transfera.");
      return false;
    }

    for (const line of lines) {
      if (line.quantity <= 0) errors[line.skuId] = "Kolicina mora biti veca od nule.";
      else if (line.quantity > line.available) errors[line.skuId] = `Nedovoljno zalihe (max ${line.available}).`;
    }

    setLineErrors(errors);
    if (Object.keys(errors).length > 0) {
      setStatusText("Ispravi stavke sa greskom pre snimanja.");
      return false;
    }

    return true;
  }, [destinationId, lines, sourceId]);

  const buildCreateRequest = useCallback((): TransferCreateRequest => ({
    sourceId: sourceId!,
    destinationId: destinationId!,
    sourceType: "store",
    destinationType: "store",
    reserve,
    notes: notes.trim() || undefined,
    items: lines.map((line) => ({ skuId: line.skuId, quantity: line.quantity, unit: line.unit })),
  }), [destinationId, lines, notes, reserve, sourceId]);

  const buildUpdateRequest = useCallback((): TransferUpdateRequest => ({
    reserve,
    notes: notes.trim() || undefined,
    items: lines.map((line) => ({ skuId: line.skuId, quantity: line.quantity, unit: line.unit })),
  }), [lines, notes, reserve]);

  const syncFromResponse = useCallback((response: TransferResponse) => {
    setSelectedTransfer(response);
    setSourceId(response.sourceId);
    setDestinationId(response.destinationId);
    setReserve(response.reserve);
    setNotes(response.notes ?? "");
    setLines(response.items.map((x) => ({
      skuId: x.skuId,
      skuName: x.skuName ?? `SKU #${x.skuId}`,
      skuCode: x.skuCode,
      available: Number(x.availableQuantity ?? 0),
      quantity: Number(x.quantity),
      unit: x.unit,
    })));
    setLineErrors({});
  }, []);

  const saveDraft = useCallback(async (): Promise<TransferResponse | null> => {
    if (!validateDraft()) return null;
    setWorking(true);
    try {
      let response: TransferResponse;
      if (selectedTransferId && selectedTransferStatus === "draft") {
        response = await updateTransfer(selectedTransferId, buildUpdateRequest());
      } else {
        response = await createTransfer(buildCreateRequest());
      }
      syncFromResponse(response);
      await loadTransfers();
      setStatusText(`Draft #${response.id} je snimljen.`);
      return response;
    } catch (err) {
      setStatusText(err instanceof Error ? err.message : "Snimanje draft-a nije uspelo.");
      return null;
    } finally {
      setWorking(false);
    }
  }, [
    buildCreateRequest,
    buildUpdateRequest,
    loadTransfers,
    selectedTransferId,
    selectedTransferStatus,
    syncFromResponse,
    validateDraft,
  ]);

  const runStateAction = useCallback(async (
    action: (id: number) => Promise<TransferResponse>,
    successLabel: string
  ) => {
    const existing = selectedTransfer;
    const current = existing ?? (await saveDraft());
    if (!current) return;

    setWorking(true);
    try {
      const response = await action(current.id);
      syncFromResponse(response);
      await loadTransfers();
      setStatusText(`${successLabel} (#${response.id}).`);
    } catch (err) {
      setStatusText(err instanceof Error ? err.message : "Promena statusa transfera nije uspela.");
    } finally {
      setWorking(false);
    }
  }, [loadTransfers, saveDraft, selectedTransfer, syncFromResponse]);

  const loadTransferDetails = useCallback(async (id: number) => {
    setWorking(true);
    try {
      const response = await getTransfer(id);
      syncFromResponse(response);
      setStatusText(`Ucitan transfer #${id}.`);
    } catch (err) {
      setStatusText(err instanceof Error ? err.message : "Ucitavanje transfer detalja nije uspelo.");
    } finally {
      setWorking(false);
    }
  }, [syncFromResponse]);

  const resetDraft = useCallback(() => {
    setSelectedTransfer(null);
    setNotes("");
    setReserve(true);
    setLines([]);
    setLineErrors({});
    setSearchQuery("");
    setSearchResults([]);
    setScannerInput("");
    setStatusText(INITIAL_STATUS_TEXT);
  }, []);

  return (
    <div className="mx-auto max-w-7xl space-y-4 px-4 py-4 text-[var(--text-primary)]">
      <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
        <h1 className="text-xl font-semibold">Prenosi robe</h1>
        <p className="mt-1 text-sm text-[var(--text-muted)]">
          Draft - Confirm - Complete workflow sa atomskim OUT/IN stock movement zapisima.
        </p>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <section className="space-y-3 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 lg:col-span-2">
          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-secondary)]">Iz radnje</label>
              <select
                className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-2 text-sm"
                value={sourceId ?? ""}
                disabled={working || !isDraftEditable}
                onChange={(e) => setSourceId(Number(e.target.value))}
              >
                <option value="" disabled>Izaberi source radnju</option>
                {stores.map((store) => (
                  <option key={store.storeId} value={store.storeId}>
                    {store.storeName}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-secondary)]">U radnju</label>
              <select
                className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-2 text-sm"
                value={destinationId ?? ""}
                disabled={working || !isDraftEditable}
                onChange={(e) => setDestinationId(Number(e.target.value))}
              >
                <option value="" disabled>Izaberi destination radnju</option>
                {stores.map((store) => (
                  <option key={store.storeId} value={store.storeId}>
                    {store.storeName}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-3">
            <div className="md:col-span-2">
              <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-secondary)]">Brza pretraga artikla (naziv / PLU)</label>
              <input
                type="text"
                className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-2 text-sm"
                placeholder="Npr. Nike Air, 12345..."
                value={searchQuery}
                disabled={working || !isDraftEditable || !sourceId}
                onChange={(e) => setSearchQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && searchResults.length > 0 && isDraftEditable && !scannerBusy) {
                    e.preventDefault();
                    addLine(searchResults[0]);
                  }
                }}
              />
            </div>
            <label className="mt-5 inline-flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={reserve}
                disabled={working || !isDraftEditable}
                onChange={(e) => setReserve(e.target.checked)}
              />
              Rezervisi stock na potvrdi
            </label>
          </div>

          <div className="grid gap-3 rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] p-3 md:grid-cols-4">
            <label className="inline-flex items-center gap-2 text-sm md:col-span-1">
              <input
                type="checkbox"
                checked={scannerMode}
                disabled={working || !isDraftEditable}
                onChange={(e) => setScannerMode(e.target.checked)}
              />
              Scanner mode
            </label>
            <div className="md:col-span-3">
              <input
                ref={scannerInputRef}
                type="text"
                className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm"
                placeholder="Skeniraj barcode/PLU pa Enter"
                value={scannerInput}
                disabled={working || scannerBusy || !isDraftEditable || !scannerMode || !sourceId}
                onChange={(e) => setScannerInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    void scanAndAddArticle();
                  }
                }}
              />
            </div>
          </div>

          {(searchLoading || searchResults.length > 0) && (
            <div className="max-h-52 overflow-y-auto rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)]">
              {searchLoading ? (
                <div className="p-3 text-sm text-[var(--text-muted)]">Pretraga u toku...</div>
              ) : (
                <table className="w-full text-sm">
                  <thead className="sticky top-0 bg-[var(--surface-elevated)] text-[var(--text-secondary)]">
                    <tr>
                      <th className="px-3 py-2 text-left">Artikal</th>
                      <th className="px-3 py-2 text-right">Dostupno</th>
                      <th className="px-3 py-2 text-right">Akcija</th>
                    </tr>
                  </thead>
                  <tbody>
                    {searchResults.map((item) => (
                      <tr key={item.id} className="border-t border-[var(--border-default)]">
                        <td className="px-3 py-2">
                          <div className="font-medium">{item.naziv}</div>
                          <div className="text-xs text-[var(--text-muted)]">{item.plu ?? `SKU #${item.id}`}</div>
                        </td>
                        <td className="px-3 py-2 text-right">{item.kolicina ?? 0}</td>
                        <td className="px-3 py-2 text-right">
                          <button
                            type="button"
                            className="rounded-lg border border-[var(--border-default)] px-3 py-1 text-xs font-semibold"
                            disabled={!isDraftEditable}
                            onClick={() => addLine(item)}
                          >
                            Dodaj
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-secondary)]">Napomena</label>
            <textarea
              className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-2 text-sm"
              rows={2}
              value={notes}
              disabled={working || !isDraftEditable}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>

          <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
            <table className="w-full min-w-[680px] text-sm">
              <thead className="bg-[var(--surface-default)] text-[var(--text-secondary)]">
                <tr>
                  <th className="px-3 py-2 text-left">Artikal</th>
                  <th className="px-3 py-2 text-right">Dostupno</th>
                  <th className="px-3 py-2 text-right">Kolicina</th>
                  <th className="px-3 py-2 text-right">Akcija</th>
                </tr>
              </thead>
              <tbody>
                {lines.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="px-3 py-5 text-center text-[var(--text-muted)]">
                      Dodaj stavke kroz pretragu iznad.
                    </td>
                  </tr>
                ) : (
                  lines.map((line) => (
                    <tr key={line.skuId} className="border-t border-[var(--border-default)]">
                      <td className="px-3 py-2">
                        <div className="font-medium">{line.skuName}</div>
                        <div className="text-xs text-[var(--text-muted)]">{line.skuCode ?? `SKU #${line.skuId}`}</div>
                        {lineErrors[line.skuId] && (
                          <div className="mt-1 text-xs text-[var(--error)]">{lineErrors[line.skuId]}</div>
                        )}
                      </td>
                      <td className="px-3 py-2 text-right">{line.available}</td>
                      <td className="px-3 py-2 text-right">
                        <input
                          type="number"
                          min={0}
                          step={1}
                          className="w-24 rounded-lg border border-[var(--border-default)] bg-[var(--surface-default)] px-2 py-1 text-right"
                          disabled={working || !isDraftEditable}
                          value={line.quantity}
                          onChange={(e) => updateLineQuantity(line.skuId, Number(e.target.value))}
                        />
                      </td>
                      <td className="px-3 py-2 text-right">
                        <button
                          type="button"
                          className="rounded-lg border border-[var(--error)] px-2 py-1 text-xs text-[var(--text-on-error)] bg-[var(--error)]"
                          disabled={working || !isDraftEditable}
                          onClick={() => removeLine(line.skuId)}
                        >
                          Ukloni
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
              <tfoot className="border-t border-[var(--border-default)] bg-[var(--surface-default)]">
                <tr>
                  <td className="px-3 py-2 font-semibold" colSpan={2}>Ukupno stavki: {lines.length}</td>
                  <td className="px-3 py-2 text-right font-semibold" colSpan={2}>Ukupna kolicina: {totalQty}</td>
                </tr>
              </tfoot>
            </table>
          </div>

          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-default)] px-4 py-2 text-sm font-semibold"
              onClick={() => void saveDraft()}
              disabled={working || !isDraftEditable}
            >
              Sacuvaj draft
            </button>
            <button
              type="button"
              className="rounded-lg border border-[var(--focus-ring)] bg-[var(--focus-ring)] px-4 py-2 text-sm font-semibold text-[var(--text-on-primary)]"
              onClick={() => void runStateAction(confirmTransfer, "Transfer potvrdjen")}
              disabled={working || !isDraftEditable}
            >
              Potvrdi transfer
            </button>
            <button
              type="button"
              className="rounded-lg border border-[var(--success)] bg-[var(--success)] px-4 py-2 text-sm font-semibold text-[var(--text-on-success)]"
              onClick={() => void runStateAction(completeTransfer, "Transfer zavrsen")}
              disabled={working || selectedTransferStatus !== "confirmed"}
            >
              Zavrsi transfer
            </button>
            <button
              type="button"
              className="rounded-lg border border-[var(--error)] px-4 py-2 text-sm font-semibold text-[var(--error)]"
              onClick={() => void runStateAction(cancelTransfer, "Transfer otkazan")}
              disabled={working || !selectedTransferId || selectedTransferStatus === "completed" || selectedTransferStatus === "cancelled"}
            >
              Otkazi transfer
            </button>
            <button
              type="button"
              className="rounded-lg border border-[var(--border-default)] px-4 py-2 text-sm font-semibold"
              onClick={resetDraft}
              disabled={working}
            >
              Novi transfer
            </button>
          </div>
          <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-2 text-sm text-[var(--text-secondary)]">
            {statusText}
          </div>
        </section>

        <aside className="space-y-3 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
          <h2 className="text-base font-semibold">Skoriji prenosi</h2>
          <div className="space-y-2 rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] p-3">
            <label className="block text-xs uppercase tracking-wide text-[var(--text-secondary)]">
              Filter po akteru (audit)
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                className="flex-1 rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-1.5 text-sm"
                value={actorFilterInput}
                onChange={(e) => setActorFilterInput(e.target.value)}
                placeholder="npr. admin@trendplus"
              />
              <button
                type="button"
                className="rounded-lg border border-[var(--border-default)] px-2 py-1 text-xs font-semibold"
                onClick={() => {
                  setActorFilterApplied(actorFilterInput.trim());
                }}
              >
                Primeni
              </button>
              <button
                type="button"
                className="rounded-lg border border-[var(--border-default)] px-2 py-1 text-xs font-semibold"
                onClick={() => {
                  setActorFilterInput("");
                  setActorFilterApplied("");
                }}
              >
                Reset
              </button>
            </div>
          </div>
          <div className="max-h-[640px] space-y-2 overflow-y-auto pr-1">
            {transfers.length === 0 ? (
              <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-default)] px-3 py-4 text-sm text-[var(--text-muted)]">
                Nema transfer dokumenata.
              </div>
            ) : (
              transfers.map((item) => (
                <button
                  type="button"
                  key={item.id}
                  className={`w-full rounded-xl border px-3 py-3 text-left ${
                    selectedTransferId === item.id
                      ? "border-[var(--focus-ring)] bg-[var(--surface-default)]"
                      : "border-[var(--border-default)] bg-[var(--surface-default)]"
                  }`}
                  onClick={() => void loadTransferDetails(item.id)}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold">#{item.id}</span>
                    <span className="rounded-md border border-[var(--border-default)] px-2 py-0.5 text-xs uppercase tracking-wide">
                      {item.status}
                    </span>
                  </div>
                  <div className="mt-1 text-xs text-[var(--text-muted)]">
                    {getStoreName(stores, item.sourceId)} {"->"} {getStoreName(stores, item.destinationId)}
                  </div>
                  <div className="mt-1 text-xs text-[var(--text-muted)]">
                    Stavke: {item.itemCount} | Kolicina: {item.totalQuantity}
                  </div>
                  <div className="mt-1 text-xs text-[var(--text-muted)]">
                    Akter: {item.updatedBy ?? item.createdBy ?? "-"}
                  </div>
                </button>
              ))
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}
