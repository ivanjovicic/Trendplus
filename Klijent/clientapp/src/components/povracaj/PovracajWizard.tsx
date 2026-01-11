import React, { useEffect, useState } from "react";
import type { Dobavljac } from "../../types/Dobavljaci";
import type { PovracajStavka } from "../../types/povracaj";
import { kreirajPovracaj } from "../../services/povracajApi";
import { getDobavljaci } from "../../services/dobavljaciApi";
import { getArtikliPaged } from "../../services/artikliApi";

type WizardStep = 1 | 2;

const STANJA_OPTIONS: readonly string[] = [
  "Oštećeno",
  "Pogrešna veličina",
  "Pogrešan model",
  "Neprodat",
  "Dobar",
  "Ostalo"
];

interface PovracajWizardProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

export default function PovracajWizard({ onSuccess, onCancel }: PovracajWizardProps) {
  const [step, setStep] = useState<WizardStep>(1);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Step 1 data
  const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
  const [selectedDobavljac, setSelectedDobavljac] = useState<number | "">("");
  const [razlogPovracaja, setRazlogPovracaja] = useState("");
  const [komentar, setKomentar] = useState("");

  // Step 2 data
  const [artikli, setArtikli] = useState<any[]>([]);
  const [loadingArtikli, setLoadingArtikli] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedStavke, setSelectedStavke] = useState<PovracajStavka[]>([]);

  // Load dobavljači on mount
  useEffect(() => {
    const loadDobavljaci = async () => {
      try {
        const data = await getDobavljaci();
        setDobavljaci(data);
      } catch (err) {
        console.error("Failed to load dobavljači:", err);
        setError("Greška pri učitavanju dobavljača");
      }
    };
    loadDobavljaci();
  }, []);

  // Load artikli when step 2 is reached
  useEffect(() => {
    if (step === 2) {
      loadArtikli();
    }
  }, [step]);

  const loadArtikli = async () => {
    setLoadingArtikli(true);
    try {
      // Load all artikli for the selected supplier
      // Note: We load all artikli, then user can search/filter in the UI
      const response = await getArtikliPaged(1, 1000); // Load more items
      setArtikli(response.items);
    } catch (err) {
      console.error("Failed to load artikli:", err);
      setError("Greška pri učitavanju artikala");
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
        setError("Morate izabrati dobavljača");
        return;
      }
      if (!razlogPovracaja.trim()) {
        setError("Morate uneti razlog povraćaja");
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
          stanjeArtikla: ""
        }
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
          stanjeArtikla: s.stanjeArtikla
        }))
      });

      if (onSuccess) onSuccess();
    } catch (err: any) {
      setError(err?.message ?? "Greška pri kreiranju povraćaja");
    } finally {
      setSaving(false);
    }
  };

  const ukupanIznos = selectedStavke.reduce((sum, s) => sum + s.kolicina * s.cena, 0);

  return (
    <div className="card" style={{ maxWidth: 1200, margin: "2rem auto" }}>
      <h2 style={{ fontSize: "1.5rem", fontWeight: 600, marginBottom: "1.5rem" }}>
        ↩️ Novi Zapisnik o povraćaju - Korak {step} od 2
      </h2>

      {error && (
        <div style={{ 
          background: "#fef2f2", 
          border: "1px solid #fecaca", 
          color: "#dc2626", 
          padding: "1rem", 
          borderRadius: "8px", 
          marginBottom: "1rem" 
        }}>
          {error}
        </div>
      )}

      {/* Step 1: Osnovni podaci */}
      {step === 1 && (
        <div style={{ display: "grid", gap: "1.5rem" }}>
          <div>
            <label className="field-label">Dobavljač *</label>
            <select
              className="input-big"
              value={selectedDobavljac}
              onChange={(e) => setSelectedDobavljac(e.target.value ? Number(e.target.value) : "")}
              required
            >
              <option value="">-- Izaberite dobavljača --</option>
              {dobavljaci.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.naziv}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="field-label">Razlog povraćaja *</label>
            <textarea
              className="input-big"
              value={razlogPovracaja}
              onChange={(e) => setRazlogPovracaja(e.target.value)}
              placeholder="Unesite razlog povraćaja (npr. oštećena roba, pogrešna veličina...)"
              rows={3}
              required
            />
          </div>

          <div>
            <label className="field-label">Dodatni komentar</label>
            <textarea
              className="input-big"
              value={komentar}
              onChange={(e) => setKomentar(e.target.value)}
              placeholder="Opcioni komentar..."
              rows={2}
            />
          </div>

          <div style={{ display: "flex", gap: "1rem", justifyContent: "flex-end" }}>
            <button
              type="button"
              className="button-big button-secondary"
              onClick={onCancel}
            >
              Otkaži
            </button>
            <button
              type="button"
              className="button-big"
              onClick={handleNext}
              style={{ background: "#3b82f6" }}
            >
              Dalje →
            </button>
          </div>
        </div>
      )}

      {/* Step 2: Izbor artikala */}
      {step === 2 && (
        <div style={{ display: "grid", gap: "1.5rem" }}>
          {/* Search */}
          <div>
            <label className="field-label">Pretraga artikala</label>
            <input
              type="text"
              className="input-big"
              placeholder="Unesite naziv artikla..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          {loadingArtikli ? (
            <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje artikala...</p>
          ) : (
            <>
              {/* Artikli lista */}
              <div style={{ 
                maxHeight: "400px", 
                overflowY: "auto", 
                border: "1px solid #e5e7eb", 
                borderRadius: "8px" 
              }}>
                <table className="table">
                  <thead style={{ position: "sticky", top: 0, background: "white" }}>
                    <tr>
                      <th style={{ width: "50px" }}>Izaberi</th>
                      <th>Artikal</th>
                      <th style={{ textAlign: "right" }}>Nabavna cena</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredArtikli.map((artikal) => (
                      <tr key={artikal.id}>
                        <td>
                          <input
                            type="checkbox"
                            checked={selectedStavke.some((s) => s.idArtikal === artikal.id)}
                            onChange={() => handleToggleArtikal(artikal)}
                          />
                        </td>
                        <td>{artikal.naziv}</td>
                        <td style={{ textAlign: "right" }}>
                          {(artikal.nabavnaCena || 0).toFixed(2)} RSD
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Selected stavke */}
              {selectedStavke.length > 0 && (
                <div>
                  <h3 style={{ fontWeight: 600, marginBottom: "0.75rem" }}>
                    Izabrani artikli ({selectedStavke.length})
                  </h3>
                  <div style={{ overflowX: "auto" }}>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Artikal</th>
                          <th style={{ width: "100px" }}>Količina</th>
                          <th style={{ width: "120px" }}>Cena</th>
                          <th>Stanje</th>
                          <th>Razlog</th>
                          <th style={{ textAlign: "right" }}>Iznos</th>
                          <th style={{ width: "60px" }}></th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedStavke.map((stavka) => (
                          <tr key={stavka.idArtikal}>
                            <td>{stavka.artikalNaziv}</td>
                            <td>
                              <input
                                type="number"
                                className="input-big"
                                value={stavka.kolicina}
                                onChange={(e) =>
                                  handleUpdateStavka(
                                    stavka.idArtikal,
                                    "kolicina",
                                    Number(e.target.value)
                                  )
                                }
                                min={1}
                                style={{ marginBottom: 0, fontSize: "0.875rem" }}
                              />
                            </td>
                            <td>
                              <input
                                type="number"
                                className="input-big"
                                value={stavka.cena}
                                onChange={(e) =>
                                  handleUpdateStavka(
                                    stavka.idArtikal,
                                    "cena",
                                    Number(e.target.value)
                                  )
                                }
                                step={0.01}
                                style={{ marginBottom: 0, fontSize: "0.875rem" }}
                              />
                            </td>
                            <td>
                              <select
                                className="input-big"
                                value={stavka.stanjeArtikla || ""}
                                onChange={(e) =>
                                  handleUpdateStavka(
                                    stavka.idArtikal,
                                    "stanjeArtikla",
                                    e.target.value
                                  )
                                }
                                style={{ marginBottom: 0, fontSize: "0.875rem" }}
                              >
                                {STANJA_OPTIONS.map((stanje) => (
                                  <option key={stanje} value={stanje}>
                                    {stanje}
                                  </option>
                                ))}
                              </select>
                            </td>
                            <td>
                              <input
                                type="text"
                                className="input-big"
                                value={stavka.razlog || ""}
                                onChange={(e) =>
                                  handleUpdateStavka(
                                    stavka.idArtikal,
                                    "razlog",
                                    e.target.value
                                  )
                                }
                                placeholder="Razlog..."
                                style={{ marginBottom: 0, fontSize: "0.875rem" }}
                              />
                            </td>
                            <td style={{ textAlign: "right", fontWeight: 600 }}>
                              {(stavka.kolicina * stavka.cena).toFixed(2)} RSD
                            </td>
                            <td>
                              <button
                                type="button"
                                className="button-big button-secondary"
                                onClick={() => handleToggleArtikal({ id: stavka.idArtikal })}
                                style={{ padding: "4px 8px", fontSize: "0.75rem" }}
                              >
                                ✕
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr style={{ background: "#f9fafb", fontWeight: 600 }}>
                          <td colSpan={5} style={{ textAlign: "right" }}>
                            UKUPNO:
                          </td>
                          <td style={{ textAlign: "right", color: "#dc2626", fontSize: "1.125rem" }}>
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

          <div style={{ display: "flex", gap: "1rem", justifyContent: "space-between" }}>
            <button
              type="button"
              className="button-big button-secondary"
              onClick={handleBack}
            >
              ← Nazad
            </button>
            <button
              type="button"
              className="button-big"
              onClick={handleSubmit}
              disabled={saving || selectedStavke.length === 0}
              style={{ background: "#059669" }}
            >
              {saving ? "Čuvam..." : "Kreiraj povraćaj"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
