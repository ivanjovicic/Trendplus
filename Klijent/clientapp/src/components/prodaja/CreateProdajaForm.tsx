import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
    CalculatedField,
    EntitySearchCombobox,
    FormField,
    FormLayout,
    FormProgress,
    FormSection,
    LineItemsEditor,
    ReadonlyField,
    StickyActionBar,
    SummaryPanel,
    ValidationChecklist,
    useLineItems,
    type EntitySearchItem,
    type LineItem,
} from "../forms/FormSystem";
import { KreirajProdajuDto } from "../../types/prodaja/prodaja";
import { useToast } from "../Toast";

type ArtikalOption = { id: number; naziv: string; cena: number };
type ProdajaLine = { idArtikal: number; kolicina: number; cena: number };

interface CreateProdajaFormProps {
    artikli: ArtikalOption[];
    onSearchArtikli?: (query: string) => Promise<ArtikalOption[]>;
    onSubmit: (data: KreirajProdajuDto) => Promise<void>;
}

function safeNumber(value: unknown, fallback = 0) {
    const n = typeof value === "number" ? value : Number(value);
    return Number.isFinite(n) ? n : fallback;
}

function mergeArtikli(base: ArtikalOption[], incoming: ArtikalOption[]) {
    const map = new Map<number, ArtikalOption>();
    for (const x of base) map.set(x.id, x);
    for (const x of incoming) map.set(x.id, x);
    return Array.from(map.values()).sort((a, b) => a.naziv.localeCompare(b.naziv, "sr-Latn", { sensitivity: "base" }));
}

function normalizeRacun(value: string): string {
    return value.trim();
}

function buildNextRacunSuggestion(value: string): string | null {
    const trimmed = normalizeRacun(value);
    if (!trimmed) return null;
    const match = trimmed.match(/^(.*?)(\d+)(\D*)$/);
    if (!match) return null;
    const [, prefix, digits, suffix] = match;
    const next = (Number(digits) + 1).toString().padStart(digits.length, "0");
    return `${prefix}${next}${suffix}`;
}

function makeLine(id: number | string, artikal?: ArtikalOption): LineItem<ProdajaLine> {
    return {
        id,
        title: artikal?.naziv ?? "Nova stavka",
        status: artikal?.id ? "ok" : "error",
        data: {
            idArtikal: artikal?.id ?? 0,
            kolicina: 1,
            cena: safeNumber(artikal?.cena, 0),
        },
        error: artikal?.id ? null : "Izaberite artikal.",
    };
}

