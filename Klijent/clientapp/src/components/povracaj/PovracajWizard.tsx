import { useCallback, useEffect, useMemo, useState } from "react";
import type { Dobavljac } from "../../types/Dobavljaci";
import type { PovracajStavka } from "../../types/povracaj";
import { kreirajPovracaj } from "../../services/povracajApi";
import { getDobavljaci } from "../../services/dobavljaciApi";
import { getArtikliPaged } from "../../services/artikliApi";
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

type WizardStep = 1 | 2;
type ArticleLookup = { id: number; naziv: string; nabavnaCena?: number | null; kolicina?: number | null };

const STANJA_OPTIONS: readonly string[] = [
  "Oštećeno",
  "Pogrešna veličina",
  "Pogrešan model",
  "Neprodat",
  "Dobar",
  "Ostalo",
];

interface PovracajWizardProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

function makeReturnLine(article: ArticleLookup): LineItem<PovracajStavka> {
  return {
    id: article.id,
    title: article.naziv,
    status: "error",
    data: {
      idArtikal: article.id,
      artikalNaziv: article.naziv,
      kolicina: 1,
      cena: Number(article.nabavnaCena ?? 0),
      razlog: "",
      stanjeArtikla: "",
    },
    error: "Izaberite stanje artikla.",
  };
}

function validateLine(row: LineItem<PovracajStavka>): string | null {
  if (row.data.kolicina <= 0) return "Količina mora biti veća od 0.";
  if (row.data.cena < 0) return "Cena ne može biti negativna.";
  if (!row.data.stanjeArtikla) return "Izaberite stanje artikla.";
  return null;
}

