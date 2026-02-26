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
      <div className="rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
        <h2 className="text-xl font-semibold text-[#f3f6ff]">Novi zapisnik o povracaju</h2>
        <p className="mt-1 text-sm text-[#9aabc7]">Korak {step} od 2</p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-700 bg-rose-950/30 px-4 py-3 text-sm text-rose-300">
          {error}
        </div>
      )}

      {step === 1 && (
        <div className="grid gap-4 rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Dobavljac *</label>
            <select
              className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff]"
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
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Razlog povracaja *</label>
            <textarea
              className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff]"
              value={razlogPovracaja}
              onChange={(e) => setRazlogPovracaja(e.target.value)}
              rows={3}
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Dodatni komentar</label>
            <textarea
              className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff]"
              value={komentar}
              onChange={(e) => setKomentar(e.target.value)}
              rows={2}
            />
          </div>

          <div className="flex justify-end gap-2">
            <button
              type="button"
              className="rounded-lg border border-[#3c4458] bg-[#222734] px-4 py-2 text-sm text-[#dbe6fb]"
              onClick={onCancel}
            >
              Otkazi
            </button>
            <button
              type="button"
              className="rounded-lg border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white"
              onClick={handleNext}
            >
              Dalje
            </button>
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="grid gap-4 rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Pretraga artikala</label>
            <input
              type="text"
              className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff]"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          {loadingArtikli ? (
            <p className="py-8 text-center text-sm text-[#9aabc7]">Ucitavanje artikala...</p>
          ) : (
            <>
              <div className="max-h-80 overflow-y-auto rounded-xl border border-[#2f323b]">
                <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                  <thead className="sticky top-0 bg-[#14161d] text-[#93a7c8]">
                    <tr>
                      <th className="px-3 py-2 text-left">Izaberi</th>
                      <th className="px-3 py-2 text-left">Artikal</th>
                      <th className="px-3 py-2 text-right">Nabavna cena</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                    {filteredArtikli.map((artikal) => (
                      <tr key={artikal.id} className="hover:bg-[#1f2330]">
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
                  <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Izabrani artikli ({selectedStavke.length})</h3>
                  <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
                    <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                      <thead className="bg-[#14161d] text-[#93a7c8]">
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
                      <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                        {selectedStavke.map((stavka) => (
                          <tr key={stavka.idArtikal} className="hover:bg-[#1f2330]">
                            <td className="px-3 py-2">{stavka.artikalNaziv}</td>
                            <td className="px-3 py-2">
                              <input
                                type="number"
                                value={stavka.kolicina}
                                onChange={(e) => handleUpdateStavka(stavka.idArtikal, "kolicina", Number(e.target.value))}
                                min={1}
                                className="w-20 rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-1 text-sm text-[#dbe6fb]"
                              />
                            </td>
                            <td className="px-3 py-2">
                              <input
                                type="number"
                                value={stavka.cena}
                                onChange={(e) => handleUpdateStavka(stavka.idArtikal, "cena", Number(e.target.value))}
                                step={0.01}
                                className="w-24 rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-1 text-sm text-[#dbe6fb]"
                              />
                            </td>
                            <td className="px-3 py-2">
                              <select
                                value={stavka.stanjeArtikla || ""}
                                onChange={(e) => handleUpdateStavka(stavka.idArtikal, "stanjeArtikla", e.target.value)}
                                className="rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-1 text-sm text-[#dbe6fb]"
                              >
                                <option value="">-- stanje --</option>
                                {STANJA_OPTIONS.map((stanje) => (
                                  <option key={stanje} value={stanje}>{stanje}</option>
                                ))}
                              </select>
                            </td>
                            <td className="px-3 py-2">
                              <input
                                type="text"
                                value={stavka.razlog || ""}
                                onChange={(e) => handleUpdateStavka(stavka.idArtikal, "razlog", e.target.value)}
                                className="rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-1 text-sm text-[#dbe6fb]"
                              />
                            </td>
                            <td className="px-3 py-2 text-right font-semibold text-emerald-300">{(stavka.kolicina * stavka.cena).toFixed(2)} RSD</td>
                            <td className="px-3 py-2">
                              <button
                                type="button"
                                onClick={() => handleToggleArtikal({ id: stavka.idArtikal })}
                                className="rounded-md border border-rose-700 bg-rose-900/40 px-2 py-1 text-xs font-semibold text-rose-200"
                              >
                                X
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr className="bg-[#14161d] font-semibold text-[#dbe6fb]">
                          <td colSpan={5} className="px-3 py-2 text-right">UKUPNO:</td>
                          <td className="px-3 py-2 text-right text-emerald-300">{ukupanIznos.toFixed(2)} RSD</td>
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
              className="rounded-lg border border-[#3c4458] bg-[#222734] px-4 py-2 text-sm text-[#dbe6fb]"
              onClick={handleBack}
            >
              Nazad
            </button>
            <button
              type="button"
              className="rounded-lg border border-[#2d7759] bg-[#1e5b45] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
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
