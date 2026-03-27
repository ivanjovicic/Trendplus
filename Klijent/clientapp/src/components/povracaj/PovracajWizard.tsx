import React, { useEffect, useState } from "react";
import type { Dobavljac } from "../../types/Dobavljaci";
import type { PovracajStavka } from "../../types/povracaj";
import { kreirajPovracaj } from "../../services/povracajApi";
import { getDobavljaci } from "../../services/dobavljaciApi";
import { getArtikliPaged } from "../../services/artikliApi";

type WizardStep = 1 | 2;

const STANJA_OPTIONS: readonly string[] = [
  "Osteceno",
  "Pogresna velicina",
  "Pogresan model",
  "Neprodat",
  "Dobar",
  "Ostalo",
];

interface PovracajWizardProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

const themeBorder = "var(--border-default, #2f323b)";
const themeSurface = "var(--surface-default, #14161d)";
const themeSurfaceLight = "var(--surface-light, #1a1b1f)";
const themeElevation = "var(--surface-elevated, #222734)";
const textPrimary = "var(--text-primary, #dbe6fb)";
const textSecondary = "var(--text-secondary, #9aa9c6)";
const textMuted = "var(--text-muted, #9aabc7)";
const successColor = "var(--success, #10b981)";
const borderAccent = "var(--border-hover, #4763a6)";
const primaryAccent = "var(--primary, #3760b7)";
const dangerAccent = "var(--error, #ef4444)";
const textOnPrimary = "var(--text-on-primary, #ffffff)";
const textOnError = "var(--text-on-error, #ffffff)";

