import { type FormEvent, useEffect, useState } from "react";
import { createSezona, getSezone } from "../services/sezoneApi";
import type { Sezona } from "../types/Sezona";

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
    const [customNaziv, setCustomNaziv] = useState("");
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
        } catch (e) {
            const message = e instanceof Error ? e.message : "Greška pri učitavanju sezona";
            setError(message);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadSezone();
    }, []);

    const handleSubmit = async (e: FormEvent) => {
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
            setError("Datum od mora biti pre datuma do.");
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
        } catch (e) {
            const message = e instanceof Error ? e.message : "Greška pri kreiranju sezone";
            setError(message);
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="card max-w-4xl">
            <h2 className="text-2xl font-bold mb-6 text-contrast">{"\u{1F4C5}"} Sezone</h2>

            {error && (
                <div className="mb-4 rounded-lg border border-error bg-error/10 p-4 text-sm text-error">
                    {error}
                </div>
            )}

            {success && (
                <div className="mb-4 rounded-lg border border-success bg-success/10 p-4 text-sm text-success font-semibold">
                    {success}
                </div>
            )}

            <form onSubmit={handleSubmit} className="mb-12 space-y-6">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                    <div className="space-y-4">
                        <label className="field-label text-muted !mb-0">Naziv sezone</label>

                        <div className="flex flex-col gap-3 p-4 rounded-xl border border-muted bg-surface-darker">
                            <label className="flex items-center gap-3 text-sm text-contrast cursor-pointer">
                                <input
                                    type="radio"
                                    className="w-4 h-4 accent-info"
                                    checked={!useCustomNaziv}
                                    onChange={() => setUseCustomNaziv(false)}
                                />
                                Izaberi iz ponuđenih
                            </label>

                            {!useCustomNaziv && (
                                <select
                                    className="input-big w-full"
                                    value={naziv}
                                    onChange={(e) => setNaziv(e.target.value)}
                                    required={!useCustomNaziv}
                                >
                                    <option value="">-- Izaberi sezonu --</option>
                                    {seasonSuggestions.map((season) => (
                                        <option key={season} value={season}>
                                            {season}
                                        </option>
                                    ))}
                                </select>
                            )}

                            <label className="flex items-center gap-3 text-sm text-contrast cursor-pointer">
                                <input
                                    type="radio"
                                    className="w-4 h-4 accent-info"
                                    checked={useCustomNaziv}
                                    onChange={() => setUseCustomNaziv(true)}
                                />
                                Unesi novi naziv
                            </label>

                            {useCustomNaziv && (
                                <input
                                    className="input-big w-full"
                                    value={customNaziv}
                                    onChange={(e) => setCustomNaziv(e.target.value)}
                                    placeholder="Unesite vlastiti naziv sezone..."
                                    required={useCustomNaziv}
                                />
                            )}
                        </div>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 h-fit">
                        <div className="space-y-2">
                            <label className="field-label text-muted">Datum od</label>
                            <input
                                type="date"
                                className="input-big w-full"
                                value={datumOd}
                                onChange={(e) => setDatumOd(e.target.value)}
                                required
                            />
                        </div>

                        <div className="space-y-2">
                            <label className="field-label text-muted">Datum do</label>
                            <input
                                type="date"
                                className="input-big w-full"
                                value={datumDo}
                                onChange={(e) => setDatumDo(e.target.value)}
                                required
                            />
                        </div>
                    </div>
                </div>

                <button className="button-big min-w-[180px]" type="submit" disabled={isSaving}>
                    {isSaving ? "Čuvam..." : "Dodaj sezonu"}
                </button>
            </form>

            <h3 className="text-xl font-bold mb-4 text-contrast">Postojeće sezone</h3>

            {loading && <div className="py-10 text-center text-muted">Učitavanje...</div>}

            {!loading && sezone.length === 0 && (
                <div className="py-10 text-center text-muted border border-dashed border-muted rounded-xl">
                    Nema nijedne kreirane sezone.
                </div>
            )}

            {!loading && sezone.length > 0 && (
                <div className="overflow-hidden rounded-xl border border-muted bg-surface-elevated">
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-muted text-sm">
                            <thead className="bg-surface-darker text-muted">
                                <tr>
                                    <th className="px-6 py-4 text-left font-semibold uppercase tracking-wider">Naziv</th>
                                    <th className="px-6 py-4 text-left font-semibold uppercase tracking-wider">Datum od</th>
                                    <th className="px-6 py-4 text-left font-semibold uppercase tracking-wider">Datum do</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-muted text-contrast">
                                {sezone.map((season) => (
                                    <tr key={season.id} className="hover:bg-surface/50 transition-colors">
                                        <td className="px-6 py-4 font-bold">{season.naziv}</td>
                                        <td className="px-6 py-4">{new Date(season.datumOd).toLocaleDateString("sr-RS")}</td>
                                        <td className="px-6 py-4">{new Date(season.datumDo).toLocaleDateString("sr-RS")}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}
        </div>
    );
}