export default function PovracajWizard({ onSuccess, onCancel }: PovracajWizardProps) {
  const [step, setStep] = useState<WizardStep>(1);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
  const [selectedDobavljac, setSelectedDobavljac] = useState<number | "">("");
  const [razlogPovracaja, setRazlogPovracaja] = useState("");
  const [komentar, setKomentar] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [searchResults, setSearchResults] = useState<ArticleLookup[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [rows, dispatchRows] = useLineItems<PovracajStavka>([]);

  useEffect(() => {
    let aborted = false;
    getDobavljaci()
      .then((data) => {
        if (!aborted) setDobavljaci(data);
      })
      .catch((reason) => {
        console.error("Failed to load dobavljaci:", reason);
        if (!aborted) setError("Greška pri učitavanju dobavljača");
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
    getArtikliPaged<ArticleLookup>(1, 25, { naziv: query })
      .then((response) => {
        if (!cancelled) setSearchResults(response.items ?? []);
      })
      .catch((reason) => {
        if (!cancelled) {
          console.error("Failed to search return articles:", reason);
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

  const selectedSupplierName = dobavljaci.find((item) => item.id === Number(selectedDobavljac))?.naziv ?? "Nije izabran";
  const setupValid = !!selectedDobavljac && razlogPovracaja.trim().length > 0;
  const invalidRows = useMemo(() => rows.filter((row) => validateLine(row) !== null), [rows]);
  const ukupanIznos = useMemo(() => rows.reduce((sum, row) => sum + row.data.kolicina * row.data.cena, 0), [rows]);
  const canSubmit = setupValid && rows.length > 0 && invalidRows.length === 0 && !saving;
  const disabledReason = !setupValid
    ? "Izaberite dobavljača i unesite razlog."
    : rows.length === 0
      ? "Dodajte bar jedan artikal."
      : invalidRows.length > 0
        ? "Ispravite stavke sa greškom."
        : undefined;

  const articleItems = useMemo<EntitySearchItem[]>(
    () =>
      searchResults.map((article) => ({
        id: article.id,
        title: article.naziv,
        meta: `Količina: ${article.kolicina ?? 0}`,
        value: `${Number(article.nabavnaCena ?? 0).toFixed(2)} RSD`,
      })),
    [searchResults]
  );

  const patchLine = useCallback((row: LineItem<PovracajStavka>, patch: Partial<PovracajStavka>) => {
    const next = { ...row.data, ...patch };
    const draft = { ...row, data: next };
    const rowError = validateLine(draft);
    dispatchRows({
      type: "patch",
      id: row.id,
      patch,
      rowPatch: { status: rowError ? "error" : "ok", error: rowError },
    });
    setError(null);
  }, [dispatchRows]);

  const addArticle = useCallback((article: ArticleLookup) => {
    if (rows.some((row) => row.data.idArtikal === article.id)) return;
    dispatchRows({ type: "add", row: makeReturnLine(article) });
    setSearchQuery("");
  }, [dispatchRows, rows]);

  const handleNext = useCallback(() => {
    if (!selectedDobavljac) {
      setError("Morate izabrati dobavljača.");
      return;
    }
    if (!razlogPovracaja.trim()) {
      setError("Morate uneti razlog povraćaja.");
      return;
    }
    setError(null);
    setStep(2);
  }, [razlogPovracaja, selectedDobavljac]);

  const handleSubmit = useCallback(async () => {
    if (!canSubmit) {
      setError(disabledReason ?? "Forma nije validna.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await kreirajPovracaj({
        idDobavljac: Number(selectedDobavljac),
        razlogPovracaja,
        komentar,
        stavke: rows.map((row) => ({
          idArtikal: row.data.idArtikal,
          kolicina: row.data.kolicina,
          cena: row.data.cena,
          razlog: row.data.razlog,
          stanjeArtikla: row.data.stanjeArtikla,
        })),
      });
      onSuccess?.();
    } catch (reason: unknown) {
      setError(reason instanceof Error ? reason.message : "Greška pri kreiranju povraćaja");
    } finally {
      setSaving(false);
    }
  }, [canSubmit, komentar, disabledReason, onSuccess, razlogPovracaja, rows, selectedDobavljac]);

  const renderRow = useCallback((row: LineItem<PovracajStavka>) => (
    <>
      <ReadonlyField label="Artikal" value={row.data.artikalNaziv ?? `#${row.data.idArtikal}`} />
      <FormField label="Količina" required>
        <input
          className="form-control form-control--number"
          type="number"
          min={1}
          value={row.data.kolicina}
          onChange={(event) => patchLine(row, { kolicina: Number(event.target.value) })}
        />
      </FormField>
      <FormField label="Cena" required>
        <input
          className="form-control form-control--number"
          type="number"
          min={0}
          step={0.01}
          value={row.data.cena}
          onChange={(event) => patchLine(row, { cena: Number(event.target.value) })}
        />
      </FormField>
      <FormField label="Stanje" required>
        <select
          className="form-control"
          value={row.data.stanjeArtikla || ""}
          onChange={(event) => patchLine(row, { stanjeArtikla: event.target.value })}
        >
          <option value="">Izaberite</option>
          {STANJA_OPTIONS.map((stanje) => <option key={stanje} value={stanje}>{stanje}</option>)}
        </select>
      </FormField>
      <FormField label="Razlog">
        <input
          className="form-control"
          value={row.data.razlog || ""}
          onChange={(event) => patchLine(row, { razlog: event.target.value })}
        />
      </FormField>
      <CalculatedField label="Iznos" value={`${(row.data.kolicina * row.data.cena).toFixed(2)} RSD`} tone={row.error ? "warning" : "success"} />
      <button type="button" className="btn btn--warning" onClick={() => dispatchRows({ type: "remove", id: row.id })}>
        Ukloni
      </button>
    </>
  ), [dispatchRows, patchLine]);

  return (
    <>
      <FormProgress
        steps={[
          { label: "Dobavljač i razlog", state: setupValid ? "complete" : "pending" },
          { label: `Artikli: ${rows.length}`, state: rows.length > 0 ? "complete" : "pending" },
          { label: invalidRows.length === 0 ? "Validacija OK" : `${invalidRows.length} problema`, state: invalidRows.length === 0 ? "complete" : "warning" },
        ]}
      />

      <FormLayout
        main={(
          <>
            <FormSection title="Osnovni podaci" description="Dobavljač i razlog su obavezni pre izbora artikala." complete={setupValid}>
              <div className="form-grid form-grid--two">
                <FormField label="Dobavljač" required>
                  <select
                    className="form-control"
                    value={selectedDobavljac}
                    onChange={(event) => {
                      setSelectedDobavljac(event.target.value ? Number(event.target.value) : "");
                      setError(null);
                    }}
                  >
                    <option value="">Izaberite dobavljača</option>
                    {dobavljaci.map((dobavljac) => <option key={dobavljac.id} value={dobavljac.id}>{dobavljac.naziv}</option>)}
                  </select>
                </FormField>
                <FormField label="Razlog povraćaja" required>
                  <input className="form-control" value={razlogPovracaja} onChange={(event) => setRazlogPovracaja(event.target.value)} />
                </FormField>
              </div>
              <FormField label="Komentar">
                <textarea className="form-control" value={komentar} onChange={(event) => setKomentar(event.target.value)} />
              </FormField>
              {step === 1 ? (
                <div className="line-row__header">
                  <button type="button" className="btn btn--secondary" onClick={onCancel}>Otkaži</button>
                  <button type="button" className="btn btn--primary" disabled={!setupValid} onClick={handleNext}>Dalje</button>
                </div>
              ) : null}
            </FormSection>

            {step === 2 ? (
              <>
                <FormSection title="Dodaj artikle" description="Pretraga je debounced i učitava samo potrebne rezultate." complete={rows.length > 0}>
                  <EntitySearchCombobox
                    label="Pretraga artikala"
                    value={searchQuery}
                    placeholder="Naziv artikla..."
                    items={articleItems}
                    loading={searchLoading}
                    helper="Unesite najmanje 2 karaktera."
                    onQueryChange={setSearchQuery}
                    onSelect={(item) => {
                      const article = searchResults.find((result) => result.id === Number(item.id));
                      if (article) addArticle(article);
                    }}
                  />
                </FormSection>

                <FormSection title="Izabrani artikli" description="Količina, cena, stanje i razlog su izmenjivi po stavci." complete={invalidRows.length === 0 && rows.length > 0} warning={invalidRows.length > 0}>
                  <LineItemsEditor title={`Stavke (${rows.length})`} rows={rows} grid="return" renderRow={renderRow} />
                </FormSection>
              </>
            ) : null}
          </>
        )}
        aside={(
          <SummaryPanel
            title="Zapisnik"
            actions={(
              <>
                {error ? <p className="form-error">{error}</p> : null}
                {disabledReason ? <p className="form-helper">{disabledReason}</p> : null}
                {step === 2 ? (
                  <button type="button" className="btn btn--primary btn--full" disabled={!canSubmit} onClick={() => void handleSubmit()}>
                    {saving ? "Čuvam..." : "Kreiraj povraćaj"}
                  </button>
                ) : null}
              </>
            )}
          >
            <ReadonlyField label="Dobavljač" value={selectedSupplierName} />
            <ReadonlyField label="Stavke" value={rows.length} />
            <CalculatedField label="Ukupno" value={`${ukupanIznos.toFixed(2)} RSD`} tone="success" />
            <ValidationChecklist
              items={[
                { label: "Dobavljač je izabran", valid: !!selectedDobavljac },
                { label: "Razlog je unet", valid: razlogPovracaja.trim().length > 0 },
                { label: "Dodate su stavke", valid: rows.length > 0 },
                { label: "Stavke su validne", valid: invalidRows.length === 0 },
              ]}
            />
          </SummaryPanel>
        )}
      />

      <StickyActionBar
        primaryLabel={step === 1 ? "Dalje" : saving ? "Čuvam..." : "Kreiraj povraćaj"}
        disabled={step === 1 ? !setupValid : !canSubmit}
        disabledReason={step === 1 ? (!setupValid ? "Izaberite dobavljača i razlog." : undefined) : disabledReason}
        onPrimary={step === 1 ? handleNext : () => void handleSubmit()}
      />
    </>
  );
}