export default function CreateProdajaForm({ artikli, onSearchArtikli, onSubmit }: CreateProdajaFormProps) {
    const toast = useToast();
    const nextLineId = useRef(1);
    const [knownArtikli, setKnownArtikli] = useState<ArtikalOption[]>(artikli);
    const [brojRacuna, setBrojRacuna] = useState("");
    const [rows, dispatchRows] = useLineItems<ProdajaLine>([makeLine(0, artikli[0])]);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [searchQuery, setSearchQuery] = useState("");
    const [debouncedQuery, setDebouncedQuery] = useState("");
    const [filteredArtikli, setFilteredArtikli] = useState<ArtikalOption[]>([]);
    const [isSearching, setIsSearching] = useState(false);
    const remoteSearchSeq = useRef(0);

    useEffect(() => {
        setKnownArtikli((prev) => mergeArtikli(prev, artikli));
    }, [artikli]);

    useEffect(() => {
        if (!knownArtikli.length) return;
        dispatchRows({
            type: "reset",
            rows: rows.length > 0
                ? rows.map((row, index) => {
                    if (index !== 0 || row.data.idArtikal !== 0) return row;
                    return makeLine(row.id, knownArtikli[0]);
                })
                : [makeLine(0, knownArtikli[0])],
        });
        // keep this effect tied to catalogue bootstrap only
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [knownArtikli.length]);

    useEffect(() => {
        const timer = window.setTimeout(() => setDebouncedQuery(searchQuery), 250);
        return () => window.clearTimeout(timer);
    }, [searchQuery]);

    useEffect(() => {
        if (onSearchArtikli) return;
        const q = debouncedQuery.trim().toLowerCase();
        setIsSearching(false);
        setFilteredArtikli(q ? knownArtikli.filter((a) => a.naziv.toLowerCase().includes(q)).slice(0, 10) : []);
    }, [debouncedQuery, knownArtikli, onSearchArtikli]);

    useEffect(() => {
        if (!onSearchArtikli) return;
        const q = debouncedQuery.trim();
        if (!q) {
            setFilteredArtikli([]);
            setIsSearching(false);
            return;
        }

        const seq = ++remoteSearchSeq.current;
        let cancelled = false;
        setIsSearching(true);

        onSearchArtikli(q)
            .then((items) => {
                if (cancelled || seq !== remoteSearchSeq.current) return;
                const data = (items ?? []).slice(0, 20);
                setFilteredArtikli(data);
                setKnownArtikli((prev) => mergeArtikli(prev, data));
            })
            .catch((reason) => {
                if (cancelled || seq !== remoteSearchSeq.current) return;
                console.error("Search artikli failed:", reason);
                setFilteredArtikli([]);
            })
            .finally(() => {
                if (!cancelled && seq === remoteSearchSeq.current) setIsSearching(false);
            });

        return () => {
            cancelled = true;
        };
    }, [debouncedQuery, onSearchArtikli]);

    const itemById = useMemo(() => new Map(knownArtikli.map((item) => [item.id, item])), [knownArtikli]);

    const normalizedRacun = normalizeRacun(brojRacuna);
    const invalidRows = useMemo(
        () =>
            rows.filter((row) => (
                row.data.idArtikal <= 0 ||
                safeNumber(row.data.kolicina, 0) <= 0 ||
                safeNumber(row.data.cena, 0) < 0
            )),
        [rows]
    );
    const ukupno = useMemo(
        () => rows.reduce((sum, row) => sum + safeNumber(row.data.kolicina, 0) * safeNumber(row.data.cena, 0), 0),
        [rows]
    );
    const canSubmit = normalizedRacun.length >= 3 && rows.length > 0 && invalidRows.length === 0 && !isSubmitting;
    const disabledReason =
        normalizedRacun.length < 3
            ? "Unesite broj računa od najmanje 3 karaktera."
            : rows.length === 0
                ? "Dodajte bar jednu stavku."
                : invalidRows.length > 0
                    ? "Ispravite stavke sa greškom."
                    : undefined;

    const racunSuggestions = useMemo(() => {
        const suggestions = new Set<string>();
        const next = buildNextRacunSuggestion(brojRacuna);
        if (next) suggestions.add(next);
        if (!normalizedRacun) suggestions.add(`POS-${new Date().getFullYear()}-001`);
        return Array.from(suggestions).slice(0, 4);
    }, [brojRacuna, normalizedRacun]);

    const searchItems = useMemo<EntitySearchItem[]>(
        () =>
            filteredArtikli.map((item) => ({
                id: item.id,
                title: item.naziv,
                meta: `ID: ${item.id}`,
                value: `${item.cena} RSD`,
            })),
        [filteredArtikli]
    );

    const addEmptyLine = useCallback(() => {
        const artikal = knownArtikli[0];
        dispatchRows({ type: "add", row: makeLine(nextLineId.current++, artikal) });
    }, [dispatchRows, knownArtikli]);

    const addArtikal = useCallback((artikal: ArtikalOption) => {
        setKnownArtikli((prev) => mergeArtikli(prev, [artikal]));
        dispatchRows({ type: "add", row: makeLine(nextLineId.current++, artikal) });
        setSearchQuery("");
        setError(null);
    }, [dispatchRows]);

    const updateLine = useCallback((row: LineItem<ProdajaLine>, patch: Partial<ProdajaLine>) => {
        const next = { ...row.data, ...patch };
        const artikal = itemById.get(next.idArtikal);
        const rowError =
            next.idArtikal <= 0
                ? "Izaberite artikal."
                : safeNumber(next.kolicina, 0) <= 0
                    ? "Količina mora biti veća od 0."
                    : safeNumber(next.cena, 0) < 0
                        ? "Cena ne može biti negativna."
                        : null;

        dispatchRows({
            type: "patch",
            id: row.id,
            patch,
            rowPatch: {
                title: artikal?.naziv ?? row.title,
                status: rowError ? "error" : "ok",
                error: rowError,
            },
        });
        setError(null);
    }, [dispatchRows, itemById]);

    const handleSubmit = useCallback(async () => {
        setError(null);
        if (!canSubmit) {
            setError(disabledReason ?? "Forma nije validna.");
            return;
        }

        const payload: KreirajProdajuDto = {
            brojRacuna: normalizedRacun,
            idObjekat: 1,
            nacinPlacanja: "Gotovina",
            stavke: rows.map((row) => ({
                idArtikal: row.data.idArtikal,
                kolicina: Math.max(1, Math.trunc(safeNumber(row.data.kolicina, 1))),
                cena: safeNumber(row.data.cena, 0),
            })),
        };

        setIsSubmitting(true);
        try {
            await onSubmit(payload);
            setBrojRacuna("");
            dispatchRows({ type: "reset", rows: [makeLine(0, knownArtikli[0])] });
            toast.success("Prodaja uspešno sačuvana.");
        } catch (reason: unknown) {
            console.error(reason);
            const message = reason instanceof Error ? reason.message : "Greška pri kreiranju prodaje";
            setError(message);
            toast.error(message);
        } finally {
            setIsSubmitting(false);
        }
    }, [canSubmit, disabledReason, dispatchRows, knownArtikli, normalizedRacun, onSubmit, rows, toast]);

    useEffect(() => {
        const onWindowKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key === "Enter" && canSubmit) {
                event.preventDefault();
                void handleSubmit();
            }
        };

        window.addEventListener("keydown", onWindowKeyDown);
        return () => window.removeEventListener("keydown", onWindowKeyDown);
    }, [canSubmit, handleSubmit]);

    const renderRow = useCallback((row: LineItem<ProdajaLine>) => (
        <>
            <FormField label="Artikal" required>
                <select
                    className="form-control"
                    value={row.data.idArtikal}
                    onChange={(event) => {
                        const id = Number(event.target.value);
                        const artikal = itemById.get(id);
                        updateLine(row, { idArtikal: id, cena: artikal?.cena ?? row.data.cena });
                    }}
                >
                    <option value={0}>Izaberite artikal</option>
                    {knownArtikli.map((item) => (
                        <option key={item.id} value={item.id}>
                            {item.naziv} - {item.cena} RSD
                        </option>
                    ))}
                </select>
            </FormField>
            <FormField label="Količina" required>
                <input
                    className="form-control form-control--number"
                    type="number"
                    min={1}
                    value={row.data.kolicina}
                    onChange={(event) => updateLine(row, { kolicina: Number(event.target.value) })}
                    onKeyDown={(event) => {
                        if (event.key === "Enter") addEmptyLine();
                    }}
                />
            </FormField>
            <FormField label="Cena" required>
                <input
                    className="form-control form-control--number"
                    type="number"
                    min={0}
                    step={0.01}
                    value={row.data.cena}
                    onChange={(event) => updateLine(row, { cena: Number(event.target.value) })}
                />
            </FormField>
            <CalculatedField
                label="Iznos"
                value={`${(safeNumber(row.data.kolicina, 0) * safeNumber(row.data.cena, 0)).toFixed(2)} RSD`}
                tone={row.error ? "warning" : "success"}
            />
            <button
                type="button"
                className="btn btn--warning"
                disabled={rows.length === 1}
                onClick={() => dispatchRows({ type: "remove", id: row.id })}
            >
                Ukloni
            </button>
        </>
    ), [addEmptyLine, dispatchRows, itemById, knownArtikli, rows.length, updateLine]);

    return (
        <>
            <FormProgress
                steps={[
                    { label: "Broj računa", state: normalizedRacun.length >= 3 ? "complete" : "pending" },
                    { label: `Stavke: ${rows.length}`, state: rows.length > 0 ? "complete" : "pending" },
                    { label: invalidRows.length === 0 ? "Validacija OK" : `${invalidRows.length} problema`, state: invalidRows.length === 0 ? "complete" : "warning" },
                ]}
            />

            <FormLayout
                main={(
                    <>
                        <FormSection title="Račun" description="Identifikacija prodaje i priprema za unos stavki." complete={normalizedRacun.length >= 3}>
                            <div className="form-grid">
                                <FormField label="Broj računa" required helper="Ctrl+Enter čuva prodaju kada je forma validna.">
                                    <input
                                        className="form-control"
                                        placeholder="Npr. POS-2026-001"
                                        value={brojRacuna}
                                        onChange={(event) => {
                                            setBrojRacuna(event.target.value);
                                            setError(null);
                                        }}
                                    />
                                </FormField>
                                {racunSuggestions.length > 0 ? (
                                    <div className="line-row__header">
                                        {racunSuggestions.map((suggestion) => (
                                            <button key={suggestion} type="button" className="btn btn--secondary" onClick={() => setBrojRacuna(suggestion)}>
                                                {suggestion}
                                            </button>
                                        ))}
                                    </div>
                                ) : null}
                            </div>
                        </FormSection>

                        <FormSection title="Dodavanje artikala" description="Pretražite artikal i dodajte ga u listu stavki." complete={rows.length > 0}>
                            <EntitySearchCombobox
                                label="Pretraži artikal"
                                value={searchQuery}
                                placeholder="Naziv artikla..."
                                items={searchItems}
                                loading={isSearching}
                                onQueryChange={setSearchQuery}
                                onSelect={(item) => {
                                    const artikal = filteredArtikli.find((x) => x.id === Number(item.id));
                                    if (artikal) addArtikal(artikal);
                                }}
                            />
                        </FormSection>

                        <FormSection title="Stavke prodaje" description="Količina i cena su izmenjivi; iznos je obračunat." complete={invalidRows.length === 0 && rows.length > 0} warning={invalidRows.length > 0}>
                            <LineItemsEditor
                                title={`Stavke (${rows.length})`}
                                rows={rows}
                                grid="sale"
                                onAdd={addEmptyLine}
                                addLabel="Dodaj stavku"
                                renderRow={renderRow}
                            />
                        </FormSection>
                    </>
                )}
                aside={(
                    <SummaryPanel
                        title="Pregled prodaje"
                        actions={(
                            <>
                                {error ? <p className="form-error">{error}</p> : null}
                                {disabledReason && !canSubmit ? <p className="form-helper">{disabledReason}</p> : null}
                                <button type="button" className="btn btn--primary btn--full" disabled={!canSubmit} onClick={() => void handleSubmit()}>
                                    {isSubmitting ? "Čuvam..." : "Sačuvaj prodaju"}
                                </button>
                            </>
                        )}
                    >
                        <ReadonlyField label="Broj računa" value={normalizedRacun || "Nije unet"} />
                        <ReadonlyField label="Broj stavki" value={rows.length} />
                        <CalculatedField label="Ukupno" value={`${safeNumber(ukupno, 0).toFixed(2)} RSD`} tone="success" />
                        <ValidationChecklist
                            items={[
                                { label: "Broj računa je unet", valid: normalizedRacun.length >= 3 },
                                { label: "Postoji bar jedna stavka", valid: rows.length > 0 },
                                { label: "Sve stavke su validne", valid: invalidRows.length === 0 },
                            ]}
                        />
                    </SummaryPanel>
                )}
            />

            <StickyActionBar
                primaryLabel={isSubmitting ? "Čuvam..." : "Sačuvaj prodaju"}
                disabled={!canSubmit}
                disabledReason={disabledReason}
                onPrimary={() => void handleSubmit()}
            />
        </>
    );
}
