import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createArtikal, getArtikliPaged } from "../services/artikliApi";
import { getSezone } from "../services/sezoneApi";
import type { Artikal } from "../types/Artikal";
import type { Sezona } from "../types/Sezona";
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
} from "./forms/FormSystem";

type ArtikalStavka = {
    id?: number;
    naziv: string;
    kolicina: number;
    nabavnaCena: number;
    prodajnaCena: number;
    tipObuceId: number | null;
    sezonaId: number | null;
    komentar: string;
    isExisting: boolean;
};

interface UnosArtikalaFormProps {
    dobavljacId: number;
    dobavljacNaziv: string;
    brojRacuna: string;
    tipoviObuce: { id: number; naziv: string }[];
}

function makeEmptyLine(id: number | string): LineItem<ArtikalStavka> {
    return {
        id,
        title: "Novi artikal",
        status: "new",
        data: {
            naziv: "",
            kolicina: 1,
            nabavnaCena: 0,
            prodajnaCena: 0,
            tipObuceId: null,
            sezonaId: null,
            komentar: "",
            isExisting: false,
        },
        error: "Unesite naziv artikla.",
    };
}

function makeExistingLine(id: number | string, artikal: Artikal): LineItem<ArtikalStavka> {
    return {
        id,
        title: artikal.naziv,
        status: "existing",
        data: {
            id: artikal.id,
            naziv: artikal.naziv,
            kolicina: 1,
            nabavnaCena: Number(artikal.nabavnaCena ?? 0),
            prodajnaCena: Number(artikal.prodajnaCena ?? 0),
            tipObuceId: null,
            sezonaId: null,
            komentar: "",
            isExisting: true,
        },
        error: null,
    };
}

function validateLine(row: LineItem<ArtikalStavka>): string | null {
    if (!row.data.naziv.trim()) return "Naziv artikla je obavezan.";
    if (row.data.kolicina <= 0) return "Količina mora biti veća od 0.";
    if (row.data.nabavnaCena < 0 || row.data.prodajnaCena < 0) return "Cena ne može biti negativna.";
    return null;
}

