import { useState, useEffect } from "react";
import { Building2, Plus, Pencil, Trash2, X, Check, AlertCircle } from "lucide-react";
import { createDobavljac } from "../services/dobavljaciApi";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
    napomena?: string;
}

const API = import.meta.env.VITE_API_BASE_URL;

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

    const loadDobavljaci = async () => {
        setLoading(true);
        setError(null);
        try {
            const res = await fetch(`${API}/api/dobavljaci`);
            if (!res.ok) throw new Error("Ne mogu da dohvatim dobavljače");
            setDobavljaci(await res.json());
        } catch (e: unknown) {
            setError((e as Error)?.message ?? "Greška pri učitavanju");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => { loadDobavljaci(); }, []);

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
            const res = await fetch(`${API}/api/dobavljaci/${editId}`, {
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
            const res = await fetch(`${API}/api/dobavljaci/${deleteId}`, { method: "DELETE" });
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
                    <Building2 size={20} className="text-[#4F8EF7]" />
                    <h1 className="text-xl font-bold text-white">Dobavljači</h1>
                    <span className="rounded bg-[#1E2332] px-2 py-0.5 text-xs font-semibold text-[#8A95B0]">
                        {dobavljaci.length} ukupno
                    </span>
                </div>
                <button
                    onClick={() => { setShowForm(!showForm); setError(null); setSuccess(null); }}
                    className="flex items-center gap-1.5 rounded-xl bg-[#2563eb] px-3 py-2 text-xs font-semibold text-white hover:bg-[#1d4ed8] transition-colors"
                >
                    <Plus size={14} />
                    {showForm ? "Otkaži" : "Novi dobavljač"}
                </button>
            </div>

            {/* Notifications */}
            {error && (
                <div className="flex items-center gap-2 rounded-xl border border-[#991b1b] bg-[#2b0a0a] px-4 py-3 text-sm text-[#f87171]">
                    <AlertCircle size={15} className="shrink-0" />
                    {error}
                    <button className="ml-auto" onClick={() => setError(null)}><X size={13} /></button>
                </div>
            )}
            {success && (
                <div className="flex items-center gap-2 rounded-xl border border-[#166534] bg-[#0d2118] px-4 py-3 text-sm text-[#4ade80]">
                    <Check size={15} className="shrink-0" />
                    {success}
                    <button className="ml-auto" onClick={() => setSuccess(null)}><X size={13} /></button>
                </div>
            )}

            {/* Create form */}
            {showForm && (
                <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-5">
                    <div className="mb-4 text-sm font-semibold text-[#c9d3e4]">Novi dobavljač</div>
                    <form onSubmit={handleCreate} className="space-y-4">
                        {/* Row 1: naziv full-width */}
                        <div>
                            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-[#8A95B0]">Naziv *</label>
                            <input
                                type="text"
                                className="w-full rounded-lg border border-[#2A3045] bg-[#1A1F2E] px-3 py-2 text-sm text-[#c9d3e4] placeholder-[#3A4565] focus:border-[#4F8EF7] focus:outline-none"
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
                                <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-[#8A95B0]">Adresa</label>
                                <input
                                    type="text"
                                    className="w-full rounded-lg border border-[#2A3045] bg-[#1A1F2E] px-3 py-2 text-sm text-[#c9d3e4] placeholder-[#3A4565] focus:border-[#4F8EF7] focus:outline-none"
                                    placeholder="npr. Kneza Miloša 10, Beograd"
                                    value={adresa}
                                    onChange={(e) => setAdresa(e.target.value)}
                                    disabled={isSaving}
                                />
                            </div>
                            <div>
                                <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-[#8A95B0]">Telefon</label>
                                <input
                                    type="text"
                                    className="w-full rounded-lg border border-[#2A3045] bg-[#1A1F2E] px-3 py-2 text-sm text-[#c9d3e4] placeholder-[#3A4565] focus:border-[#4F8EF7] focus:outline-none"
                                    placeholder="+381 11 1234567"
                                    value={telefon}
                                    onChange={(e) => setTelefon(e.target.value)}
                                    disabled={isSaving}
                                />
                            </div>
                        </div>
                        {/* Row 3: napomena full-width */}
                        <div>
                            <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-[#8A95B0]">Napomena</label>
                            <textarea
                                className="w-full rounded-lg border border-[#2A3045] bg-[#1A1F2E] px-3 py-2 text-sm text-[#c9d3e4] placeholder-[#3A4565] focus:border-[#4F8EF7] focus:outline-none"
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
                                className="flex items-center gap-1.5 rounded-lg bg-[#2563eb] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50 hover:bg-[#1d4ed8] transition-colors"
                            >
                                <Plus size={14} />
                                {isSaving ? "Kreiram..." : "Kreiraj dobavljača"}
                            </button>
                            <button
                                type="button"
                                onClick={() => { setShowForm(false); setError(null); }}
                                className="rounded-lg border border-[#2A3045] px-4 py-2 text-sm text-[#8A95B0] hover:text-[#c9d3e4] transition-colors"
                            >
                                Otkaži
                            </button>
                        </div>
                    </form>
                </div>
            )}

            {/* Delete confirm modal */}
            {deleteId && (
                <div className="rounded-xl border border-[#991b1b] bg-[#1a0a0a] p-4">
                    <p className="mb-3 text-sm text-[#f87171]">
                        Sigurno želiš da obrišeš dobavljača <strong className="text-white">"{dobavljaci.find(d => d.id === deleteId)?.naziv}"</strong>?
                        Ova akcija se ne može poništiti.
                    </p>
                    <div className="flex gap-2">
                        <button
                            onClick={confirmDelete}
                            disabled={isDeleting}
                            className="flex items-center gap-1 rounded-lg bg-[#dc2626] px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50 hover:bg-[#b91c1c]"
                        >
                            <Trash2 size={12} /> {isDeleting ? "Brišem..." : "Da, obriši"}
                        </button>
                        <button
                            onClick={() => setDeleteId(null)}
                            className="rounded-lg border border-[#2A3045] px-3 py-1.5 text-xs text-[#8A95B0] hover:text-[#c9d3e4]"
                        >
                            Otkaži
                        </button>
                    </div>
                </div>
            )}

            {/* Table */}
            <div className="rounded-xl border border-[#2A3045] bg-[#161A23] overflow-hidden">
                {loading ? (
                    <div className="py-12 text-center text-sm text-[#8A95B0]">Učitavanje...</div>
                ) : dobavljaci.length === 0 ? (
                    <div className="flex flex-col items-center gap-3 py-12">
                        <Building2 size={32} className="text-[#3A4565]" />
                        <p className="text-sm text-[#8A95B0]">Nema kreiranih dobavljača.</p>
                        <button
                            onClick={() => setShowForm(true)}
                            className="flex items-center gap-1.5 rounded-lg bg-[#2563eb] px-3 py-2 text-xs font-semibold text-white hover:bg-[#1d4ed8]"
                        >
                            <Plus size={13} /> Dodaj prvog dobavljača
                        </button>
                    </div>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-[#2A3045] text-sm">
                            <thead className="bg-[#0D0F14] text-[#8A95B0]">
                                <tr>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Naziv</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Adresa</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Telefon</th>
                                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide">Napomena</th>
                                    <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide">Akcije</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-[#1E2332] bg-[#161A23] text-[#c9d3e4]">
                                {dobavljaci.map((d) =>
                                    editId === d.id ? (
                                        // ── Inline Edit Row ──
                                        <tr key={d.id} className="bg-[#1A1F2E]">
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-[#4F8EF7] bg-[#0e1520] px-2 py-1 text-sm text-[#c9d3e4] focus:outline-none"
                                                    value={editData.naziv ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, naziv: e.target.value }))}
                                                    autoFocus
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-[#2A3045] bg-[#0e1520] px-2 py-1 text-sm text-[#c9d3e4] focus:outline-none"
                                                    value={editData.adresa ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, adresa: e.target.value }))}
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-[#2A3045] bg-[#0e1520] px-2 py-1 text-sm text-[#c9d3e4] focus:outline-none"
                                                    value={editData.telefon ?? ""}
                                                    onChange={(e) => setEditData(p => ({ ...p, telefon: e.target.value }))}
                                                />
                                            </td>
                                            <td className="px-3 py-2">
                                                <input
                                                    className="w-full rounded border border-[#2A3045] bg-[#0e1520] px-2 py-1 text-sm text-[#c9d3e4] focus:outline-none"
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
                                                        className="flex items-center gap-1 rounded bg-[#166534] px-2 py-1 text-xs text-[#4ade80] disabled:opacity-50 hover:bg-[#14532d]"
                                                    >
                                                        <Check size={12} /> {isEditing ? "..." : "Sačuvaj"}
                                                    </button>
                                                    <button
                                                        onClick={cancelEdit}
                                                        title="Otkaži"
                                                        className="rounded border border-[#2A3045] px-2 py-1 text-xs text-[#8A95B0] hover:text-[#c9d3e4]"
                                                    >
                                                        <X size={12} />
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ) : (
                                        // ── Normal Row ──
                                        <tr key={d.id} className="hover:bg-[#1A1F2E]">
                                            <td className="px-4 py-3 font-medium">{d.naziv}</td>
                                            <td className="px-4 py-3 text-[#8A95B0]">{d.adresa || <span className="text-[#3A4565]">—</span>}</td>
                                            <td className="px-4 py-3 text-[#8A95B0]">{d.telefon || <span className="text-[#3A4565]">—</span>}</td>
                                            <td className="px-4 py-3 max-w-xs truncate text-[#8A95B0]">{d.napomena || <span className="text-[#3A4565]">—</span>}</td>
                                            <td className="px-4 py-3">
                                                <div className="flex justify-end gap-1.5">
                                                    <button
                                                        onClick={() => startEdit(d)}
                                                        title="Izmeni"
                                                        className="flex items-center gap-1 rounded border border-[#2d4f95] bg-[#1e2d52] px-2 py-1 text-xs text-[#60a5fa] hover:bg-[#2d4f95] transition-colors"
                                                    >
                                                        <Pencil size={11} /> Izmeni
                                                    </button>
                                                    <button
                                                        onClick={() => { setDeleteId(d.id); setError(null); }}
                                                        title="Obriši"
                                                        className="flex items-center gap-1 rounded border border-[#7f1d1d] bg-[#2b0a0a] px-2 py-1 text-xs text-[#f87171] hover:bg-[#7f1d1d] transition-colors"
                                                    >
                                                        <Trash2 size={11} /> Obriši
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    )
                                )}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}
