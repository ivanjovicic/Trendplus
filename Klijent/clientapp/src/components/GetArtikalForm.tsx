import { useState } from "react";
import { getArtikal } from "../services/artikliApi";

export default function GetArtikalForm() {
    const [id, setId] = useState("");
    const [error, setError] = useState("");
    const [artikal, setArtikal] = useState<any>(null);

    const handleSearch = async () => {
        setError("");
        setArtikal(null);

        const parsed = Number(id);
        if (!parsed || parsed <= 0) {
            setError("ID mora biti ceo broj veći od 0.");
            return;
        }

        try {
            const result = await getArtikal(parsed);
            setArtikal(result);
        } catch {
            setError("Artikal nije pronađen.");
        }
    };

    return (
        <div className="card">
            <h2 className="text-2xl font-semibold mb-6">🔍 Pronađi artikal</h2>

            <input
                className="input-big"
                placeholder="ID artikla"
                value={id}
                onChange={(e) => setId(e.target.value)}
            />

            <button className="button-big" onClick={handleSearch}>
                Pretraži
            </button>

            {error && <p className="text-red-600 mt-4">{error}</p>}

            {artikal && (
                <div className="mt-6 p-4 bg-gray-100 border rounded-xl text-xl">
                    <p><strong>ID:</strong> {artikal.id}</p>
                    <p><strong>Naziv:</strong> {artikal.naziv}</p>
                    <p><strong>Cena:</strong> {artikal.cena}</p>
                </div>
            )}
        </div>
    );
}