export default function UnosArtikalaForm({
    dobavljacId,
    dobavljacNaziv,
    brojRacuna,
    tipoviObuce,
}: UnosArtikalaFormProps) {
    const nextLineId = useRef(1);
    const [sezone, setSezone] = useState<Sezona[]>([]);
    const [rows, dispatchRows] = useLineItems<ArtikalStavka>([makeEmptyLine(0)]);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [successCount, setSuccessCount] = useState(0);
    const [searchQuery, setSearchQuery] = useState("");
    const [debouncedQuery, setDebouncedQuery] = useState("");
    const [searchResults, setSearchResults] = useState<Artikal[]>([]);
    const [searchLoading, setSearchLoading] = useState(false);

    useEffect(() => {
        let aborted = false;
        getSezone()
            .then((data) => {
                if (!aborted) setSezone(data);
            })
            .catch((reason) => {
                console.error("Failed to load sezone:", reason);
            });
        return () => {
            aborted = true;
        };
    }, []);

    useEffect(() => {
        const timer = window.setTimeout(() => setDebouncedQuery(searchQuery), 250);
        return () => window.clearTimeout(timer);
    }, [searchQuery]);

    useEffect(() => {
        const query = debouncedQuery.trim();
        if (query.length < 2) {
            setSearchResults([]);
            setSearchLoading(false);
            return;
        }

        let cancelled = false;
        setSearchLoading(true);
        getArtikliPaged<Artikal>(1, 20, { naziv: query })
            .then((response) => {
                if (!cancelled) setSearchResults(response.items ?? []);
            })
            .catch((reason) => {
                if (!cancelled) {
                    console.error("Failed to search artikli:", reason);
                    setSearchResults([]);
                }
            })
            .finally(() => {
                if (!cancelled) setSearchLoading(false);
            });

        return () => {
            cancelled = true;
        };
    }, [debouncedQuery]);

    const invalidRows = useMemo(() => rows.filter((row) => validateLine(row) !== null), [rows]);
    const totalValue = useMemo(
        () => rows.reduce((sum, row) => sum + row.data.kolicina * row.data.nabavnaCena, 0),
        [rows]
    );
    const newCount = rows.filter((row) => !row.data.isExisting).length;
    const existingCount = rows.filter((row) => row.data.isExisting).length;
    const canSubmit = rows.length > 0 && invalidRows.length === 0 && !isSubmitting;
    const disabledReason =
        rows.length === 0
            ? "Dodajte bar jedan artikal."
            : invalidRows.length > 0
                ? "Ispravite stavke sa greškom."
                : undefined;

    const searchItems = useMemo<EntitySearchItem[]>(
        () =>
            searchResults.map((artikal) => ({
                id: artikal.id,
                title: artikal.naziv,
                meta: `Količina: ${artikal.kolicina ?? 0}`,
                value: `${artikal.prodajnaCena ?? 0} RSD`,
            })),
        [searchResults]
    );

    const patchLine = useCallback((row: LineItem<ArtikalStavka>, patch: Partial<ArtikalStavka>) => {
        const next = { ...row.data, ...patch };
        const draft = { ...row, data: next };
        const rowError = validateLine(draft);
        dispatchRows({
            type: "patch",
            id: row.id,
            patch,
            rowPatch: {
                title: next.naziv || "Novi artikal",
                status: rowError ? "error" : next.isExisting ? "existing" : "new",
                error: rowError,
            },
        });
        setError(null);
    }, [dispatchRows]);

    const addNewLine = useCallback(() => {
        dispatchRows({ type: "add", row: makeEmptyLine(nextLineId.current++) });
    }, [dispatchRows]);

    const addExistingLine = useCallback((artikal: Artikal) => {
        dispatchRows({ type: "add", row: makeExistingLine(nextLineId.current++, artikal) });
        setSearchQuery("");
    }, [dispatchRows]);

    const handleSubmitAll = useCallback(async () => {
        setError(null);
        setSuccessCount(0);

        if (!canSubmit) {
            setError(disabledReason ?? "Forma nije validna.");
            return;
        }

        setIsSubmitting(true);
        let successfulCount = 0;

        try {
            for (const row of rows) {
                const stavka = row.data;
                if (stavka.isExisting && stavka.id) {
                    console.info(`Existing article selected for receive flow: ${stavka.id}`);
                } else {
                    await createArtikal({
                        Naziv: stavka.naziv,
                        ProdajnaCena: stavka.prodajnaCena,
                        NabavnaCena: stavka.nabavnaCena,
                        NabavnaCenaDin: null,
                        PrvaProdajnaCena: null,
                        Kolicina: stavka.kolicina,
                        Komentar: stavka.komentar || `Unos robe - Račun: ${brojRacuna}`,
                        tipObuceId: stavka.tipObuceId,
                        dobavljacId: dobavljacId,
                        idObjekat: null,
                        idSezona: stavka.sezonaId,
                    });
                }

                successfulCount++;
                setSuccessCount(successfulCount);
            }

            dispatchRows({ type: "reset", rows: [makeEmptyLine(0)] });
            setSearchQuery("");
            setSuccessCount(0);
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : "Greška pri unosu artikala");
        } finally {
            setIsSubmitting(false);
        }
    }, [brojRacuna, canSubmit, disabledReason, dispatchRows, dobavljacId, rows]);

    const renderRow = useCallback((row: LineItem<ArtikalStavka>) => (
        <>
            <FormField label="Naziv artikla" required>
                <input
                    className="form-control"
                    value={row.data.naziv}
                    readOnly={row.data.isExisting}
                    onChange={(event) => patchLine(row, { naziv: event.target.value, id: undefined, isExisting: false })}
                />
            </FormField>
            <FormField label="Tip obuće">
                <select
                    className="form-control"
                    value={row.data.tipObuceId ?? ""}
                    disabled={row.data.isExisting}
                    onChange={(event) => patchLine(row, { tipObuceId: event.target.value ? Number(event.target.value) : null })}
                >
                    <option value="">Izaberite</option>
                    {tipoviObuce.map((tip) => <option key={tip.id} value={tip.id}>{tip.naziv}</option>)}
                </select>
            </FormField>
            <FormField label="Sezona">
                <select
                    className="form-control"
                    value={row.data.sezonaId ?? ""}
                    disabled={row.data.isExisting}
                    onChange={(event) => patchLine(row, { sezonaId: event.target.value ? Number(event.target.value) : null })}
                >
                    <option value="">Izaberite</option>
                    {sezone.map((sezona) => <option key={sezona.id} value={sezona.id}>{sezona.naziv}</option>)}
                </select>
            </FormField>
            <FormField label="Količina" required>
                <input
                    className="form-control form-control--number"
                    type="number"
                    min={1}
                    value={row.data.kolicina}
                    onChange={(event) => patchLine(row, { kolicina: Number(event.target.value) })}
                    onKeyDown={(event) => {
                        if (event.key === "Enter") addNewLine();
                    }}
                />
            </FormField>
            <FormField label="Nabavna" required>
                <input
                    className="form-control form-control--number"
                    type="number"
                    min={0}
                    step={0.01}
                    value={row.data.nabavnaCena}
                    onChange={(event) => patchLine(row, { nabavnaCena: Number(event.target.value) })}
                />
            </FormField>
            <FormField label="Prodajna" required>
                <input
                    className="form-control form-control--number"
                    type="number"
                    min={0}
                    step={0.01}
                    value={row.data.prodajnaCena}
                    onChange={(event) => patchLine(row, { prodajnaCena: Number(event.target.value) })}
                />
            </FormField>
            <FormField label="Komentar">
                <input
                    className="form-control"
                    value={row.data.komentar}
                    onChange={(event) => patchLine(row, { komentar: event.target.value })}
                />
            </FormField>
            <button
                type="button"
                className="btn btn--warning"
                disabled={rows.length === 1}
                onClick={() => dispatchRows({ type: "remove", id: row.id })}
            >
                Ukloni
            </button>
        </>
    ), [addNewLine, dispatchRows, patchLine, rows.length, sezone, tipoviObuce]);

    return (
        <>
            <FormProgress
                steps={[
                    { label: "Račun i dobavljač", state: "complete" },
                    { label: `Stavke: ${rows.length}`, state: rows.length > 0 ? "complete" : "pending" },
                    { label: invalidRows.length === 0 ? "Validacija OK" : `${invalidRows.length} problema`, state: invalidRows.length === 0 ? "complete" : "warning" },
                ]}
            />

            <FormLayout
                main={(
                    <>
                        <FormSection title="Kontekst prijema" description="Podaci su preneti iz prvog koraka unosa robe." complete>
                            <div className="form-grid form-grid--two">
                                <ReadonlyField label="Broj računa" value={brojRacuna} />
                                <ReadonlyField label="Dobavljač" value={`${dobavljacNaziv} (#${dobavljacId})`} />
                            </div>
                        </FormSection>

                        <FormSection title="Pretraga postojećih artikala" description="Dodajte postojeći artikal ili unesite novi kroz stavke." complete={false}>
                            <EntitySearchCombobox
                                label="Pretraži artikal"
                                value={searchQuery}
                                placeholder="Naziv artikla..."
                                items={searchItems}
                                loading={searchLoading}
                                helper="Pretraga se pokreće nakon najmanje 2 karaktera."
                                onQueryChange={setSearchQuery}
                                onSelect={(item) => {
                                    const artikal = searchResults.find((result) => result.id === Number(item.id));
                                    if (artikal) addExistingLine(artikal);
                                }}
                            />
                        </FormSection>

                        <FormSection title="Lista artikala" description="Novi artikli su izmenjivi; postojeći su jasno označeni." complete={invalidRows.length === 0 && rows.length > 0} warning={invalidRows.length > 0}>
                            <LineItemsEditor
                                title={`Artikli (${rows.length})`}
                                rows={rows}
                                grid="receive"
                                onAdd={addNewLine}
                                addLabel="Dodaj novi artikal"
                                renderRow={renderRow}
                            />
                        </FormSection>
                    </>
                )}
                aside={(
                    <SummaryPanel
                        title="Pregled unosa"
                        actions={(
                            <>
                                {error ? <p className="form-error">{error}</p> : null}
                                {disabledReason ? <p className="form-helper">{disabledReason}</p> : null}
                                <button type="button" className="btn btn--primary btn--full" disabled={!canSubmit} onClick={() => void handleSubmitAll()}>
                                    {isSubmitting ? `Unosim... (${successCount}/${rows.length})` : `Sačuvaj artikle (${rows.length})`}
                                </button>
                            </>
                        )}
                    >
                        <ReadonlyField label="Novi artikli" value={newCount} />
                        <ReadonlyField label="Postojeći artikli" value={existingCount} />
                        <CalculatedField label="Ukupna nabavna vrednost" value={`${totalValue.toFixed(2)} RSD`} tone="success" />
                        <ValidationChecklist
                            items={[
                                { label: "Postoji bar jedna stavka", valid: rows.length > 0 },
                                { label: "Sve stavke imaju naziv", valid: rows.every((row) => row.data.naziv.trim().length > 0) },
                                { label: "Količine i cene su validne", valid: invalidRows.length === 0 },
                            ]}
                        />
                    </SummaryPanel>
                )}
            />

            <StickyActionBar
                primaryLabel={isSubmitting ? "Čuvam..." : `Sačuvaj artikle (${rows.length})`}
                disabled={!canSubmit}
                disabledReason={disabledReason}
                onPrimary={() => void handleSubmitAll()}
            />
        </>
    );
}
