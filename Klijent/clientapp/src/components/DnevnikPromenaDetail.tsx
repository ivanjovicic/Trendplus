import React from "react";
import { ArrowRight, Copy, RefreshCw } from "lucide-react";
import { getDnevnikPromenaById } from "../services/dnevnikPromenaApi";
import type { DnevnikPromenaDetail as DnevnikPromenaDetailDto } from "../types/dnevnikPromena";
import { InventoryState } from "./inventory/InventoryPageShell";

function formatDate(value: string): string {
    return new Date(value).toLocaleString("sr-RS", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
    });
}

function formatCurrency(value?: number | null): string {
    return value == null ? "-" : `${value.toFixed(2)} RSD`;
}

function DetailRow({ label, value, highlight = false }: { label: string; value: React.ReactNode; highlight?: boolean }) {
    return (
        <div className="grid gap-1 border-b border-[#2a2b32] py-3 sm:grid-cols-[140px_1fr] sm:gap-3">
            <div className="text-xs uppercase tracking-wide text-[#8ea0bd]">{label}</div>
            <div className={highlight ? "font-semibold text-emerald-300" : "text-[#e7eeff]"}>{value}</div>
        </div>
    );
}

function DetailSkeleton() {
    return (
        <div className="space-y-5 animate-pulse">
            {[0, 1, 2].map((section) => (
                <section key={section} className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                    <div className="mb-4 h-4 w-32 rounded bg-[#253049]" />
                    <div className="space-y-3">
                        {[0, 1, 2].map((row) => (
                            <div key={row} className="grid gap-2 sm:grid-cols-[140px_1fr]">
                                <div className="h-3 w-24 rounded bg-[#202938]" />
                                <div className="h-4 w-full rounded bg-[#2a3448]" />
                            </div>
                        ))}
                    </div>
                </section>
            ))}
        </div>
    );
}

export default function DnevnikPromenaDetail({ id }: { id: number }) {
    const [detail, setDetail] = React.useState<DnevnikPromenaDetailDto | null>(null);
    const [loading, setLoading] = React.useState(true);
    const [error, setError] = React.useState<string | null>(null);

    const loadDetail = React.useCallback(async () => {
        setLoading(true);
        setError(null);

        try {
            const data = await getDnevnikPromenaById(id);
            setDetail(data);
        } catch (err: unknown) {
            setError(err instanceof Error ? err.message : "Greska pri ucitavanju detalja.");
        } finally {
            setLoading(false);
        }
    }, [id]);

    React.useEffect(() => {
        void loadDetail();
    }, [loadDetail]);

    const copyValue = React.useCallback(async (value: string | number | null | undefined) => {
        if (value == null) return;

        try {
            await navigator.clipboard.writeText(String(value));
        } catch (err) {
            console.error("Failed to copy DnevnikPromena value", err);
        }
    }, []);

    if (loading) {
        return <DetailSkeleton />;
    }

    if (error) {
        return (
            <div className="space-y-3">
                <InventoryState message={error} tone="danger" />
                <div className="flex justify-center">
                    <button
                        type="button"
                        onClick={() => void loadDetail()}
                        className="inline-flex items-center gap-2 rounded-lg border border-[#345dad] bg-[#1d2a46] px-3 py-2 text-xs font-semibold text-[#d6e4ff] transition hover:bg-[#22335a]"
                    >
                        <RefreshCw size={14} />
                        Pokusaj ponovo
                    </button>
                </div>
            </div>
        );
    }

    if (!detail) {
        return <InventoryState message={`Promena sa ID ${id} nije pronadjena.`} tone="neutral" />;
    }

    const priceChanged = detail.staraCena != null && detail.novaCena != null && detail.staraCena !== detail.novaCena;
    const priceDelta = detail.staraCena != null && detail.novaCena != null
        ? detail.novaCena - detail.staraCena
        : null;

    return (
        <div className="space-y-5 text-sm">
            <section className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                        <div className="text-xs uppercase tracking-wide text-[#8ea0bd]">Tip promene</div>
                        <div className="mt-2 text-lg font-semibold text-white">{detail.tipPromene}</div>
                    </div>
                    <div className="flex items-center gap-2">
                        <button
                            type="button"
                            onClick={() => void copyValue(detail.id)}
                            className="inline-flex items-center gap-1 rounded-lg border border-[#3c4458] bg-[#222734] px-3 py-2 text-xs text-[#dbe6fb] hover:bg-[#2d3347]"
                        >
                            <Copy size={13} />
                            Kopiraj ID
                        </button>
                    </div>
                </div>
            </section>

            <section className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Header</h3>
                <DetailRow label="Datum" value={formatDate(detail.datum)} />
                <DetailRow label="Artikal ID" value={detail.artikalId ?? "-"} />
                <DetailRow label="Naziv artikla" value={detail.nazivArtikla ?? "-"} />
            </section>

            <section className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Promena</h3>
                {priceChanged && (
                    <div className="mb-4 rounded-xl border border-emerald-800/70 bg-gradient-to-r from-[#13251f] via-[#173128] to-[#11211c] p-4">
                        <div className="mb-2 text-xs uppercase tracking-wide text-emerald-300">Promena cene</div>
                        <div className="flex flex-wrap items-center gap-3 text-base">
                            <span className="rounded-lg border border-[#455468] bg-[#1a2230] px-3 py-2 text-[#c6d2e7]">
                                {formatCurrency(detail.staraCena)}
                            </span>
                            <ArrowRight size={16} className="text-emerald-300" />
                            <span className="rounded-lg border border-emerald-700 bg-[#123126] px-3 py-2 font-semibold text-emerald-300">
                                {formatCurrency(detail.novaCena)}
                            </span>
                            {priceDelta != null && (
                                <span className={`rounded-lg px-3 py-2 font-semibold ${priceDelta >= 0 ? "bg-[#3a1d1d] text-rose-300" : "bg-[#162b20] text-emerald-300"}`}>
                                    {priceDelta >= 0 ? "+" : ""}{priceDelta.toFixed(2)} RSD
                                </span>
                            )}
                        </div>
                    </div>
                )}
                <DetailRow label="Kolicina" value={detail.kolicina ?? "-"} />
                <DetailRow label="Stara cena" value={formatCurrency(detail.staraCena)} />
                <DetailRow label="Nova cena" value={formatCurrency(detail.novaCena)} highlight={priceChanged} />
                <DetailRow
                    label="Iznos"
                    value={<span className={detail.iznos >= 0 ? "text-emerald-300 font-semibold" : "text-rose-300 font-semibold"}>{formatCurrency(detail.iznos)}</span>}
                />
            </section>

            <section className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Metadata</h3>
                <DetailRow label="Broj racuna" value={detail.brojRacuna ?? "-"} />
                <DetailRow label="Korisnik" value={detail.korisnikIme ?? "-"} />
                <DetailRow label="Komentar" value={detail.komentar ?? "-"} />
                <DetailRow label="Data origin" value={detail.dataOrigin ?? "-"} />
                <DetailRow label="Source ID" value={detail.sourceId} />
            </section>
        </div>
    );
}
