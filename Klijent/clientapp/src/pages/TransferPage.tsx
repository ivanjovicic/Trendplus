import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Boxes } from "lucide-react";
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
import {
  FormField,
  FormLayout,
  FormPageShell,
  FormProgress,
  FormSection,
  LineItemsEditor,
  ReadonlyField,
  StickyActionBar,
  SummaryPanel,
  ValidationChecklist,
  type LineItem,
} from "../components/forms/FormSystem";

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
    if (available <= 0) {
      setStatusText(`Nema dostupnih zaliha za '${item.naziv}'.`);
      return;
    }

    setLines((prev) => {
      const existing = prev.find((x) => x.skuId === item.id);
      if (existing) {
        return prev.map((x) =>
          x.skuId === item.id ? { ...x, quantity: Math.min(x.quantity + 1, available) } : x
        );
      }

      return [
        ...prev,
        {
          skuId: item.id,
          skuName: item.naziv,
          skuCode: item.plu ?? undefined,
          available,
          quantity: 1,
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

      if (Number(picked.kolicina ?? 0) <= 0) {
        setStatusText(`Artikal '${picked.naziv}' nema dostupnih zaliha za prenos.`);
      } else {
        addLine(picked);
        setScannerInput("");
        setStatusText(`Skenirano i dodato: ${picked.naziv} (${picked.plu ?? `SKU #${picked.id}`}).`);
      }
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

  const lineRows: Array<LineItem<TransferLineDraft>> = lines.map((line) => ({
    id: line.skuId,
    title: line.skuName,
    status: lineErrors[line.skuId] ? "error" : "ok",
    error: lineErrors[line.skuId] ?? null,
    data: line,
  }));

  const setupReady = !!sourceId && !!destinationId && sourceId !== destinationId;
  const draftReady = setupReady && lines.length > 0 && Object.keys(lineErrors).length === 0;
  const primaryDisabledReason = !setupReady
    ? "Izaberite dve različite radnje."
    : lines.length === 0
      ? "Dodajte bar jednu stavku transfera."
      : Object.keys(lineErrors).length > 0
        ? "Ispravite količine sa greškom."
        : undefined;

  return (
    <FormPageShell
      icon={Boxes}
      title="Prenosi robe"
      subtitle="Draft, potvrda i završavanje transfera sa jasno odvojenim unosom, statusom i istorijom."
    >
      <FormProgress
        steps={[
          { label: "Radnje", state: setupReady ? "complete" : "pending" },
          { label: `Stavke: ${lines.length}`, state: lines.length > 0 ? "complete" : "pending" },
          { label: selectedTransferStatus, state: selectedTransferStatus === "draft" ? "pending" : "complete" },
        ]}
      />

      <FormLayout
        main={(
          <>
            <FormSection title="Lokacije" description="Izvor i odredište moraju biti različiti." complete={setupReady}>
              <div className="form-grid form-grid--two">
                <FormField label="Iz radnje" required>
                  <select
                    className="form-control"
                    value={sourceId ?? ""}
                    disabled={working || !isDraftEditable}
                    onChange={(event) => setSourceId(Number(event.target.value))}
                  >
                    <option value="" disabled>Izaberite radnju</option>
                    {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName}</option>)}
                  </select>
                </FormField>
                <FormField label="U radnju" required>
                  <select
                    className="form-control"
                    value={destinationId ?? ""}
                    disabled={working || !isDraftEditable}
                    onChange={(event) => setDestinationId(Number(event.target.value))}
                  >
                    <option value="" disabled>Izaberite radnju</option>
                    {stores.map((store) => <option key={store.storeId} value={store.storeId}>{store.storeName}</option>)}
                  </select>
                </FormField>
              </div>
              <label className="validation-list__item">
                <input
                  type="checkbox"
                  checked={reserve}
                  disabled={working || !isDraftEditable}
                  onChange={(event) => setReserve(event.target.checked)}
                />
                Rezerviši stock na potvrdi
              </label>
            </FormSection>

            <FormSection title="Dodavanje artikala" description="Koristite pretragu ili scanner mode za brz unos." complete={lines.length > 0}>
              <div className="form-grid form-grid--two">
                <FormField label="Brza pretraga artikla">
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Naziv / PLU"
                    value={searchQuery}
                    disabled={working || !isDraftEditable || !sourceId}
                    onChange={(event) => setSearchQuery(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" && searchResults.length > 0 && isDraftEditable && !scannerBusy) {
                        event.preventDefault();
                        addLine(searchResults[0]);
                      }
                    }}
                  />
                </FormField>
                <FormField label="Scanner mode">
                  <input
                    ref={scannerInputRef}
                    type="text"
                    className="form-control"
                    placeholder={scannerMode ? "Skeniraj barcode/PLU pa Enter" : "Scanner je isključen"}
                    value={scannerInput}
                    disabled={working || scannerBusy || !isDraftEditable || !scannerMode || !sourceId}
                    onChange={(event) => setScannerInput(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        void scanAndAddArticle();
                      }
                    }}
                  />
                </FormField>
              </div>
              <label className="validation-list__item">
                <input
                  type="checkbox"
                  checked={scannerMode}
                  disabled={working || !isDraftEditable}
                  onChange={(event) => setScannerMode(event.target.checked)}
                />
                Scanner mode uključen
              </label>
              {searchLoading ? <div className="form-note">Pretraga u toku...</div> : null}
              {searchResults.length > 0 ? (
                <div className="form-grid">
                  {searchResults.map((item) => (
                    <button
                      key={item.id}
                      type="button"
                      className="btn btn--secondary btn--full"
                      disabled={!isDraftEditable || Number(item.kolicina ?? 0) <= 0}
                      onClick={() => addLine(item)}
                    >
                      {item.naziv} | Dostupno: {item.kolicina ?? 0}
                    </button>
                  ))}
                </div>
              ) : null}
            </FormSection>

            <FormSection title="Stavke transfera" description="Dostupno je samo za prikaz; količina je izmenjiva." complete={draftReady} warning={Object.keys(lineErrors).length > 0}>
              <LineItemsEditor
                title={`Stavke (${lines.length})`}
                rows={lineRows}
                grid="transfer"
                renderRow={(row) => (
                  <>
                    <ReadonlyField label="Artikal" value={`${row.data.skuName} (${row.data.skuCode ?? `SKU #${row.data.skuId}`})`} />
                    <ReadonlyField label="Dostupno" value={row.data.available} />
                    <FormField label="Količina" required>
                      <input
                        className="form-control form-control--number"
                        type="number"
                        min={row.data.available > 0 ? 1 : 0}
                        step={1}
                        disabled={working || !isDraftEditable || row.data.available <= 0}
                        value={row.data.quantity}
                        onChange={(event) => updateLineQuantity(row.data.skuId, Number(event.target.value))}
                      />
                    </FormField>
                    <button
                      type="button"
                      className="btn btn--warning"
                      disabled={working || !isDraftEditable}
                      onClick={() => removeLine(row.data.skuId)}
                    >
                      Ukloni
                    </button>
                  </>
                )}
              />
            </FormSection>

            <FormSection title="Napomena" description="Interna napomena za dokument transfera." complete>
              <FormField label="Napomena">
                <textarea
                  className="form-control"
                  rows={2}
                  value={notes}
                  disabled={working || !isDraftEditable}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </FormField>
            </FormSection>
          </>
        )}
        aside={(
          <>
            <SummaryPanel
              title="Status transfera"
              actions={(
                <>
                  {primaryDisabledReason ? <p className="form-helper">{primaryDisabledReason}</p> : null}
                  <button type="button" className="btn btn--secondary btn--full" onClick={() => void saveDraft()} disabled={working || !isDraftEditable}>
                    Sačuvaj draft
                  </button>
                  <button type="button" className="btn btn--primary btn--full" onClick={() => void runStateAction(confirmTransfer, "Transfer potvrđen")} disabled={working || !isDraftEditable}>
                    Potvrdi transfer
                  </button>
                  <button type="button" className="btn btn--success btn--full" onClick={() => void runStateAction(completeTransfer, "Transfer završen")} disabled={working || selectedTransferStatus !== "confirmed"}>
                    Završi transfer
                  </button>
                  <button type="button" className="btn btn--warning btn--full" onClick={() => void runStateAction(cancelTransfer, "Transfer otkazan")} disabled={working || !selectedTransferId || selectedTransferStatus === "completed" || selectedTransferStatus === "cancelled"}>
                    Otkaži transfer
                  </button>
                  <button type="button" className="btn btn--secondary btn--full" onClick={resetDraft} disabled={working}>
                    Novi transfer
                  </button>
                </>
              )}
            >
              <ReadonlyField label="Dokument" value={selectedTransferId ? `#${selectedTransferId}` : "Novi draft"} />
              <ReadonlyField label="Status" value={selectedTransferStatus} />
              <ReadonlyField label="Stavke" value={lines.length} />
              <ReadonlyField label="Ukupna količina" value={totalQty} />
              <ValidationChecklist
                items={[
                  { label: "Radnje su različite", valid: setupReady },
                  { label: "Dodate su stavke", valid: lines.length > 0 },
                  { label: "Količine su validne", valid: Object.keys(lineErrors).length === 0 },
                ]}
              />
              <div className="form-note">{statusText}</div>
            </SummaryPanel>

            <FormSection title="Skoriji prenosi" description="Audit filter i lista poslednjih dokumenata." complete={transfers.length > 0}>
              <FormField label="Filter po akteru">
                <input className="form-control" value={actorFilterInput} onChange={(event) => setActorFilterInput(event.target.value)} placeholder="Akter" />
              </FormField>
              <div className="line-row__header">
                <button type="button" className="btn btn--secondary" onClick={() => setActorFilterApplied(actorFilterInput.trim())}>Primeni</button>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => {
                    setActorFilterInput("");
                    setActorFilterApplied("");
                  }}
                >
                  Reset
                </button>
              </div>
              <div className="form-grid">
                {transfers.length === 0 ? <div className="form-note">Nema transfer dokumenata.</div> : transfers.map((item) => (
                  <button key={item.id} type="button" className="btn btn--secondary btn--full" onClick={() => void loadTransferDetails(item.id)}>
                    #{item.id} | {item.status} | {getStoreName(stores, item.sourceId)} - {getStoreName(stores, item.destinationId)}
                  </button>
                ))}
              </div>
            </FormSection>
          </>
        )}
      />

      <StickyActionBar
        primaryLabel="Potvrdi transfer"
        disabled={working || !isDraftEditable || !draftReady}
        disabledReason={primaryDisabledReason}
        onPrimary={() => void runStateAction(confirmTransfer, "Transfer potvrđen")}
      />
    </FormPageShell>
  );
}
