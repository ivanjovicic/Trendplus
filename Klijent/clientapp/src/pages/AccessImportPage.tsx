import { useEffect, useMemo, useRef, useState } from "react";
import { DatabaseZap } from "lucide-react";
import {
    deleteAccessImportBatch,
    getAccessImportBatches,
    previewAccessImport,
    runAccessImport,
    type AccessImportBatchDto,
    type AccessImportPreviewResponse,
    type AccessImportRunResponse,
    type AccessImportTablePreview,
    type DeleteBatchResult,
} from "../services/accessImportApi";
import { InventoryKpiRow, InventoryPageShell } from "../components/inventory/InventoryPageShell";
import "./AccessImportPage.css";

type Tab = "source" | "preview" | "lastImport" | "batches";
type BatchStatusFilter = "all" | "Completed" | "Running" | "Pending" | "Failed";

function fmtDate(value: string | null): string {
    if (!value) return "—";
    return new Date(value).toLocaleString("sr-RS");
}

function matchedCount(t: AccessImportTablePreview): number {
    return t.fieldMappings.filter((m) => m.status.toLowerCase() === "matched").length;
}

function ResultLine({ entity, inserted, updated }: { entity: string; inserted: number; updated: number }) {
    if (inserted === 0 && updated === 0) return null;
    return (
        <div className="accimport-result-item">
            <span className="accimport-result-entity">{entity}</span>
            <span className="accimport-result-counts">
                <span className="ins">+{inserted}</span>{" / "}
                <span className="upd">{updated} upd</span>
            </span>
        </div>
    );
}

