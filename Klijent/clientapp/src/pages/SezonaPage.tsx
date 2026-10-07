import { type FormEvent, useEffect, useState } from "react";
import { Search } from "lucide-react";
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
    const [searchQuery, setSearchQuery] = useState("");
    const [visibleCount, setVisibleCount] = useState(50);

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

    const filteredSezone = sezone.filter((season) =>
        season.naziv.toLocaleLowerCase("sr-Latn").includes(searchQuery.trim().toLocaleLowerCase("sr-Latn"))
    );
    const visibleSezone = filteredSezone.slice(0, visibleCount);

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

            <label className="relative mb-3 block">
                <Search size={16} aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
                <input
                    type="search"
                    aria-label="Pretraži sezone"
                    placeholder="Pretraži sezone"
                    value={searchQuery}
                    onChange={(event) => { setSearchQuery(event.target.value); setVisibleCount(50); }}
                    className="w-full rounded-xl border border-muted bg-surface-darker py-3 pl-10 pr-3 text-base text-contrast"
                />
            </label>

            {loading && <div className="py-10 text-center text-muted">Učitavanje...</div>}

            {!loading && filteredSezone.length === 0 && (
                <div className="py-10 text-center text-muted border border-dashed border-muted rounded-xl">
                    {searchQuery ? "Nema sezona koje odgovaraju pretrazi." : "Nema nijedne kreirane sezone."}
                </div>
            )}

            {!loading && sezone.length > 0 && (
                <div className="hidden overflow-hidden rounded-xl border border-muted bg-surface-elevated sm:block">
                    <div className="overflow-x-auto overscroll-x-contain" role="region" aria-label="Tabela sezona" tabIndex={0}>
                        <table className="min-w-[520px] divide-y divide-muted text-sm">
                            <thead className="bg-surface-darker text-muted">
                                <tr>
                                    <th className="sticky left-0 z-10 bg-surface-darker px-6 py-4 text-left font-semibold uppercase tracking-wider">Naziv</th>
                                    <th className="px-6 py-4 text-left font-semibold uppercase tracking-wider">Datum od</th>
                                    <th className="px-6 py-4 text-left font-semibold uppercase tracking-wider">Datum do</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-muted text-contrast">
                                {visibleSezone.map((season) => (
                                    <tr key={season.id} className="hover:bg-surface/50 transition-colors">
                                        <td className="sticky left-0 z-10 bg-surface-elevated px-6 py-4 font-bold">{season.naziv}</td>
                                        <td className="px-6 py-4">{new Date(season.datumOd).toLocaleDateString("sr-RS")}</td>
                                        <td className="px-6 py-4">{new Date(season.datumDo).toLocaleDateString("sr-RS")}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}
            {!loading && visibleSezone.length > 0 && (
                <div className="grid gap-2 sm:hidden" role="list" aria-label="Sezone">
                    {visibleSezone.map((season) => (
                        <article key={season.id} className="rounded-xl border border-muted bg-surface-elevated p-3" role="listitem">
                            <h4 className="break-words font-semibold text-contrast">{season.naziv}</h4>
                            <dl className="mt-2 grid grid-cols-2 gap-2 text-sm">
                                <div><dt className="text-xs text-muted">Datum od</dt><dd className="text-contrast">{new Date(season.datumOd).toLocaleDateString("sr-RS")}</dd></div>
                                <div><dt className="text-xs text-muted">Datum do</dt><dd className="text-contrast">{new Date(season.datumDo).toLocaleDateString("sr-RS")}</dd></div>
                            </dl>
                        </article>
                    ))}
                </div>
            )}
            {!loading && filteredSezone.length > visibleSezone.length && (
                <button type="button" onClick={() => setVisibleCount((count) => count + 50)} className="mt-3 min-h-11 w-full rounded-lg border border-muted bg-surface-elevated px-4 py-2 text-sm font-semibold text-contrast">
                    Prikaži još ({filteredSezone.length - visibleSezone.length})
                </button>
            )}
        </div>
    );
}
