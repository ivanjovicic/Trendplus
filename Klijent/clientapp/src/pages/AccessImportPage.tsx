import { useEffect, useRef, useState } from "react";
import {
    deleteAccessImportBatch,
    getAccessImportBatches,
    previewAccessImport,
    runAccessImport,
    type AccessImportBatchDto,
    type AccessImportFieldMappingPreview,
    type AccessImportPreviewResponse,
    type AccessImportRunResponse,
    type AccessImportTablePreview,
    type DeleteBatchResult,
} from "../services/accessImportApi";

function fmtDate(value: string | null): string {
    if (!value) return "-";
    return new Date(value).toLocaleString("sr-RS");
}

function getMatchedMappingsCount(table: AccessImportTablePreview): number {
    return table.fieldMappings.filter((m) => m.status.toLowerCase() === "matched").length;
}

function getMappingStatusColor(mapping: AccessImportFieldMappingPreview): string {
    return mapping.status.toLowerCase() === "matched" ? "#166534" : "#b91c1c";
}

export default function AccessImportPage() {
    const fileInputRef = useRef<HTMLInputElement | null>(null);
    const [file, setFile] = useState<File | null>(null);
    const [useRootFile, setUseRootFile] = useState(false);
    const [includeAnalytics, setIncludeAnalytics] = useState(true);
    const [overwriteExisting, setOverwriteExisting] = useState(true);
    const [preview, setPreview] = useState<AccessImportPreviewResponse | null>(null);
    const [runResult, setRunResult] = useState<AccessImportRunResponse | null>(null);
    const [batches, setBatches] = useState<AccessImportBatchDto[]>([]);
    const [loadingPreview, setLoadingPreview] = useState(false);
    const [loadingImport, setLoadingImport] = useState(false);
    const [deletingBatchId, setDeletingBatchId] = useState<number | null>(null);
    const [deleteIncludeAnalytics, setDeleteIncludeAnalytics] = useState(true);
    const [deleteResult, setDeleteResult] = useState<DeleteBatchResult | null>(null);
    const [error, setError] = useState<string | null>(null);

    const refreshBatches = async () => {
        try {
            setBatches(await getAccessImportBatches(20));
        } catch {
            // best effort
        }
    };

    useEffect(() => {
        void refreshBatches();
    }, []);

    const handlePreview = async () => {
        setError(null);
        setRunResult(null);
        setLoadingPreview(true);
        try {
            const data = await previewAccessImport(file, useRootFile);
            setPreview(data);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greska pri analizi ACCDB fajla.");
        } finally {
            setLoadingPreview(false);
        }
    };

    const handleDeleteBatch = async (batchId: number) => {
        if (!window.confirm(`Obrisati sve podatke importovane u batch-u #${batchId}? Ova akcija je nepovratna.`))
            return;
        setError(null);
        setDeleteResult(null);
        setDeletingBatchId(batchId);
        try {
            const result = await deleteAccessImportBatch(batchId, deleteIncludeAnalytics);
            setDeleteResult(result);
            await refreshBatches();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greska pri brisanju batch-a.");
        } finally {
            setDeletingBatchId(null);
        }
    };

    const handleImport = async () => {
        setError(null);
        setLoadingImport(true);
        try {
            const data = await runAccessImport(file, {
                useRootFile,
                includeAnalytics,
                overwriteExisting,
            });
            setRunResult(data);
            await refreshBatches();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greska pri importu.");
        } finally {
            setLoadingImport(false);
        }
    };

    return (
        <div style={{ maxWidth: 1100, margin: "0 auto", paddingBottom: 28 }}>
            <div
                style={{
                    borderRadius: 14,
                    padding: "18px 22px",
                    background: "linear-gradient(135deg, #0f766e 0%, #0ea5e9 100%)",
                    color: "white",
                    marginBottom: 16,
                }}
            >
                <h1 style={{ margin: 0, fontSize: 24 }}>Access Import (TRENDPLUS.accdb)</h1>
                <p style={{ margin: "8px 0 0", opacity: 0.92 }}>
                    Import podataka iz Access baze u Trendplus i Analytics, sa podrskom za globalni prikaz:
                    <strong> existing / imported / all</strong>.
                </p>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr", gap: 14 }}>
                <section style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: 16 }}>
                    <h3 style={{ marginTop: 0 }}>1) Izvor ACCDB</h3>

                    <label style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
                        <input
                            type="checkbox"
                            checked={useRootFile}
                            onChange={(e) => setUseRootFile(e.target.checked)}
                        />
                        Koristi TRENDPLUS.accdb automatski (root ili parent folder)
                    </label>

                    <input
                        ref={fileInputRef}
                        type="file"
                        accept=".accdb"
                        style={{ display: "none" }}
                        onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                    />

                    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
                        <button
                            type="button"
                            onClick={() => fileInputRef.current?.click()}
                            style={{
                                border: "1px solid #cbd5e1",
                                background: "#f8fafc",
                                borderRadius: 8,
                                padding: "7px 12px",
                                cursor: "pointer",
                                fontWeight: 600,
                            }}
                        >
                            Browse ACCDB
                        </button>
                        {file && (
                            <button
                                type="button"
                                onClick={() => setFile(null)}
                                style={{
                                    border: "1px solid #fecaca",
                                    background: "#fff1f2",
                                    color: "#b91c1c",
                                    borderRadius: 8,
                                    padding: "7px 10px",
                                    cursor: "pointer",
                                    fontWeight: 600,
                                }}
                            >
                                Clear
                            </button>
                        )}
                        <span style={{ fontSize: 13, color: "#374151" }}>
                            {file ? `Izabrano: ${file.name}` : "Nije izabran fajl"}
                        </span>
                    </div>

                    <div style={{ display: "flex", gap: 14, marginBottom: 10, flexWrap: "wrap" }}>
                        <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
                            <input
                                type="checkbox"
                                checked={includeAnalytics}
                                onChange={(e) => setIncludeAnalytics(e.target.checked)}
                            />
                            Ukljuci upis u Analytics
                        </label>
                        <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
                            <input
                                type="checkbox"
                                checked={overwriteExisting}
                                onChange={(e) => setOverwriteExisting(e.target.checked)}
                            />
                            Azuriraj postojece redove
                        </label>
                    </div>

                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                        <button
                            onClick={handlePreview}
                            disabled={loadingPreview || loadingImport}
                            style={{
                                border: "none",
                                borderRadius: 8,
                                padding: "8px 12px",
                                background: loadingPreview ? "#9ca3af" : "#2563eb",
                                color: "white",
                                fontWeight: 700,
                                cursor: loadingPreview ? "not-allowed" : "pointer",
                            }}
                        >
                            {loadingPreview ? "Analiziram..." : "Analiza seme"}
                        </button>
                        <button
                            onClick={handleImport}
                            disabled={loadingImport || loadingPreview}
                            style={{
                                border: "none",
                                borderRadius: 8,
                                padding: "8px 12px",
                                background: loadingImport ? "#9ca3af" : "#059669",
                                color: "white",
                                fontWeight: 700,
                                cursor: loadingImport ? "not-allowed" : "pointer",
                            }}
                        >
                            {loadingImport ? "Importujem..." : "Pokreni import"}
                        </button>
                    </div>

                    {error && (
                        <div style={{ marginTop: 12, background: "#fef2f2", color: "#b91c1c", border: "1px solid #fecaca", borderRadius: 8, padding: "10px 12px" }}>
                            {error}
                        </div>
                    )}
                </section>

                <section style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: 16 }}>
                    <h3 style={{ marginTop: 0 }}>2) Poslednji import</h3>
                    {!runResult && <p style={{ color: "#6b7280" }}>Nije pokrenut import u ovoj sesiji.</p>}
                    {runResult && (
                        <div style={{ fontSize: 14 }}>
                            <div><strong>Status:</strong> {runResult.status}</div>
                            <div><strong>Batch ID:</strong> {runResult.batchId}</div>
                            <div><strong>Fajl:</strong> {runResult.sourceFileName}</div>
                            <div><strong>Start:</strong> {fmtDate(runResult.startedAtUtc)}</div>
                            <div><strong>Kraj:</strong> {fmtDate(runResult.completedAtUtc)}</div>
                            <hr style={{ margin: "10px 0" }} />
                            <div>Artikli: +{runResult.artikliInserted} / update {runResult.artikliUpdated}</div>
                            <div>Dobavljaci: +{runResult.dobavljaciInserted} / update {runResult.dobavljaciUpdated}</div>
                            <div>Sezone: +{runResult.sezoneInserted} / update {runResult.sezoneUpdated}</div>
                            <div>Tipovi: +{runResult.tipoviInserted} / update {runResult.tipoviUpdated}</div>
                            <div>Prodaje: +{runResult.prodajaInserted} / update {runResult.prodajaUpdated}</div>
                            <div>Stavke prodaje: +{runResult.prodajaStavkeInserted} / update {runResult.prodajaStavkeUpdated}</div>
                            {(runResult.dnevnikInserted > 0 || runResult.dnevnikUpdated > 0) && (
                                <div>Dnevnik promena: +{runResult.dnevnikInserted} / update {runResult.dnevnikUpdated}</div>
                            )}
                            {(runResult.povracajInserted > 0 || runResult.povracajUpdated > 0) && (
                                <div>Povracaji: +{runResult.povracajInserted} / update {runResult.povracajUpdated}</div>
                            )}
                            {(runResult.povracajStavkeInserted > 0 || runResult.povracajStavkeUpdated > 0) && (
                                <div>Stavke povracaja: +{runResult.povracajStavkeInserted} / update {runResult.povracajStavkeUpdated}</div>
                            )}
                            {runResult.includeAnalytics && (
                                <>
                                    <hr style={{ margin: "10px 0" }} />
                                    <div>ProductsDim: +{runResult.productsDimInserted} / update {runResult.productsDimUpdated}</div>
                                    <div>SalesFacts: +{runResult.salesFactsInserted} / update {runResult.salesFactsUpdated}</div>
                                    <div>SalesLineFacts: +{runResult.salesLineFactsInserted}</div>
                                    <div>Stores: +{runResult.storesInserted} / update {runResult.storesUpdated}</div>
                                </>
                            )}
                        </div>
                    )}
                </section>
            </div>

            {preview && (
                <section style={{ marginTop: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: 16 }}>
                    <h3 style={{ marginTop: 0 }}>3) Rezultat analize</h3>
                    <div style={{ marginBottom: 8 }}>
                        <strong>Can import:</strong> {preview.canImport ? "DA" : "NE"}
                    </div>
                    <div style={{ display: "grid", gap: 10 }}>
                        {preview.tables.map((t) => {
                            const matched = getMatchedMappingsCount(t);
                            const total = t.fieldMappings.length;

                            return (
                                <details
                                    key={t.key}
                                    style={{
                                        border: "1px solid #e5e7eb",
                                        borderRadius: 8,
                                        padding: "8px 10px",
                                        background: "#fafafa",
                                    }}
                                >
                                    <summary style={{ cursor: "pointer", display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                                        <strong>{t.key}</strong>
                                        <span style={{ color: t.found ? "#111827" : "#9ca3af" }}>{t.tableName ?? "-"}</span>
                                        <span style={{ fontSize: 12, color: "#475569" }}>rows: {t.rowCount}</span>
                                        {total > 0 && (
                                            <span
                                                style={{
                                                    fontSize: 12,
                                                    color: matched === total ? "#166534" : "#9a3412",
                                                    background: matched === total ? "#dcfce7" : "#fff7ed",
                                                    border: "1px solid #e2e8f0",
                                                    borderRadius: 999,
                                                    padding: "2px 8px",
                                                }}
                                            >
                                                mapirano {matched}/{total}
                                            </span>
                                        )}
                                    </summary>

                                    {t.accessColumns.length > 0 && (
                                        <div style={{ marginTop: 8 }}>
                                            <div style={{ fontSize: 12, color: "#475569", marginBottom: 4 }}>Access kolone</div>
                                            <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                                                {t.accessColumns.map((c) => (
                                                    <span
                                                        key={`${t.key}-${c}`}
                                                        style={{
                                                            fontSize: 12,
                                                            background: "#f1f5f9",
                                                            border: "1px solid #e2e8f0",
                                                            borderRadius: 999,
                                                            padding: "2px 8px",
                                                        }}
                                                    >
                                                        {c}
                                                    </span>
                                                ))}
                                            </div>
                                        </div>
                                    )}

                                    {t.fieldMappings.length > 0 && (
                                        <table style={{ width: "100%", borderCollapse: "collapse", marginTop: 8, fontSize: 13 }}>
                                            <thead>
                                                <tr>
                                                    <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "4px" }}>Target polje</th>
                                                    <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "4px" }}>Access kolona</th>
                                                    <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "4px" }}>Status</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {t.fieldMappings.map((m) => (
                                                    <tr key={`${t.key}-${m.targetField}`}>
                                                        <td style={{ padding: "4px" }}>{m.targetField}</td>
                                                        <td style={{ padding: "4px", color: m.sourceColumn ? "#111827" : "#9ca3af" }}>
                                                            {m.sourceColumn ?? "-"}
                                                        </td>
                                                        <td style={{ padding: "4px", color: getMappingStatusColor(m), fontWeight: 600 }}>
                                                            {m.status}
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    )}
                                </details>
                            );
                        })}
                    </div>

                    {preview.warnings.length > 0 && (
                        <div style={{ marginTop: 10, background: "#fff7ed", border: "1px solid #fed7aa", color: "#9a3412", borderRadius: 8, padding: "8px 10px" }}>
                            {preview.warnings.map((w, i) => (
                                <div key={i}>- {w}</div>
                            ))}
                        </div>
                    )}
                </section>
            )}

            <section style={{ marginTop: 14, background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: 16 }}>
                <h3 style={{ marginTop: 0 }}>4) Istorija batch-eva</h3>
                <label style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10, fontSize: 13 }}>
                    <input
                        type="checkbox"
                        checked={deleteIncludeAnalytics}
                        onChange={(e) => setDeleteIncludeAnalytics(e.target.checked)}
                    />
                    Pri brisanju batch-a obriši i Analytics podatke (DataOrigin=access)
                </label>

                {deleteResult && (
                    <div style={{ marginBottom: 12, background: "#f0fdf4", border: "1px solid #bbf7d0", color: "#15803d", borderRadius: 8, padding: "10px 14px", fontSize: 13 }}>
                        <strong>Batch #{deleteResult.batchId} obrisan.</strong>{" "}
                        Artikli: {deleteResult.artikliDeleted}, Prodaje: {deleteResult.prodajaDeleted},
                        Stavke: {deleteResult.stavkeDeleted}, Sezone: {deleteResult.sezoneDeleted},
                        Dobavljaci: {deleteResult.dobavljaciDeleted}, Tipovi: {deleteResult.tipoviDeleted},
                        Dnevnik: {deleteResult.dnevnikDeleted}, Povracaji: {deleteResult.povracajDeleted} ({deleteResult.povracajStavkeDeleted} stavki),
                        ProductsDim: {deleteResult.productsDimDeleted}, SalesFacts: {deleteResult.salesFactsDeleted},
                        SalesLineFacts: {deleteResult.salesLineFactsDeleted}, InventoryMoves: {deleteResult.inventoryMovementsDeleted},
                        SuppliersDim: {deleteResult.suppliersDimDeleted}, SeasonsDim: {deleteResult.seasonsDimDeleted},
                        TypesDim: {deleteResult.footwearTypesDimDeleted}, StoresDim: {deleteResult.storesDimDeleted},
                        Cache invalidiran: {deleteResult.cacheInvalidated ? "da" : "ne"}.
                        <button
                            onClick={() => setDeleteResult(null)}
                            style={{ marginLeft: 10, border: "none", background: "transparent", color: "#15803d", cursor: "pointer", fontWeight: 700 }}
                        >x</button>
                    </div>
                )}

                {batches.length === 0 ? (
                    <p style={{ color: "#6b7280" }}>Nema import istorije.</p>
                ) : (
                    <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 14 }}>
                        <thead>
                            <tr>
                                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}>ID</th>
                                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}>Fajl</th>
                                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}>Status</th>
                                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}>Pocetak</th>
                                <th style={{ textAlign: "left", borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}>Kraj</th>
                                <th style={{ borderBottom: "1px solid #e5e7eb", padding: "6px 4px" }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {batches.map((b) => (
                                <tr key={b.id}>
                                    <td style={{ padding: "6px 4px" }}>{b.id}</td>
                                    <td style={{ padding: "6px 4px" }}>{b.sourceFileName}</td>
                                    <td style={{ padding: "6px 4px" }}>{b.status}</td>
                                    <td style={{ padding: "6px 4px" }}>{fmtDate(b.startedAtUtc)}</td>
                                    <td style={{ padding: "6px 4px" }}>{fmtDate(b.completedAtUtc)}</td>
                                    <td style={{ padding: "6px 4px", textAlign: "right" }}>
                                        <button
                                            type="button"
                                            disabled={deletingBatchId !== null}
                                            onClick={() => void handleDeleteBatch(b.id)}
                                            style={{
                                                border: "1px solid #fecaca",
                                                background: deletingBatchId === b.id ? "#fef2f2" : "#fff1f2",
                                                color: "#b91c1c",
                                                borderRadius: 6,
                                                padding: "4px 10px",
                                                cursor: deletingBatchId !== null ? "not-allowed" : "pointer",
                                                fontSize: 12,
                                                fontWeight: 600,
                                                opacity: deletingBatchId !== null && deletingBatchId !== b.id ? 0.5 : 1,
                                            }}
                                        >
                                            {deletingBatchId === b.id ? "Brisem..." : "Obrisi"}
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </section>
        </div>
    );
}