export default function AccessImportPage() {
    const fileInputRef = useRef<HTMLInputElement | null>(null);
    const [activeTab, setActiveTab] = useState<Tab>("source");
    const [file, setFile] = useState<File | null>(null);
    const [useRootFile, setUseRootFile] = useState(false);
    const [includeAnalytics, setIncludeAnalytics] = useState(true);
    const [overwriteExisting, setOverwriteExisting] = useState(true);
    const [preview, setPreview] = useState<AccessImportPreviewResponse | null>(null);
    const [runResult, setRunResult] = useState<AccessImportRunResponse | null>(null);
    const [batches, setBatches] = useState<AccessImportBatchDto[]>([]);
    const [batchStatusFilter, setBatchStatusFilter] = useState<BatchStatusFilter>("all");
    const [loadingPreview, setLoadingPreview] = useState(false);
    const [loadingImport, setLoadingImport] = useState(false);
    const [importElapsed, setImportElapsed] = useState(0);
    const [deletingBatchId, setDeletingBatchId] = useState<number | null>(null);
    const [deleteIncludeAnalytics, setDeleteIncludeAnalytics] = useState(true);
    const [deleteResult, setDeleteResult] = useState<DeleteBatchResult | null>(null);
    const [error, setError] = useState<string | null>(null);

    // --- data loading ---

    const refreshBatches = async () => {
        try { setBatches(await getAccessImportBatches(50)); } catch { /* best effort */ }
    };

    useEffect(() => { void refreshBatches(); }, []);

    // elapsed timer while importing
    useEffect(() => {
        if (!loadingImport) { setImportElapsed(0); return; }
        const t0 = Date.now();
        const id = setInterval(() => setImportElapsed(Math.floor((Date.now() - t0) / 1000)), 500);
        return () => clearInterval(id);
    }, [loadingImport]);

    // --- handlers ---

    const handlePreview = async () => {
        setError(null);
        setLoadingPreview(true);
        try {
            const data = await previewAccessImport(file, useRootFile);
            setPreview(data);
            setActiveTab("preview");
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greska pri analizi ACCDB fajla.");
        } finally {
            setLoadingPreview(false);
        }
    };

    const handleImport = async () => {
        setError(null);
        setLoadingImport(true);
        try {
            const data = await runAccessImport(file, { useRootFile, includeAnalytics, overwriteExisting });
            setRunResult(data);
            setActiveTab("lastImport");
            await refreshBatches();
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greska pri importu.");
        } finally {
            setLoadingImport(false);
        }
    };

    const handleDeleteBatch = async (batchId: number) => {
        if (!window.confirm(`Obrisati batch #${batchId}? Ova akcija je nepovratna.`)) return;
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

    // --- derived ---

    const filteredBatches = useMemo(
        () => batchStatusFilter === "all" ? batches : batches.filter((b) => b.status === batchStatusFilter),
        [batches, batchStatusFilter],
    );

    const busy = loadingPreview || loadingImport;

    // --- render ---

    return (
        <InventoryPageShell
            icon={DatabaseZap}
            title="Access Import"
            subtitle="Safe import workflow za Access fajl, sa schema preview, batch istorijom i rollback brisanjem."
        >
            <InventoryKpiRow
                items={[
                    { label: "Batch zapisi", value: `${batches.length}` },
                    { label: "Aktivni tab", value: activeTab },
                    { label: "Status", value: busy ? "U toku" : "Idle", tone: busy ? "warning" : "positive" },
                    { label: "Filter", value: batchStatusFilter },
                ]}
            />

            <div className="accimport-page">
            <h1 className="accimport-title">Access Import (TRENDPLUS.accdb)</h1>
            <p className="accimport-subtitle">
                ETL pipeline: Access → Trendplus DB + Analytics DB. Podrska za upsert, analizu seme, batch istoriju i kaskadno brisanje.
            </p>

            {/* ---- Tabs ---- */}
            <div className="accimport-tabs">
                {([
                    ["source", "Izvor"],
                    ["preview", "Analiza seme"],
                    ["lastImport", "Poslednji import"],
                    ["batches", "Istorija batch-eva"],
                ] as const).map(([key, label]) => (
                    <button
                        key={key}
                        className={`accimport-tab ${activeTab === key ? "active" : ""}`}
                        onClick={() => setActiveTab(key)}
                        type="button"
                    >
                        {label}
                    </button>
                ))}
            </div>

            {/* ---- Global error ---- */}
            {error && <div className="accimport-error">{error}</div>}

            {/* ---- Progress bar (visible on any tab while importing) ---- */}
            {loadingImport && (
                <div className="accimport-progress">
                    <div className="accimport-progress-title">Import u toku...</div>
                    <div className="accimport-progress-bar-track">
                        <div className="accimport-progress-bar-fill" style={{ width: "100%", animation: "pulse 1.5s ease-in-out infinite" }} />
                    </div>
                    <div className="accimport-progress-meta">
                        <span>Status: Running</span>
                        <span>Proteklo: {importElapsed}s</span>
                    </div>
                </div>
            )}

            {/* ============ TAB: Source ============ */}
            {activeTab === "source" && (
                <div className="accimport-filterbar">
                    {/* File source */}
                    <div className="accimport-field" style={{ gridColumn: "1 / -1" }}>
                        <span className="accimport-label">Izvor ACCDB fajla</span>
                        <label className="accimport-checkbox-row">
                            <input type="checkbox" checked={useRootFile} onChange={(e) => setUseRootFile(e.target.checked)} />
                            Koristi TRENDPLUS.accdb automatski (root ili parent folder)
                        </label>
                    </div>

                    <div className="accimport-field">
                        <span className="accimport-label">Rucni izbor fajla</span>
                        <input ref={fileInputRef} type="file" accept=".accdb" style={{ display: "none" }} onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
                        <div className="accimport-file-row">
                            <button type="button" className="accimport-btn accimport-btn-secondary" onClick={() => fileInputRef.current?.click()}>
                                Browse .accdb
                            </button>
                            {file && (
                                <button type="button" className="accimport-btn accimport-btn-clear" onClick={() => { setFile(null); if (fileInputRef.current) fileInputRef.current.value = ""; }}>
                                    Obrisi
                                </button>
                            )}
                            <span className={`accimport-file-name ${file ? "has-file" : ""}`}>
                                {file ? file.name : "Nije izabran fajl"}
                            </span>
                        </div>
                    </div>

                    {/* Options */}
                    <div className="accimport-field">
                        <span className="accimport-label">Opcije importa</span>
                        <label className="accimport-checkbox-row">
                            <input type="checkbox" checked={includeAnalytics} onChange={(e) => setIncludeAnalytics(e.target.checked)} />
                            Ukljuci upis u Analytics
                        </label>
                        <label className="accimport-checkbox-row">
                            <input type="checkbox" checked={overwriteExisting} onChange={(e) => setOverwriteExisting(e.target.checked)} />
                            Azuriraj postojece redove (upsert)
                        </label>
                    </div>

                    {/* Actions */}
                    <div className="accimport-field">
                        <span className="accimport-label">Akcije</span>
                        <div className="accimport-actions">
                            <button className="accimport-btn accimport-btn-primary" onClick={() => void handlePreview()} disabled={busy}>
                                {loadingPreview ? "Analiziram..." : "Analiza seme"}
                            </button>
                            <button className="accimport-btn accimport-btn-success" onClick={() => void handleImport()} disabled={busy}>
                                {loadingImport ? "Importujem..." : "Pokreni import"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* ============ TAB: Preview (Schema Analysis) ============ */}
            {activeTab === "preview" && (
                <>
                    {!preview && !loadingPreview && (
                        <div className="accimport-empty-state">
                            Pokrenite "Analiza seme" na Izvor tabu da vidite rezultat.
                        </div>
                    )}
                    {loadingPreview && <div className="accimport-empty-state">Analiziram ACCDB fajl...</div>}
                    {preview && (
                        <div className="accimport-card">
                            <h3 className="accimport-card-title">
                                Rezultat analize — {preview.sourceFileName}
                                <span style={{ marginLeft: 10 }}>
                                    {preview.canImport
                                        ? <span className="accimport-status accimport-status-Completed">Moze se importovati</span>
                                        : <span className="accimport-status accimport-status-Failed">Nije moguce importovati</span>
                                    }
                                </span>
                            </h3>

                            {preview.warnings.length > 0 && (
                                <div className="accimport-warning">
                                    {preview.warnings.map((w, i) => <div key={i}>⚠ {w}</div>)}
                                </div>
                            )}

                            {/* KPI summary */}
                            <div className="accimport-kpis">
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Pronadjene tabele</div>
                                    <div className="accimport-kpi-value">{preview.tables.filter((t) => t.found).length} / {preview.tables.length}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Ukupno redova</div>
                                    <div className="accimport-kpi-value">{preview.tables.reduce((s, t) => s + t.rowCount, 0).toLocaleString("sr-RS")}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Access tabele u fajlu</div>
                                    <div className="accimport-kpi-value">{preview.availableTables.length}</div>
                                </div>
                            </div>

                            {/* Schema details */}
                            <div style={{ display: "grid", gap: 8 }}>
                                {preview.tables.map((t) => {
                                    const hits = matchedCount(t);
                                    const total = t.fieldMappings.length;
                                    return (
                                        <details key={t.key} className="accimport-schema-details">
                                            <summary className="accimport-schema-summary">
                                                <strong>{t.key}</strong>
                                                <span style={{ color: t.found ? "#e5e7eb" : "#6b7280" }}>{t.tableName ?? "—"}</span>
                                                <span style={{ fontSize: 12, color: "#6b7280" }}>rows: {t.rowCount}</span>
                                                {total > 0 && (
                                                    <span className={`accimport-schema-badge ${hits === total ? "all-matched" : "partial"}`}>
                                                        mapirano {hits}/{total}
                                                    </span>
                                                )}
                                            </summary>

                                            {t.accessColumns.length > 0 && (
                                                <div style={{ marginTop: 8 }}>
                                                    <div className="accimport-label" style={{ marginBottom: 4 }}>Access kolone</div>
                                                    <div className="accimport-col-chips">
                                                        {t.accessColumns.map((c) => (
                                                            <span key={`${t.key}-${c}`} className="accimport-col-chip">{c}</span>
                                                        ))}
                                                    </div>
                                                </div>
                                            )}

                                            {t.fieldMappings.length > 0 && (
                                                <div className="accimport-scroll" style={{ marginTop: 8, maxHeight: 200 }}>
                                                    <table className="accimport-table">
                                                        <thead>
                                                            <tr>
                                                                <th>Target polje</th>
                                                                <th>Access kolona</th>
                                                                <th>Status</th>
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {t.fieldMappings.map((m) => (
                                                                <tr key={`${t.key}-${m.targetField}`}>
                                                                    <td>{m.targetField}</td>
                                                                    <td style={{ color: m.sourceColumn ? "#e5e7eb" : "#6b7280" }}>{m.sourceColumn ?? "—"}</td>
                                                                    <td>
                                                                        <span className={`accimport-mapping-badge ${m.status.toLowerCase() === "matched" ? "accimport-mapping-matched" : "accimport-mapping-missing"}`}>
                                                                            {m.status}
                                                                        </span>
                                                                    </td>
                                                                </tr>
                                                            ))}
                                                        </tbody>
                                                    </table>
                                                </div>
                                            )}
                                        </details>
                                    );
                                })}
                            </div>
                        </div>
                    )}
                </>
            )}

            {/* ============ TAB: Last Import ============ */}
            {activeTab === "lastImport" && (
                <>
                    {!runResult && !loadingImport && (
                        <div className="accimport-empty-state">Nije pokrenut import u ovoj sesiji.</div>
                    )}
                    {runResult && (
                        <>
                            {/* KPI row */}
                            <div className="accimport-kpis">
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Status</div>
                                    <div className="accimport-kpi-value">
                                        <span className={`accimport-status accimport-status-${runResult.status}`}>{runResult.status}</span>
                                    </div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Batch ID</div>
                                    <div className="accimport-kpi-value">#{runResult.batchId}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Fajl</div>
                                    <div className="accimport-kpi-value" style={{ fontSize: 14, wordBreak: "break-all" }}>{runResult.sourceFileName}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Pocetak</div>
                                    <div className="accimport-kpi-value" style={{ fontSize: 14 }}>{fmtDate(runResult.startedAtUtc)}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Kraj</div>
                                    <div className="accimport-kpi-value" style={{ fontSize: 14 }}>{fmtDate(runResult.completedAtUtc)}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Analytics</div>
                                    <div className="accimport-kpi-value">{runResult.includeAnalytics ? "Da" : "Ne"}</div>
                                </div>
                            </div>

                            {/* Entity counters */}
                            <div className="accimport-card">
                                <h3 className="accimport-card-title">Rezultati importa — Trendplus entiteti</h3>
                                <div className="accimport-result-grid">
                                    <ResultLine entity="Artikli" inserted={runResult.artikliInserted} updated={runResult.artikliUpdated} />
                                    <ResultLine entity="Dobavljaci" inserted={runResult.dobavljaciInserted} updated={runResult.dobavljaciUpdated} />
                                    <ResultLine entity="Sezone" inserted={runResult.sezoneInserted} updated={runResult.sezoneUpdated} />
                                    <ResultLine entity="Tipovi obuce" inserted={runResult.tipoviInserted} updated={runResult.tipoviUpdated} />
                                    <ResultLine entity="Prodaje" inserted={runResult.prodajaInserted} updated={runResult.prodajaUpdated} />
                                    <ResultLine entity="Stavke prodaje" inserted={runResult.prodajaStavkeInserted} updated={runResult.prodajaStavkeUpdated} />
                                    <ResultLine entity="Dnevnik promena" inserted={runResult.dnevnikInserted} updated={runResult.dnevnikUpdated} />
                                    <ResultLine entity="Povracaji" inserted={runResult.povracajInserted} updated={runResult.povracajUpdated} />
                                    <ResultLine entity="Stavke povracaja" inserted={runResult.povracajStavkeInserted} updated={runResult.povracajStavkeUpdated} />
                                </div>
                            </div>

                            {runResult.includeAnalytics && (
                                <div className="accimport-card">
                                    <h3 className="accimport-card-title">Rezultati importa — Analytics dimenzije</h3>
                                    <div className="accimport-result-grid">
                                        <ResultLine entity="ProductsDim" inserted={runResult.productsDimInserted} updated={runResult.productsDimUpdated} />
                                        <ResultLine entity="SalesFacts" inserted={runResult.salesFactsInserted} updated={runResult.salesFactsUpdated} />
                                        <ResultLine entity="SalesLineFacts" inserted={runResult.salesLineFactsInserted} updated={0} />
                                        <ResultLine entity="Stores" inserted={runResult.storesInserted} updated={runResult.storesUpdated} />
                                    </div>
                                </div>
                            )}

                            {runResult.warnings.length > 0 && (
                                <div className="accimport-warning">
                                    {runResult.warnings.map((w, i) => <div key={i}>⚠ {w}</div>)}
                                </div>
                            )}
                        </>
                    )}
                </>
            )}

            {/* ============ TAB: Batch History ============ */}
            {activeTab === "batches" && (
                <>
                    {/* Filter + options bar */}
                    <div className="accimport-filterbar" style={{ gridTemplateColumns: "auto auto 1fr" }}>
                        <div className="accimport-field">
                            <span className="accimport-label">Filter po statusu</span>
                            <select className="accimport-input" value={batchStatusFilter} onChange={(e) => setBatchStatusFilter(e.target.value as BatchStatusFilter)} style={{ minWidth: 160 }}>
                                <option value="all">Svi statusi</option>
                                <option value="Completed">Completed</option>
                                <option value="Running">Running</option>
                                <option value="Pending">Pending</option>
                                <option value="Failed">Failed</option>
                            </select>
                        </div>
                        <div className="accimport-field">
                            <span className="accimport-label">Opcije brisanja</span>
                            <label className="accimport-checkbox-row">
                                <input type="checkbox" checked={deleteIncludeAnalytics} onChange={(e) => setDeleteIncludeAnalytics(e.target.checked)} />
                                Obrisi i Analytics podatke (DataOrigin=access)
                            </label>
                        </div>
                        <div className="accimport-field" style={{ alignItems: "flex-end" }}>
                            <button className="accimport-btn accimport-btn-secondary" onClick={() => void refreshBatches()}>
                                &#8635; Osvezi
                            </button>
                        </div>
                    </div>

                    {/* Delete result banner */}
                    {deleteResult && (
                        <div className="accimport-delete-banner">
                            <strong>Batch #{deleteResult.batchId} obrisan:</strong>
                            Artikli: {deleteResult.artikliDeleted},
                            Prodaje: {deleteResult.prodajaDeleted},
                            Stavke: {deleteResult.stavkeDeleted},
                            Sezone: {deleteResult.sezoneDeleted},
                            Dobavljaci: {deleteResult.dobavljaciDeleted},
                            Tipovi: {deleteResult.tipoviDeleted},
                            Dnevnik: {deleteResult.dnevnikDeleted},
                            Povracaji: {deleteResult.povracajDeleted} ({deleteResult.povracajStavkeDeleted} stavki),
                            ProductsDim: {deleteResult.productsDimDeleted},
                            SalesFacts: {deleteResult.salesFactsDeleted},
                            SalesLineFacts: {deleteResult.salesLineFactsDeleted},
                            Inv.Moves: {deleteResult.inventoryMovementsDeleted},
                            Cache: {deleteResult.cacheInvalidated ? "yes" : "no"}
                            <button className="accimport-delete-banner-close" onClick={() => setDeleteResult(null)}>&times;</button>
                        </div>
                    )}

                    {/* Table */}
                    <div className="accimport-table-wrap">
                        <h3 className="accimport-table-title">Batch istorija ({filteredBatches.length})</h3>
                        <div className="accimport-scroll">
                            <table className="accimport-table">
                                <thead>
                                    <tr>
                                        <th>ID</th>
                                        <th>Fajl</th>
                                        <th>Status</th>
                                        <th>Pocetak</th>
                                        <th>Kraj</th>
                                        <th className="align-right">Akcije</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredBatches.length === 0 && (
                                        <tr><td colSpan={6} className="accimport-empty-state">Nema batch zapisa za izabrani filter.</td></tr>
                                    )}
                                    {filteredBatches.map((b) => (
                                        <tr key={b.id}>
                                            <td>#{b.id}</td>
                                            <td>{b.sourceFileName}</td>
                                            <td><span className={`accimport-status accimport-status-${b.status}`}>{b.status}</span></td>
                                            <td>{fmtDate(b.startedAtUtc)}</td>
                                            <td>{fmtDate(b.completedAtUtc)}</td>
                                            <td className="align-right">
                                                <button
                                                    type="button"
                                                    disabled={deletingBatchId !== null}
                                                    onClick={() => void handleDeleteBatch(b.id)}
                                                    className="accimport-btn accimport-btn-danger"
                                                    style={{ opacity: deletingBatchId !== null && deletingBatchId !== b.id ? 0.5 : 1 }}
                                                >
                                                    {deletingBatchId === b.id ? "Brisem..." : "Obrisi"}
                                                </button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </>
            )}
            </div>
        </InventoryPageShell>
    );
}
