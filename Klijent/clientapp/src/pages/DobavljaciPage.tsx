import { useState, useEffect } from "react";
import { Building2, Plus, Pencil, Trash2, X, Check, AlertCircle, Search, MoreVertical } from "lucide-react";
import { createDobavljac, getDobavljaci } from "../services/dobavljaciApi";
import { apiUrl } from "../utils/apiUrl";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
    napomena?: string;
}

export default function DobavljaciPage() {
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    // Create form
    const [naziv, setNaziv] = useState("");
    const [adresa, setAdresa] = useState("");
    const [telefon, setTelefon] = useState("");
    const [napomena, setNapomena] = useState("");
    const [isSaving, setIsSaving] = useState(false);
    const [showForm, setShowForm] = useState(false);

    // Inline edit
    const [editId, setEditId] = useState<number | null>(null);
    const [editData, setEditData] = useState<Partial<Dobavljac>>({});
    const [isEditing, setIsEditing] = useState(false);

    // Delete confirm
    const [deleteId, setDeleteId] = useState<number | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");
    const [visibleCount, setVisibleCount] = useState(50);
    const [openActionMenuId, setOpenActionMenuId] = useState<number | null>(null);

    const loadDobavljaci = async () => {
        setLoading(true);
        setError(null);
        try {
            const list = await getDobavljaci();
            setDobavljaci(list ?? []);
        } catch (e: unknown) {
            setError((e as Error)?.message ?? "Greška pri učitavanju");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { loadDobavljaci(); }, []);

    const filteredDobavljaci = dobavljaci.filter((supplier) => {
        const query = searchQuery.trim().toLocaleLowerCase("sr-Latn");
        return !query || [supplier.naziv, supplier.adresa, supplier.telefon, supplier.napomena]
            .some((value) => value?.toLocaleLowerCase("sr-Latn").includes(query));
    });
    const visibleDobavljaci = filteredDobavljaci.slice(0, visibleCount);

    const handleCreate = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!naziv.trim()) { setError("Naziv je obavezan."); return; }
        setIsSaving(true);
        setError(null);
        setSuccess(null);
        try {
            await createDobavljac(naziv.trim(), adresa.trim() || undefined, telefon.trim() || undefined, napomena.trim() || undefined);
            setSuccess(`Dobavljač "${naziv}" uspešno kreiran.`);
            setNaziv(""); setAdresa(""); setTelefon(""); setNapomena("");
            setShowForm(false);
            await loadDobavljaci();
        } catch (err) {
            setError((err as Error)?.message ?? "Greška pri kreiranju dobavljača.");
        } finally {
            setIsSaving(false);
        }
    };

    const startEdit = (d: Dobavljac) => {
        setEditId(d.id);
        setEditData({ naziv: d.naziv, adresa: d.adresa ?? "", telefon: d.telefon ?? "", napomena: d.napomena ?? "" });
    };

    const cancelEdit = () => { setEditId(null); setEditData({}); };

    const saveEdit = async () => {
        if (!editId || !editData.naziv?.trim()) { setError("Naziv je obavezan."); return; }
        setIsEditing(true);
        setError(null);
        try {
            const res = await fetch(apiUrl(`/api/dobavljaci/${editId}`), {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ Naziv: editData.naziv, Adresa: editData.adresa || null, Telefon: editData.telefon || null, Napomena: editData.napomena || null }),
            });
            if (!res.ok) throw new Error("Greška pri izmeni dobavljača.");
            setSuccess("Dobavljač uspešno izmenjen.");
            cancelEdit();
            await loadDobavljaci();
        } catch (err) {
            setError((err as Error)?.message ?? "Greška pri izmeni.");
        } finally {
            setIsEditing(false);
        }
    };

    const confirmDelete = async () => {
        if (!deleteId) return;
        setIsDeleting(true);
        setError(null);
        try {
            const res = await fetch(apiUrl(`/api/dobavljaci/${deleteId}`), { method: "DELETE" });
            if (!res.ok) throw new Error("Greška pri brisanju dobavljača.");
            setSuccess("Dobavljač obrisan.");
            setDeleteId(null);
            await loadDobavljaci();
        } catch (err) {
            setError((err as Error)?.message ?? "Greška pri brisanju.");
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <div className="space-y-5 pb-10">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                    <Building2 size={20} className="text-info" />
                    <h1 className="text-xl font-bold text-contrast">Dobavljači</h1>
                    <span className="rounded bg-surface-darker px-2 py-0.5 text-xs font-semibold text-muted">
                        {dobavljaci.length} ukupno
                    </span>
                </div>
                <button
                    onClick={() => { setShowForm(!showForm); setError(null); setSuccess(null); }}
                    className="flex items-center gap-1.5 rounded-xl bg-info px-3 py-2 text-xs font-semibold text-on-primary hover:opacity-90 transition-colors"
                >
                    <Plus size={14} />
                    {showForm ? "Otkaži" : "Novi dobavljač"}
                </button>
            </div>

            {/* Notifications */}
            {error && (
                <div className="flex items-center gap-2 rounded-xl border border-error bg-error/10 px-4 py-3 text-sm text-error">
                    <AlertCircle size={15} className="shrink-0" />
                    {error}
                    <button className="ml-auto" onClick={() => setError(null)}><X size={13} /></button>
                </div>
            )}
            {success && (
                <div className="flex items-center gap-2 rounded-xl border border-success bg-success/10 px-4 py-3 text-sm text-success">
                    <Check size={15} className="shrink-0" />
                    {success}
                    <button className="ml-auto" onClick={() => setSuccess(null)}><X size={13} /></button>
                </div>
            )}

            {/* Create form */}
            {showForm && (
                <div className="rounded-xl border border-muted bg-surface-darker p-5">
                    <div className="mb-4 text-sm font-semibold text-contrast">Novi dobavljač</div>
                    <form onSubmit={handleCreate} className="space-y-4">
                        {/* Row 1: naziv full-width */}
                        <div>
                            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted">Naziv *</label>
                            <input
                                type="text"
                                className="w-full rounded-lg border border-muted bg-surface-elevated px-3 py-2 text-sm text-contrast placeholder-muted/50 focus:border-info focus:outline-none"
                                placeholder="npr. ABC Company d.o.o."
                                value={naziv}
                                onChange={(e) => setNaziv(e.target.value)}
                                disabled={isSaving}
                                required
                            />
                        </div>
                        {/* Row 2: adresa + telefon */}
                        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                            <div>
                                <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted">Adresa</label>
                                <input
                                    type="text"
                                    className="w-full rounded-lg border border-muted bg-surface-elevated px-3 py-2 text-sm text-contrast placeholder-muted/50 focus:border-info focus:outline-none"
                                    placeholder="npr. Kneza Miloša 10, Beograd"
                                    value={adresa}
                                    onChange={(e) => setAdresa(e.target.value)}
                                    disabled={isSaving}
                                />
                            </div>
                            <div>
                                <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted">Telefon</label>
                                <input
                                    type="text"
                                    className="w-full rounded-lg border border-muted bg-surface-elevated px-3 py-2 text-sm text-contrast placeholder-muted/50 focus:border-info focus:outline-none"
                                    placeholder="+381 11 1234567"
                                    value={telefon}
                                    onChange={(e) => setTelefon(e.target.value)}
                                    disabled={isSaving}
                                />
                            </div>
                        </div>
                        {/* Row 3: napomena full-width */}
                        <div>
                            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted">Napomena</label>
                            <textarea
                                className="w-full rounded-lg border border-muted bg-surface-elevated px-3 py-2 text-sm text-contrast placeholder-muted/50 focus:border-info focus:outline-none"
                                placeholder="Dodatne napomene..."
                                value={napomena}
                                onChange={(e) => setNapomena(e.target.value)}
                                disabled={isSaving}
                                rows={2}
                            />
                        </div>
                        <div className="flex gap-2">
                            <button
                                type="submit"
                                disabled={isSaving}
                                className="flex items-center gap-1.5 rounded-lg bg-info px-4 py-2 text-sm font-semibold text-on-primary disabled:opacity-50 hover:opacity-90 transition-colors"
                            >
                                <Plus size={14} />
                                {isSaving ? "Kreiram..." : "Kreiraj dobavljača"}
                            </button>
                            <button
                                type="button"
                                onClick={() => { setShowForm(false); setError(null); }}
                                className="rounded-lg border border-muted px-4 py-2 text-sm text-muted hover:text-contrast transition-colors"
                            >
                                Otkaži
                            </button>
                        </div>
                    </form>
                </div>
            )}

            {/* Delete confirm modal */}
            {deleteId && (
                <div className="rounded-xl border border-error bg-error/10 p-4">
                    <p className="mb-3 text-sm text-error">
                        Sigurno želiš da obrišeš dobavljača <strong className="text-contrast">"{dobavljaci.find(d => d.id === deleteId)?.naziv}"</strong>?
                        Ova akcija se ne može poništiti.
                    </p>
                    <div className="flex gap-2">
                        <button
                            onClick={confirmDelete}
                            disabled={isDeleting}
                            className="flex items-center gap-1 rounded-lg bg-error px-3 py-1.5 text-xs font-semibold text-on-primary disabled:opacity-50 hover:bg-error/80"
                        >
                            <Trash2 size={12} /> {isDeleting ? "Brišem..." : "Da, obriši"}
                        </button>
                        <button
                            onClick={() => setDeleteId(null)}
                            className="rounded-lg border border-muted px-3 py-1.5 text-xs text-muted hover:text-contrast"
                        >
                            Otkaži
                        </button>
                    </div>
                </div>
            )}

            {/* Table */}
            <label className="relative block">
                <Search size={16} aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
                <input
                    type="search"
                    aria-label="Pretraži dobavljače"
                    placeholder="Pretraži dobavljače po nazivu, adresi, telefonu ili napomeni"
                    value={searchQuery}
                    onChange={(event) => { setSearchQuery(event.target.value); setVisibleCount(50); }}
                    className="w-full rounded-xl border border-muted bg-surface-darker py-3 pl-10 pr-3 text-base text-contrast placeholder:text-muted"
                />
            </label>

            <div className="rounded-xl border border-muted bg-surface-darker overflow-hidden">
                {loading ? (
                    <div className="py-12 text-center text-sm text-muted">Učitavanje...</div>
                ) : filteredDobavljaci.length === 0 ? (
                    <div className="flex flex-col items-center gap-3 py-12">
                        <Building2 size={32} className="text-muted/50" />
                        <p className="text-sm text-muted">{searchQuery ? "Nema dobavljača koji odgovaraju pretrazi." : "Nema kreiranih dobavljača."}</p>
                        {!searchQuery && (
                            <button
                                onClick={() => setShowForm(true)}
                                className="flex items-center gap-1.5 rounded-lg bg-info px-3 py-2 text-xs font-semibold text-on-primary hover:opacity-90"
                            >
                                <Plus size={13} /> Dodaj prvog dobavljača
                            </button>
                        )}
                    </div>
                ) : (
                    <div className="hidden overflow-x-auto overscroll-x-contain sm:block" role="region" aria-label="Tabela dobavljača" tabIndex={0}>
                        <table className="min-w-full divide-y divide-muted text-sm">
                            <thead className="bg-surface text-muted">
                                <tr>
                                    <th className="sticky left-0 z-10 bg-surface px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Naziv</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Adresa</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Telefon</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Napomena</th>
                                    <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide">Akcije</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-muted bg-surface-elevated text-contrast">
                                {visibleDobavljaci.map((d) =>
                                    editId === d.id ? (
                                        // ── Inline Edit Row ──
                                        <tr key={d.id} className="bg-surface-elevated">
                                            <td className="sticky left-0 z-10 bg-surface-elevated px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-info bg-surface-darker px-2 py-1 text-sm text-contrast focus:outline-none"
                                                    value={editData.naziv ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, naziv: e.target.value }))}
                                                    autoFocus
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-muted bg-surface-darker px-2 py-1 text-sm text-contrast focus:outline-none"
                                                    value={editData.adresa ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, adresa: e.target.value }))}
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-muted bg-surface-darker px-2 py-1 text-sm text-contrast focus:outline-none"
                                                    value={editData.telefon ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, telefon: e.target.value }))}
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-muted bg-surface-darker px-2 py-1 text-sm text-contrast focus:outline-none"
                                                    value={editData.napomena ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, napomena: e.target.value }))}
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <div className="flex justify-end gap-1">
                                                    <button
                                                        onClick={saveEdit}
                                                        disabled={isEditing}
                                                        title="Sačuvaj"
                                                        className="flex items-center gap-1 rounded bg-success/20 px-2 py-1 text-xs text-success disabled:opacity-50 hover:bg-success/30"
                                                    >
                                                        <Check size={12} /> {isEditing ? "..." : "Sačuvaj"}
                                                    </button>
                                                    <button
                                                        onClick={cancelEdit}
                                                        title="Otkaži"
                                                        className="rounded border border-muted px-2 py-1 text-xs text-muted hover:text-contrast"
                                                    >
                                                        <X size={12} />
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ) : (
                                        // ── Normal Row ──
                                        <tr key={d.id} className="hover:bg-surface/50 transition-colors">
                                            <td className="sticky left-0 z-10 bg-surface-elevated px-4 py-3 font-medium text-contrast">{d.naziv}</td>
                                            <td className="px-4 py-3 text-muted">{d.adresa || <span className="opacity-30">—</span>}</td>
                                            <td className="px-4 py-3 text-muted">{d.telefon || <span className="opacity-30">—</span>}</td>
                                            <td className="px-4 py-3 max-w-xs truncate text-muted">{d.napomena || <span className="opacity-30">—</span>}</td>
                                            <td className="px-4 py-3">
                                                <div className="supplier-desktop-actions flex justify-end gap-1.5">
                                                    <button
                                                        onClick={() => startEdit(d)}
                                                        title="Izmeni"
                                                        className="flex items-center gap-1 rounded border border-info bg-info/10 px-2 py-1 text-xs text-info hover:bg-info/20 transition-colors"
                                                    >
                                                        <Pencil size={11} /> Izmeni
                                                    </button>
                                                    <button
                                                        onClick={() => { setDeleteId(d.id); setError(null); }}
                                                        title="Obriši"
                                                        className="flex items-center gap-1 rounded border border-error bg-error/10 px-2 py-1 text-xs text-error hover:bg-error/20 transition-colors"
                                                    >
                                                        <Trash2 size={11} /> Obriši
                                                    </button>
                                                </div>
                                                <details className="supplier-touch-actions" open={openActionMenuId === d.id}>
                                                    <summary aria-label={`Akcije za ${d.naziv}`} title="Akcije" onClick={(event) => { event.preventDefault(); setOpenActionMenuId((current) => current === d.id ? null : d.id); }} className="flex h-11 w-11 cursor-pointer list-none items-center justify-center rounded-lg border border-muted text-contrast">
                                                        <MoreVertical size={18} aria-hidden="true" />
                                                    </summary>
                                                    <div className="supplier-touch-menu mt-1 grid min-w-40 gap-1 rounded-lg border border-muted bg-surface-elevated p-1 shadow-xl" hidden={openActionMenuId !== d.id}>
                                                        <button type="button" onClick={() => startEdit(d)} className="min-h-11 rounded px-3 text-left text-sm text-contrast hover:bg-surface">Izmeni</button>
                                                        <button type="button" onClick={() => { setDeleteId(d.id); setError(null); }} className="min-h-11 rounded px-3 text-left text-sm text-error hover:bg-surface">Obriši</button>
                                                    </div>
                                                </details>
                                            </td>
                                        </tr>
                                    )
                                )}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
            {!loading && filteredDobavljaci.length > 0 && (
                <div className="grid gap-2 sm:hidden" role="list" aria-label="Dobavljači">
                    {visibleDobavljaci.map((supplier) => (
                        <article key={supplier.id} className="rounded-xl border border-muted bg-surface-elevated p-2" role="listitem">
                            {editId === supplier.id ? (
                                <div className="grid gap-2">
                                    <label className="grid gap-1 text-xs text-muted">Naziv
                                        <input aria-label="Naziv dobavljača" className="min-h-11 rounded border border-muted bg-surface-darker px-3 text-base text-contrast" value={editData.naziv ?? ""} onChange={(event) => setEditData((prev) => ({ ...prev, naziv: event.target.value }))} />
                                    </label>
                                    <label className="grid gap-1 text-xs text-muted">Adresa
                                        <input aria-label="Adresa dobavljača" className="min-h-11 rounded border border-muted bg-surface-darker px-3 text-base text-contrast" value={editData.adresa ?? ""} onChange={(event) => setEditData((prev) => ({ ...prev, adresa: event.target.value }))} />
                                    </label>
                                    <label className="grid gap-1 text-xs text-muted">Telefon
                                        <input aria-label="Telefon dobavljača" className="min-h-11 rounded border border-muted bg-surface-darker px-3 text-base text-contrast" value={editData.telefon ?? ""} onChange={(event) => setEditData((prev) => ({ ...prev, telefon: event.target.value }))} />
                                    </label>
                                    <label className="grid gap-1 text-xs text-muted">Napomena
                                        <input aria-label="Napomena dobavljača" className="min-h-11 rounded border border-muted bg-surface-darker px-3 text-base text-contrast" value={editData.napomena ?? ""} onChange={(event) => setEditData((prev) => ({ ...prev, napomena: event.target.value }))} />
                                    </label>
                                    <div className="flex gap-2">
                                        <button type="button" onClick={() => void saveEdit()} disabled={isEditing} className="min-h-11 flex-1 rounded-lg bg-success/20 px-3 text-sm font-semibold text-success">{isEditing ? "Čuvam..." : "Sačuvaj"}</button>
                                        <button type="button" onClick={cancelEdit} className="min-h-11 rounded-lg border border-muted px-3 text-sm text-contrast">Otkaži</button>
                                    </div>
                                </div>
                            ) : (
                                <>
                                    <div className="flex min-h-11 items-center justify-between gap-2">
                                        <h2 className="min-w-0 break-words text-base font-semibold text-contrast">{supplier.naziv}</h2>
                                        <details className="supplier-touch-actions supplier-mobile-actions" open={openActionMenuId === supplier.id}>
                                            <summary aria-label={`Akcije na mobilnom prikazu za ${supplier.naziv}`} title="Akcije" onClick={(event) => { event.preventDefault(); setOpenActionMenuId((current) => current === supplier.id ? null : supplier.id); }} className="flex h-11 w-11 shrink-0 cursor-pointer list-none items-center justify-center rounded-lg border border-muted text-contrast">
                                                <MoreVertical size={18} aria-hidden="true" />
                                            </summary>
                                            <div className="supplier-touch-menu mt-1 grid min-w-40 gap-1 rounded-lg border border-muted bg-surface-elevated p-1 shadow-xl" hidden={openActionMenuId !== supplier.id}>
                                                <button type="button" onClick={() => startEdit(supplier)} className="min-h-11 rounded px-3 text-left text-sm text-contrast hover:bg-surface">Izmeni</button>
                                                <button type="button" onClick={() => { setDeleteId(supplier.id); setError(null); }} className="min-h-11 rounded px-3 text-left text-sm text-error hover:bg-surface">Obriši</button>
                                            </div>
                                        </details>
                                    </div>
                                    <details className="mt-1">
                                        <summary className="min-h-11 cursor-pointer py-3 text-sm text-muted">Kontakt i napomena</summary>
                                        <dl className="grid gap-1 pb-2 text-sm">
                                            <div><dt className="inline text-muted">Adresa: </dt><dd className="inline break-words text-contrast">{supplier.adresa || "—"}</dd></div>
                                            <div><dt className="inline text-muted">Telefon: </dt><dd className="inline text-contrast">{supplier.telefon || "—"}</dd></div>
                                            {supplier.napomena && <div><dt className="inline text-muted">Napomena: </dt><dd className="inline break-words text-contrast">{supplier.napomena}</dd></div>}
                                        </dl>
                                    </details>
                                </>
                            )}
                        </article>
                    ))}
                </div>
            )}
            {filteredDobavljaci.length > visibleDobavljaci.length && (
                <button type="button" onClick={() => setVisibleCount((count) => count + 50)} className="min-h-11 w-full rounded-lg border border-muted bg-surface-elevated px-4 py-2 text-sm font-semibold text-contrast">
                    Prikaži još ({filteredDobavljaci.length - visibleDobavljaci.length})
                </button>
            )}
            <style>{`
                .supplier-touch-actions { display: none; position: relative; }
                @media (max-width: 639px) {
                    .supplier-mobile-actions { display: block; }
                }
                @media (any-pointer: coarse) {
                    .supplier-desktop-actions { display: none; }
                    .supplier-touch-actions { display: block; }
                    .supplier-touch-actions > div { display: none; }
                    .supplier-touch-actions[open] > div:not([hidden]) { display: grid; }
                }
            `}</style>
        </div>
    );
}
