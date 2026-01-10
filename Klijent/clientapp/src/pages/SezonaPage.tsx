import { useEffect, useState } from "react";
import { getSezone, createSezona } from "../services/sezoneApi";
import type { Sezona } from "../types/Sezona";

// Helper funkcija za generisanje predloga sezona za prethodnu, tekucu i sledecu godinu
function generateSeasonSuggestions(): string[] {
  const currentYear = new Date().getFullYear();
  const suggestions: string[] = [];

  for (let yearOffset = -1; yearOffset <= 1; yearOffset++) {
    const year = currentYear + yearOffset;
    suggestions.push(`Proleće/Leto ${year}`);
    suggestions.push(`Jesen/Zima ${year}/${year + 1}`);
  }

  return suggestions;
}

export default function SezonaPage() {
  const [sezone, setSezone] = useState<Sezona[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [naziv, setNaziv] = useState("");
  const [customNaziv, setCustomNaziv] = useState(""); // Za custom unos
  const [useCustomNaziv, setUseCustomNaziv] = useState(false);
  const [datumOd, setDatumOd] = useState("");
  const [datumDo, setDatumDo] = useState("");

  const [isSaving, setIsSaving] = useState(false);
  const [success, setSuccess] = useState<string | null>(null);

  const seasonSuggestions = generateSeasonSuggestions();

  const loadSezone = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getSezone();
      setSezone(data);
    } catch (e: any) {
      setError(e?.message ?? "Greška pri učitavanju sezona");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSezone();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const finalNaziv = useCustomNaziv ? customNaziv.trim() : naziv;

    if (!finalNaziv) {
      setError("Naziv je obavezan.");
      return;
    }

    if (!datumOd || !datumDo) {
      setError("Oba datuma su obavezna.");
      return;
    }

    if (new Date(datumOd) >= new Date(datumDo)) {
      setError("DatumOd mora biti pre DatumDo.");
      return;
    }

    setIsSaving(true);
    setError(null);
    setSuccess(null);

    try {
      await createSezona(finalNaziv, datumOd, datumDo);
      setSuccess("Sezona uspešno kreirana!");
      setNaziv("");
      setCustomNaziv("");
      setUseCustomNaziv(false);
      setDatumOd("");
      setDatumDo("");
      await loadSezone();
    } catch (e: any) {
      setError(e?.message ?? "Greška pri kreiranju sezone");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="card" style={{ maxWidth: 900 }}>
      <h2 className="text-2xl font-semibold mb-6">📅 Sezone</h2>

      {error && <p className="error-msg">{error}</p>}
      {success && <p style={{ marginTop: "0.75rem", color: "#059669", fontWeight: 600 }}>{success}</p>}

      <form onSubmit={handleSubmit} style={{ marginBottom: "2rem" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: "1rem", marginBottom: "1rem" }}>
          <div>
            <label className="field-label">Naziv sezone</label>
            
            <div style={{ marginBottom: "0.5rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.9rem" }}>
                <input
                  type="radio"
                  checked={!useCustomNaziv}
                  onChange={() => setUseCustomNaziv(false)}
                />
                Izaberi iz ponuđenih
              </label>
            </div>

            {!useCustomNaziv && (
              <select
                className="input-big"
                value={naziv}
                onChange={(e) => setNaziv(e.target.value)}
                required={!useCustomNaziv}
                style={{ marginBottom: "0.5rem" }}
              >
                <option value="">-- Izaberi sezonu --</option>
                {seasonSuggestions.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            )}

            <div style={{ marginBottom: "0.5rem" }}>
              <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.9rem" }}>
                <input
                  type="radio"
                  checked={useCustomNaziv}
                  onChange={() => setUseCustomNaziv(true)}
                />
                Unesi novi naziv
              </label>
            </div>

            {useCustomNaziv && (
              <input
                className="input-big"
                value={customNaziv}
                onChange={(e) => setCustomNaziv(e.target.value)}
                placeholder="Unesite vlastiti naziv sezone..."
                required={useCustomNaziv}
              />
            )}
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <div>
              <label className="field-label">Datum od</label>
              <input
                type="date"
                className="input-big"
                value={datumOd}
                onChange={(e) => setDatumOd(e.target.value)}
                required
              />
            </div>

            <div>
              <label className="field-label">Datum do</label>
              <input
                type="date"
                className="input-big"
                value={datumDo}
                onChange={(e) => setDatumDo(e.target.value)}
                required
              />
            </div>
          </div>
        </div>

        <button className="button-big" type="submit" disabled={isSaving} style={{ maxWidth: 200 }}>
          {isSaving ? "Čuvam..." : "Dodaj sezonu"}
        </button>
      </form>

      <h3 className="text-xl font-semibold mb-4">Postojeće sezone</h3>

      {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}

      {!loading && sezone.length === 0 && (
        <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Nema kreiranu nijednu sezonu.</p>
      )}

      {!loading && sezone.length > 0 && (
        <div style={{ overflowX: "auto" }}>
          <table className="table">
            <thead>
              <tr>
                <th>Naziv</th>
                <th>Datum od</th>
                <th>Datum do</th>
              </tr>
            </thead>
            <tbody>
              {sezone.map((s) => (
                <tr key={s.id}>
                  <td style={{ fontWeight: 600 }}>{s.naziv}</td>
                  <td>{new Date(s.datumOd).toLocaleDateString("sr-RS")}</td>
                  <td>{new Date(s.datumDo).toLocaleDateString("sr-RS")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