export default function PovracajWizard({ onSuccess, onCancel }: PovracajWizardProps) {
  const [step, setStep] = useState<WizardStep>(1);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
  const [selectedDobavljac, setSelectedDobavljac] = useState<number | "">("");
  const [razlogPovracaja, setRazlogPovracaja] = useState("");
  const [komentar, setKomentar] = useState("");

  const [artikli, setArtikli] = useState<any[]>([]);
  const [loadingArtikli, setLoadingArtikli] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedStavke, setSelectedStavke] = useState<PovracajStavka[]>([]);

  useEffect(() => {
    const loadDobavljaci = async () => {
      try {
        const data = await getDobavljaci();
        setDobavljaci(data);
      } catch (err) {
        console.error("Failed to load dobavljaci:", err);
        setError("Greska pri ucitavanju dobavljaca");
      }
    };
    loadDobavljaci();
  }, []);

  useEffect(() => {
    if (step === 2) {
      loadArtikli();
    }
  }, [step]);

  const loadArtikli = async () => {
    setLoadingArtikli(true);
    try {
      const response = await getArtikliPaged(1, 1000);
      setArtikli(response.items);
    } catch (err) {
      console.error("Failed to load artikli:", err);
      setError("Greska pri ucitavanju artikala");
    } finally {
      setLoadingArtikli(false);
    }
  };

  const filteredArtikli = artikli.filter((a) =>
    a.naziv.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleNext = () => {
    if (step === 1) {
      if (!selectedDobavljac) {
        setError("Morate izabrati dobavljaca");
        return;
      }
      if (!razlogPovracaja.trim()) {
        setError("Morate uneti razlog povracaja");
        return;
      }
      setError(null);
      setStep(2);
    }
  };

  const handleBack = () => {
    if (step === 2) {
      setStep(1);
    }
  };

  const handleToggleArtikal = (artikal: any) => {
    const existing = selectedStavke.find((s) => s.idArtikal === artikal.id);
    if (existing) {
      setSelectedStavke((prev) => prev.filter((s) => s.idArtikal !== artikal.id));
    } else {
      setSelectedStavke((prev) => [
        ...prev,
        {
          idArtikal: artikal.id,
          artikalNaziv: artikal.naziv,
          kolicina: 1,
          cena: artikal.nabavnaCena || 0,
          razlog: "",
          stanjeArtikla: "",
        },
      ]);
    }
  };

  const handleUpdateStavka = (idArtikal: number, field: keyof PovracajStavka, value: any) => {
    setSelectedStavke((prev) =>
      prev.map((s) =>
        s.idArtikal === idArtikal ? { ...s, [field]: value } : s
      )
    );
  };

  const handleSubmit = async () => {
    if (selectedStavke.length === 0) {
      setError("Morate izabrati bar jedan artikal");
      return;
    }

    setSaving(true);
    setError(null);

    try {
      await kreirajPovracaj({
        idDobavljac: Number(selectedDobavljac),
        razlogPovracaja,
        komentar,
        stavke: selectedStavke.map((s) => ({
          idArtikal: s.idArtikal,
          kolicina: s.kolicina,
          cena: s.cena,
          razlog: s.razlog,
          stanjeArtikla: s.stanjeArtikla,
        })),
      });

      if (onSuccess) onSuccess();
    } catch (err: any) {
      setError(err?.message ?? "Greska pri kreiranju povracaja");
    } finally {
      setSaving(false);
    }
  };

  const ukupanIznos = selectedStavke.reduce((sum, s) => sum + s.kolicina * s.cena, 0);

  return (
    <div className="space-y-4">
      <div
        className="rounded-xl border p-4"
        style={{ borderColor: themeBorder, backgroundColor: themeSurface }}
      >
        <h2 className="text-xl font-semibold" style={{ color: textPrimary }}>
          Novi zapisnik o povracaju
        </h2>
        <p className="mt-1 text-sm" style={{ color: textMuted }}>
          Korak {step} od 2
        </p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-700 bg-rose-950/30 px-4 py-3 text-sm text-rose-300">
          {error}
        </div>
      )}

      {step === 1 && (
        <div
          className="grid gap-4 rounded-xl border p-4"
          style={{ borderColor: themeBorder, backgroundColor: themeSurface }}
        >
          <div>
            <label
              className="mb-1 block text-xs uppercase tracking-wide"
              style={{ color: textSecondary }}
            >
              Dobavljac *
            </label>
            <select
              className="w-full rounded-xl border bg-transparent px-3 py-2 text-sm text-[var(--text-primary)]"
              style={{ borderColor: themeBorder, backgroundColor: themeSurfaceLight }}
              value={selectedDobavljac}
              onChange={(e) => setSelectedDobavljac(e.target.value ? Number(e.target.value) : "")}
              required
            >
              <option value="">-- Izaberite dobavljaca --</option>
              {dobavljaci.map((d) => (
                <option key={d.id} value={d.id}>{d.naziv}</option>
              ))}
            </select>
          </div>

          <div>
            <label
              className="mb-1 block text-xs uppercase tracking-wide"
              style={{ color: textSecondary }}
            >
              Razlog povracaja *
            </label>
            <textarea
              className="w-full rounded-xl border bg-transparent px-3 py-2 text-sm text-[var(--text-primary)]"
              style={{ borderColor: themeBorder, backgroundColor: themeSurfaceLight }}
              value={razlogPovracaja}
              onChange={(e) => setRazlogPovracaja(e.target.value)}
              rows={3}
              required
            />
          </div>

          <div>
            <label
              className="mb-1 block text-xs uppercase tracking-wide"
              style={{ color: textSecondary }}
            >
              Dodatni komentar
            </label>
            <textarea
              className="w-full rounded-xl border bg-transparent px-3 py-2 text-sm text-[var(--text-primary)]"
              style={{ borderColor: themeBorder, backgroundColor: themeSurfaceLight }}
              value={komentar}
              onChange={(e) => setKomentar(e.target.value)}
              rows={2}
            />
          </div>

          <div className="flex justify-end gap-2">
            <button
              type="button"
              className="rounded-lg border px-4 py-2 text-sm"
              style={{
                borderColor: borderAccent,
                backgroundColor: themeElevation,
                color: textPrimary,
              }}
              onClick={onCancel}
            >
              Otkazi
            </button>
              <button
                type="button"
                className="rounded-lg border px-4 py-2 text-sm font-semibold"
                style={{
                  borderColor: primaryAccent,
                  backgroundColor: primaryAccent,
                  color: textOnPrimary,
                }}
                onClick={handleNext}
              >
              Dalje
            </button>
          </div>
        </div>
      )}

      {step === 2 && (
        <div
          className="grid gap-4 rounded-xl border p-4"
          style={{ borderColor: themeBorder, backgroundColor: themeSurface }}
        >
          <div>
            <label
              className="mb-1 block text-xs uppercase tracking-wide"
              style={{ color: textSecondary }}
            >
              Pretraga artikala
            </label>
            <input
              type="text"
              className="w-full rounded-xl border bg-transparent px-3 py-2 text-sm"
              style={{
                borderColor: themeBorder,
                backgroundColor: themeSurfaceLight,
                color: textPrimary,
              }}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          {loadingArtikli ? (
            <p className="py-8 text-center text-sm" style={{ color: textMuted }}>
              Ucitavanje artikala...
            </p>
          ) : (
            <>
              <div
                className="max-h-80 overflow-y-auto rounded-xl border"
                style={{ borderColor: themeBorder }}
              >
                <table
                  className="min-w-full text-sm"
                  style={{ borderColor: themeBorder, color: textPrimary }}
                >
                  <thead
                    className="sticky top-0"
                    style={{ backgroundColor: themeSurface, color: textSecondary }}
                  >
                    <tr>
                      <th className="px-3 py-2 text-left">Izaberi</th>
                      <th className="px-3 py-2 text-left">Artikal</th>
                      <th className="px-3 py-2 text-right">Nabavna cena</th>
                    </tr>
                  </thead>
                  <tbody
                    className="divide-y"
                    style={{ backgroundColor: themeSurfaceLight, color: textPrimary }}
                  >
                    {filteredArtikli.map((artikal) => (
                      <tr key={artikal.id} className="hover:bg-[var(--surface-elevated,#1f2330)]">
                        <td className="px-3 py-2">
                          <input
                            type="checkbox"
                            checked={selectedStavke.some((s) => s.idArtikal === artikal.id)}
                            onChange={() => handleToggleArtikal(artikal)}
                          />
                        </td>
                        <td className="px-3 py-2">{artikal.naziv}</td>
                        <td className="px-3 py-2 text-right">{(artikal.nabavnaCena || 0).toFixed(2)} RSD</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {selectedStavke.length > 0 && (
                <div>
              <h3
                className="mb-2 text-sm font-semibold uppercase tracking-wide"
                style={{ color: textSecondary }}
              >
                Izabrani artikli ({selectedStavke.length})
              </h3>
              <div
                className="overflow-x-auto rounded-xl border"
                style={{ borderColor: themeBorder }}
              >
                <table
                  className="min-w-full text-sm"
                  style={{ borderColor: themeBorder, color: textPrimary }}
                >
                  <thead
                    className="bg-[var(--surface-elevated)]"
                    style={{ backgroundColor: themeSurface, color: textSecondary }}
                  >
                    <tr>
                          <th className="px-3 py-2 text-left">Artikal</th>
                          <th className="px-3 py-2 text-left">Kolicina</th>
                          <th className="px-3 py-2 text-left">Cena</th>
                          <th className="px-3 py-2 text-left">Stanje</th>
                          <th className="px-3 py-2 text-left">Razlog</th>
                          <th className="px-3 py-2 text-right">Iznos</th>
                          <th className="px-3 py-2"></th>
                        </tr>
                      </thead>
                      <tbody
                        className="divide-y"
                        style={{ backgroundColor: themeSurfaceLight, color: textPrimary }}
                      >
                        {selectedStavke.map((stavka) => (
                          <tr key={stavka.idArtikal} className="hover:bg-[var(--surface-light)]">
                            <td className="px-3 py-2">{stavka.artikalNaziv}</td>
                            <td className="px-3 py-2">
                              <input
                                type="number"
                                value={stavka.kolicina}
                                onChange={(e) =>
                                  handleUpdateStavka(stavka.idArtikal, "kolicina", Number(e.target.value))
                                }
                                min={1}
                                className="w-20 rounded-lg border bg-transparent px-2 py-1 text-sm"
                                style={{
                                  borderColor: themeBorder,
                                  backgroundColor: themeSurfaceLight,
                                  color: textPrimary,
                                }}
                              />
                            </td>
                            <td className="px-3 py-2">
                              <input
                                type="number"
                                value={stavka.cena}
                                onChange={(e) =>
                                  handleUpdateStavka(stavka.idArtikal, "cena", Number(e.target.value))
                                }
                                step={0.01}
                                className="w-24 rounded-lg border bg-transparent px-2 py-1 text-sm"
                                style={{
                                  borderColor: themeBorder,
                                  backgroundColor: themeSurfaceLight,
                                  color: textPrimary,
                                }}
                              />
                            </td>
                            <td className="px-3 py-2">
                              <select
                                value={stavka.stanjeArtikla || ""}
                                onChange={(e) =>
                                  handleUpdateStavka(stavka.idArtikal, "stanjeArtikla", e.target.value)
                                }
                                className="rounded-lg border bg-transparent px-2 py-1 text-sm"
                                style={{
                                  borderColor: themeBorder,
                                  backgroundColor: themeSurfaceLight,
                                  color: textPrimary,
                                }}
                              >
                                <option value="">-- stanje --</option>
                                {STANJA_OPTIONS.map((stanje) => (
                                  <option key={stanje} value={stanje}>
                                    {stanje}
                                  </option>
                                ))}
                              </select>
                            </td>
                            <td className="px-3 py-2">
                              <input
                                type="text"
                                value={stavka.razlog || ""}
                                onChange={(e) =>
                                  handleUpdateStavka(stavka.idArtikal, "razlog", e.target.value)
                                }
                                className="rounded-lg border bg-transparent px-2 py-1 text-sm"
                                style={{
                                  borderColor: themeBorder,
                                  backgroundColor: themeSurfaceLight,
                                  color: textPrimary,
                                }}
                              />
                            </td>
                            <td className="px-3 py-2 text-right font-semibold" style={{ color: successColor }}>
                              {(stavka.kolicina * stavka.cena).toFixed(2)} RSD
                            </td>
                            <td className="px-3 py-2">
                              <button
                                type="button"
                                onClick={() => handleToggleArtikal({ id: stavka.idArtikal })}
                                className="rounded-md border px-2 py-1 text-xs font-semibold"
                                style={{
                                  borderColor: dangerAccent,
                                  backgroundColor: "var(--error, #ef4444)",
                                  color: textOnError,
                                  opacity: 0.9,
                                }}
                              >
                                X
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                      <tr
                        className="font-semibold"
                        style={{ backgroundColor: themeSurface, color: textPrimary }}
                      >
                        <td colSpan={5} className="px-3 py-2 text-right">UKUPNO:</td>
                        <td
                          className="px-3 py-2 text-right"
                          style={{ color: successColor }}
                        >
                          {ukupanIznos.toFixed(2)} RSD
                        </td>
                          <td></td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                </div>
              )}
            </>
          )}

          <div className="flex justify-between gap-2">
            <button
              type="button"
              className="rounded-lg border px-4 py-2 text-sm"
              style={{
                borderColor: borderAccent,
                backgroundColor: themeElevation,
                color: textPrimary,
              }}
              onClick={handleBack}
            >
              Nazad
            </button>
              <button
                type="button"
                className="rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50"
                style={{
                  borderColor: primaryAccent,
                  backgroundColor: primaryAccent,
                  color: textOnPrimary,
                }}
                onClick={handleSubmit}
                disabled={saving || selectedStavke.length === 0}
              >
              {saving ? "Cuvam..." : "Kreiraj povracaj"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

