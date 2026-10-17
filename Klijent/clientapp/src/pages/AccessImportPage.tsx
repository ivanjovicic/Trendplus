import { useEffect, useMemo, useRef, useState } from "react";
import { DatabaseZap } from "lucide-react";
import {
    cancelAccessImportBatch,
    isAccessImportRequestCanceledError,
    deleteAccessImportBatch,
    getAccessImportRuntimeStatus,
    getAccessImportBatches,
    ACCESS_IMPORT_MAX_UPLOAD_BYTES,
    accessImportFileTooLargeMessage,
    previewAccessImport,
    runAccessImport,
    previewCleanupNonAccess,
    executeCleanupNonAccess,
    findActiveAccessImportBatch,
    type AccessImportBatchDto,
    type AccessImportCoverageMetric,
    type AccessImportPreviewResponse,
    type AccessImportRuntimeStatusResponse,
    type AccessImportRunResponse,
    type AccessImportTablePreview,
    type DeleteBatchResult,
} from "../services/accessImportApi";
import { clearArtikliClientCaches } from "../services/artikliApi";
import { InventoryKpiRow, InventoryPageShell } from "../components/inventory/InventoryPageShell";
import { clearProdajaLookupCache } from "../services/prodajaApi";
import { setDataScope } from "../utils/dataScope";
import "./AccessImportPage.css";
import { getRestoreScript } from "../services/accessImportRestoreApi";

type Tab = "source" | "preview" | "lastImport" | "batches" | "cleanup";
type BatchStatusFilter = "all" | "completed" | "running" | "pending" | "failed" | "cancelled" | "interrupted";
const TERMINAL_BATCH_STATUSES = new Set(["completed", "failed", "cancelled", "interrupted"]);

function fmtDate(value: string | null): string {
    if (!value) return "-";
    return new Date(value).toLocaleString("sr-RS");
}

function matchedCount(t: AccessImportTablePreview): number {
    return t.matchedMappings ?? t.fieldMappings.filter((m) => m.status.toLowerCase() === "matched").length;
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

function describeTransformation(metric: AccessImportCoverageMetric): string {
    if (metric.transformationType === "expanded")
        return metric.expandedTargetRows > 0 ? `Proširenje (+${metric.expandedTargetRows.toLocaleString("sr-RS")})` : "Proširenje";
    if (metric.transformationType === "grouped")
        return metric.mergedRows > 0 ? `Grupisanje (-${metric.mergedRows.toLocaleString("sr-RS")})` : "Grupisanje";
    return "1:1";
}

function createEmptyRunResult(batchId: number, sourceFileName: string, includeAnalytics: boolean, startedAtUtc: string, status = "running", completedAtUtc: string | null = null, warnings: string[] = []): AccessImportRunResponse {
    return {
        batchId,
        status,
        sourceFileName,
        includeAnalytics,
        startedAtUtc,
        completedAtUtc,
        tipoviInserted: 0,
        tipoviUpdated: 0,
        dobavljaciInserted: 0,
        dobavljaciUpdated: 0,
        sezoneInserted: 0,
        sezoneUpdated: 0,
        artikliInserted: 0,
        artikliUpdated: 0,
        prodajaInserted: 0,
        prodajaUpdated: 0,
        prodajaStavkeInserted: 0,
        prodajaStavkeUpdated: 0,
        dnevnikInserted: 0,
        dnevnikUpdated: 0,
        povracajInserted: 0,
        povracajUpdated: 0,
        povracajStavkeInserted: 0,
        povracajStavkeUpdated: 0,
        productsDimInserted: 0,
        productsDimUpdated: 0,
        salesFactsInserted: 0,
        salesFactsUpdated: 0,
        salesLineFactsInserted: 0,
        storesInserted: 0,
        storesUpdated: 0,
        sourceRowsByTable: {},
        importedRowsByTable: {},
        coverageByTable: {},
        warnings,
    };
}

// RQ675 (owner decision 2026-10-15): the admin key lives only in sessionStorage for this tab session,
// is typed into a masked field and can be cleared explicitly. Never localStorage, URLs or logs.
export const ACCESS_IMPORT_ADMIN_KEY_STORAGE = "trendplus.accessImport.adminKey";

function readStoredAdminKey(): string | null {
    try {
        const value = window.sessionStorage.getItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE);
        return value && value.trim() ? value : null;
    } catch {
        return null;
    }
}

function writeStoredAdminKey(key: string | null) {
    try {
        if (key) window.sessionStorage.setItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE, key);
        else window.sessionStorage.removeItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE);
    } catch {
        // storage unavailable (private mode): keep the key in memory only
    }
}

export default function AccessImportPage() {
    const fileInputRef = useRef<HTMLInputElement | null>(null);
    const [activeTab, setActiveTab] = useState<Tab>("source");
    const [file, setFile] = useState<File | null>(null);
    const [useRootFile, setUseRootFile] = useState(false);
    const [includeAnalytics, setIncludeAnalytics] = useState(true);
    const [overwriteExisting] = useState(false);
    const [sourceNamespace, setSourceNamespace] = useState("");
    const [preview, setPreview] = useState<AccessImportPreviewResponse | null>(null);
    const [runResult, setRunResult] = useState<AccessImportRunResponse | null>(null);
    const [batches, setBatches] = useState<AccessImportBatchDto[]>([]);
    const [batchHistoryWarning, setBatchHistoryWarning] = useState<string | null>(null);
    const [batchStatusFilter, setBatchStatusFilter] = useState<BatchStatusFilter>("all");
    const [loadingPreview, setLoadingPreview] = useState(false);
    const [loadingImport, setLoadingImport] = useState(false);
    const [runningBatchId, setRunningBatchId] = useState<number | null>(null);
    const [cancellingImport, setCancellingImport] = useState(false);
    const [importElapsed, setImportElapsed] = useState(0);
    const [deletingBatchId, setDeletingBatchId] = useState<number | null>(null);
    const [deleteIncludeAnalytics, setDeleteIncludeAnalytics] = useState(true);
    const [deleteResult, setDeleteResult] = useState<DeleteBatchResult | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [runtimeStatus, setRuntimeStatus] = useState<AccessImportRuntimeStatusResponse | null>(null);
    const [cleanupPreview, setCleanupPreview] = useState<Record<string, number> | null>(null);
    const [cleanupLoadingPreview, setCleanupLoadingPreview] = useState(false);
    const [cleanupExecuting, setCleanupExecuting] = useState(false);
    const [cleanupResult, setCleanupResult] = useState<{ executed: boolean; deleted: Record<string, number> } | null>(null);
    const [archiveIdsInput, setArchiveIdsInput] = useState<string>("");
    const [generatingRestoreScript, setGeneratingRestoreScript] = useState(false);
    const [restoreScript, setRestoreScript] = useState<string | null>(null);
    const batchPollInFlightRef = useRef(false);
    const lastPollWarningAtRef = useRef(0);
    const adminKeyRef = useRef<string | null>(readStoredAdminKey());
    const adminKeyInputRef = useRef<HTMLInputElement | null>(null);
    const [adminKeyInput, setAdminKeyInput] = useState("");
    const [adminKeySaved, setAdminKeySaved] = useState<boolean>(() => adminKeyRef.current !== null);
    const [activeRunCheck, setActiveRunCheck] = useState<"pending" | "needsKey" | "done">("pending");

    // --- data loading ---

    const refreshBatches = async (reason: string, adminKey?: string) => {
        const key = adminKey ?? adminKeyRef.current ?? undefined;
        try {
            const rows = await getAccessImportBatches(50, reason, key);
            setBatches(rows);
            setBatchHistoryWarning(null);
            setActiveRunCheck("done");
            if (key) {
                try {
                    setRuntimeStatus(await getAccessImportRuntimeStatus(key));
                } catch {
                    setRuntimeStatus(null);
                }
            }
            return rows;
        } catch (error) {
            if (isUnauthorizedError(error)) {
                clearAdminKey();
                if (reason === "mount") {
                    // A fresh tab has no admin key yet; ask for it instead of showing an error.
                    setActiveRunCheck("needsKey");
                    return [] as AccessImportBatchDto[];
                }
                setError("Pregled import istorije zahteva važeći admin key.");
                return [] as AccessImportBatchDto[];
            }
            if (!isAccessImportRequestCanceledError(error)) {
                // RQ674: keep the last known rows and say the history is temporarily unavailable instead of showing "no imports".
                setBatchHistoryWarning(error instanceof Error && error.message ? error.message : "Istorija importa trenutno nije dostupna. Pokušaj ponovo.");
                console.debug("[access-import][batches] refresh failed", {
                    reason,
                    message: error instanceof Error ? error.message : String(error),
                });
            }
            return [] as AccessImportBatchDto[];
        }
    };

    const applySuccessfulImportSideEffects = () => {
        clearArtikliClientCaches();
        clearProdajaLookupCache();
        setDataScope("all");
        window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    };

    const promptAdminKey = (actionLabel: string): string | null => {
        if (adminKeyRef.current) return adminKeyRef.current;
        setError(`Admin key je obavezan za akciju: ${actionLabel}. Unesite ga u polje „Admin key“.`);
        adminKeyInputRef.current?.focus();
        return null;
    };

    const rememberAdminKey = (key: string) => {
        adminKeyRef.current = key;
        writeStoredAdminKey(key);
        setAdminKeySaved(true);
    };

    const clearAdminKey = () => {
        adminKeyRef.current = null;
        writeStoredAdminKey(null);
        setAdminKeySaved(false);
        setAdminKeyInput("");
    };

    const submitAdminKey = () => {
        const key = adminKeyInput.trim();
        if (!key) {
            setError("Admin key je obavezan.");
            return;
        }
        rememberAdminKey(key);
        setAdminKeyInput("");
        setError(null);
        void refreshBatches("active-run-check", key);
    };

    const isUnauthorizedError = (error: unknown): boolean => {
        if (!(error instanceof Error)) return false;
        const msg = error.message.toLowerCase();
        return msg.includes("401")
            || msg.includes("403")
            || msg.includes("unauthorized")
            || msg.includes("forbidden");
    };

    const hydrateRunResultFromBatch = (batch: AccessImportBatchDto, fallbackIncludeAnalytics: boolean) => {
        if (batch.summaryJson) {
            try {
                const parsed = JSON.parse(batch.summaryJson) as AccessImportRunResponse;
                return {
                    ...createEmptyRunResult(
                        batch.id,
                        batch.sourceFileName,
                        fallbackIncludeAnalytics,
                        batch.startedAtUtc,
                        batch.status,
                        batch.completedAtUtc,
                        batch.errorMessage ? [batch.errorMessage] : [],
                    ),
                    ...parsed,
                    batchId: parsed.batchId || batch.id,
                    status: parsed.status || batch.status,
                    sourceFileName: parsed.sourceFileName || batch.sourceFileName,
                    startedAtUtc: parsed.startedAtUtc || batch.startedAtUtc,
                    completedAtUtc: parsed.completedAtUtc ?? batch.completedAtUtc,
                    warnings: parsed.warnings?.length ? parsed.warnings : (batch.errorMessage ? [batch.errorMessage] : []),
                } satisfies AccessImportRunResponse;
            } catch {
                // fall through to minimal shape
            }
        }

        return createEmptyRunResult(
            batch.id,
            batch.sourceFileName,
            fallbackIncludeAnalytics,
            batch.startedAtUtc,
            batch.status,
            batch.completedAtUtc,
            batch.errorMessage ? [batch.errorMessage] : [],
        );
    };

    // Detect an import that is already queued or running on the server (page reload, another session).
    useEffect(() => {
        void refreshBatches("mount");
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const activeServerBatch = useMemo(() => findActiveAccessImportBatch(batches), [batches]);

    useEffect(() => {
        if (!activeServerBatch || runningBatchId !== null || loadingImport) return;
        setRunningBatchId(activeServerBatch.batch.id);
        setRunResult(hydrateRunResultFromBatch(activeServerBatch.batch, includeAnalytics));
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [activeServerBatch]);

    const trackedBatch = runningBatchId !== null
        ? batches.find((row) => row.id === runningBatchId) ?? null
        : null;
    const trackedBatchStale = activeServerBatch !== null
        && activeServerBatch.batch.id === runningBatchId
        && activeServerBatch.stale;

    // Keep "root file" and "manual file" mutually exclusive (reduces confusion).
    useEffect(() => {
        if (!useRootFile) return;
        setFile(null);
        if (fileInputRef.current) fileInputRef.current.value = "";
        setPreview(null);
    }, [useRootFile]);

    // Invalidate schema preview when the source file changes.
    useEffect(() => {
        setPreview(null);
    }, [file]);

    const importBusy = loadingImport || runningBatchId !== null;

    // elapsed timer while importing
    useEffect(() => {
        if (!importBusy) { setImportElapsed(0); return; }
        // Count from the server start time so a reload or second session shows the real duration.
        const serverStart = runResult?.startedAtUtc ? Date.parse(runResult.startedAtUtc) : Number.NaN;
        const t0 = Number.isFinite(serverStart) && serverStart <= Date.now() ? serverStart : Date.now();
        const id = setInterval(() => setImportElapsed(Math.floor((Date.now() - t0) / 1000)), 500);
        return () => clearInterval(id);
    }, [importBusy, runResult?.startedAtUtc]);

    useEffect(() => {
        if (runningBatchId === null) return;

        let cancelled = false;

        const pollBatch = async () => {
            if (batchPollInFlightRef.current) return;
            batchPollInFlightRef.current = true;
            try {
                const rows = await refreshBatches("poll-running-batch");
                if (cancelled) return;

                const polledBatch = rows.find((row) => row.id === runningBatchId);
                if (!polledBatch) return;

                const nextRunResult = hydrateRunResultFromBatch(
                    polledBatch,
                    runResult?.includeAnalytics ?? includeAnalytics,
                );

                setRunResult(nextRunResult);

                const normalizedStatus = polledBatch.status.toLowerCase();
                if (TERMINAL_BATCH_STATUSES.has(normalizedStatus)) {
                    setRunningBatchId(null);
                    if (normalizedStatus === "completed") {
                        applySuccessfulImportSideEffects();
                    } else if (polledBatch.errorMessage) {
                        setError(polledBatch.errorMessage);
                    }
                }
            } catch (e: unknown) {
                if (cancelled) return;
                if (isAccessImportRequestCanceledError(e)) return;
                const message = e instanceof Error ? e.message : "Greška pri praćenju batch statusa.";
                const now = Date.now();
                if (now - lastPollWarningAtRef.current >= 30_000) {
                    console.warn("Access import polling skipped after transient failure:", message);
                    lastPollWarningAtRef.current = now;
                }
            } finally {
                batchPollInFlightRef.current = false;
            }
        };

        void pollBatch();
        const id = window.setInterval(() => { void pollBatch(); }, 5000);

        return () => {
            cancelled = true;
            window.clearInterval(id);
        };
    }, [runningBatchId, includeAnalytics, runResult?.includeAnalytics]);

    // --- handlers ---

    const handlePreview = async () => {
        setError(null);
        setLoadingPreview(true);
        try {
            const data = await previewAccessImport(file, useRootFile);
            setPreview(data);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greška pri analizi Access fajla.");
        } finally {
            setLoadingPreview(false);
        }
    };

    const handleImport = async () => {
        const adminKey = promptAdminKey("Pokretanje importa");
        if (!adminKey) return;

        setError(null);
        setLoadingImport(true);
        try {
            const data = await runAccessImport(file, { useRootFile, includeAnalytics, overwriteExisting, sourceNamespace, adminKey });
            setRunResult(data);
            setActiveTab("lastImport");
            await refreshBatches("after-run");
            const normalizedStatus = data.status.toLowerCase();
            if (normalizedStatus === "running" || normalizedStatus === "pending") {
                setRunningBatchId(data.batchId);
            } else if (normalizedStatus === "completed") {
                applySuccessfulImportSideEffects();
            } else if (TERMINAL_BATCH_STATUSES.has(normalizedStatus)) {
                setRunningBatchId(null);
                if (data.warnings?.length) {
                    setError(data.warnings[data.warnings.length - 1]);
                }
            }
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greška pri importu.");
        } finally {
            setLoadingImport(false);
        }
    };

    const handleCancelImport = async () => {
        if (runningBatchId === null) return;
        const adminKey = adminKeyRef.current ?? promptAdminKey("Otkazivanje importa");
        if (!adminKey) return;

        setError(null);
        setCancellingImport(true);
        try {
            await cancelAccessImportBatch(runningBatchId, adminKey);
            await refreshBatches("after-cancel", adminKey);
        } catch (e: unknown) {
            if (isUnauthorizedError(e)) {
                clearAdminKey();
                setError("Otkazivanje importa zahteva važeći admin key.");
                return;
            }
            setError(e instanceof Error ? e.message : "Greška pri slanju zahteva za otkazivanje importa.");
        } finally {
            setCancellingImport(false);
        }
    };

    const handleDeleteBatch = async (batchId: number) => {
        if (!window.confirm(`Obrisati batch #${batchId}? Ova akcija je nepovratna.`)) return;
        const adminKey = promptAdminKey(`Brisanje batch istorije #${batchId}`);
        if (!adminKey) return;

        setError(null);
        setDeleteResult(null);
        setDeletingBatchId(batchId);
        try {
            const result = await deleteAccessImportBatch(batchId, deleteIncludeAnalytics, adminKey);
            setDeleteResult(result);
            await refreshBatches("after-delete");
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greška pri brisanju batch-a.");
        } finally {
            setDeletingBatchId(null);
        }
    };

    const handleCleanupPreview = async () => {
        const adminKey = promptAdminKey("Pregled cleanup ne-Access zapisa");
        if (!adminKey) return;

        setError(null);
        setCleanupLoadingPreview(true);
        try {
            const data = await previewCleanupNonAccess(adminKey);
            setCleanupPreview(data);
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Greška pri pregledanju podataka za brisanje.");
        } finally {
            setCleanupLoadingPreview(false);
        }
    };

    const handleCleanupExecute = async () => {
        if (!window.confirm("Potvrdite brisanje svih zapisa koji NISU iz Access-a. Ova akcija je nepovratna.")) return;
        const adminKey = promptAdminKey("Cleanup ne-Access zapisa");
        if (!adminKey) return;

        setError(null);
        setCleanupExecuting(true);
        setCleanupResult(null);
        try {
            const res = await executeCleanupNonAccess(true, adminKey);
            setCleanupResult(res);
            // optionally refresh batches / caches
            await refreshBatches("after-cleanup");
        } catch (e: unknown) {
            if (isUnauthorizedError(e)) {
                clearAdminKey();
                const retryKey = promptAdminKey("Cleanup ne-Access zapisa (ponovni unos ključa)");
                if (!retryKey) return;

                try {
                    const res = await executeCleanupNonAccess(true, retryKey);
                    setCleanupResult(res);
                    await refreshBatches("after-cleanup-retry");
                    return;
                } catch (retryError: unknown) {
                    setError(retryError instanceof Error ? retryError.message : "Greška pri brisanju podataka.");
                    return;
                }
            }

            setError(e instanceof Error ? e.message : "Greška pri brisanju podataka.");
        } finally {
            setCleanupExecuting(false);
        }
    };

    // --- derived ---

    const filteredBatches = useMemo(
        () => batchStatusFilter === "all"
            ? batches
            : batches.filter((b) => b.status.toLowerCase() === batchStatusFilter),
        [batches, batchStatusFilter],
    );

    const busy = loadingPreview || importBusy;
    const sourceSelected = useRootFile || !!file;
    const previewBlocksImport = preview !== null && !preview.canImport;
    const runtimeBlocksImport = runtimeStatus !== null && !runtimeStatus.available;
    const previewCoverageTone = preview && preview.rowCoveragePercent < 80
        ? "danger"
        : preview && preview.rowCoveragePercent < 95
            ? "warning"
            : "positive";

    const runCoverageRows = useMemo(() => {
        if (!runResult?.sourceRowsByTable) return [];

        const source = runResult.sourceRowsByTable;
        const imported = runResult.importedRowsByTable ?? {};
        const coverage = runResult.coverageByTable ?? {};
        return Object.keys(source)
            .sort((a, b) => a.localeCompare(b, "sr-Latn"))
            .map((key) => {
                const metric = coverage[key];
                const sourceRows = Number(source[key] ?? 0);
                const acceptedRows = Number(metric?.acceptedRows ?? imported[key] ?? 0);
                const skippedRows = Number(metric?.skippedRows ?? Math.max(0, sourceRows - acceptedRows));
                const targetWrites = Number(metric?.targetWrites ?? imported[key] ?? 0);
                const coveragePercent = Number(metric?.coveragePercent ?? (sourceRows <= 0 ? 100 : (acceptedRows / sourceRows) * 100));
                const transformation = metric ? describeTransformation(metric) : "1:1";
                return {
                    key,
                    sourceRows,
                    acceptedRows,
                    skippedRows,
                    targetWrites,
                    transformation,
                    coverage: Number.isFinite(coveragePercent) ? coveragePercent : 0,
                };
            });
    }, [runResult]);

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
                    {
                        label: "Coverage",
                        value: preview ? `${preview.rowCoveragePercent.toFixed(1)}%` : "-",
                        tone: preview ? previewCoverageTone : "neutral",
                    },
                ]}
            />

            <div className="accimport-page">
            <h1 className="accimport-title">Access Import (TRENDPLUS.accdb/.mdb)</h1>
            <p className="accimport-subtitle">
                ETL pipeline: Access -&gt; Trendplus DB + Analytics DB. Podrska za upsert, analizu seme, batch istoriju i kaskadno brisanje.
            </p>

            {/* ---- Tabs ---- */}
            <div className="accimport-tabs">
                {([
                    ["source", "1) Izvor"],
                    ["preview", "2) Analiza & Import"],
                    ["lastImport", "3) Rezultat"],
                    ["batches", "4) Istorija"],
                    ["cleanup", "5) Cleanup"],
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

            <form
                className="accimport-filterbar"
                aria-label="Admin key"
                onSubmit={(event) => {
                    event.preventDefault();
                    submitAdminKey();
                }}
            >
                <label className="accimport-field">
                    <span className="accimport-label">Admin key</span>
                    <input
                        ref={adminKeyInputRef}
                        className="accimport-input"
                        type="password"
                        autoComplete="off"
                        value={adminKeyInput}
                        onChange={(event) => setAdminKeyInput(event.target.value)}
                        placeholder={adminKeySaved ? "Ključ je sačuvan za ovu sesiju" : "Unesite admin key"}
                    />
                </label>
                <button type="submit" className="accimport-btn accimport-btn-secondary">Sačuvaj za ovu sesiju</button>
                {adminKeySaved && (
                    <button type="button" className="accimport-btn accimport-btn-secondary" onClick={clearAdminKey}>
                        Obriši ključ
                    </button>
                )}
            </form>

            {/* ---- Global error ---- */}
            {error && <div className="accimport-error">{error}</div>}
            {batchHistoryWarning && <div className="accimport-warning" role="status">{batchHistoryWarning}</div>}

            {activeRunCheck === "needsKey" && !importBusy && (
                <div className="accimport-error" role="status">
                    Nije provereno da li je import već pokrenut u drugoj sesiji (potreban je admin key).{" "}
                    <button
                        type="button"
                        className="accimport-btn accimport-btn-secondary"
                        onClick={() => {
                            const key = promptAdminKey("Provera aktivnog importa");
                            if (!key) return;
                            void refreshBatches("active-run-check", key);
                        }}
                    >
                        Proveri status importa
                    </button>
                </div>
            )}

            {runtimeBlocksImport && (
                <div className="accimport-error">
                    {runtimeStatus?.detail ??
                        "Access preview/import je privremeno nedostupan jer nedostaju ODBC zavisnosti na serveru."}
                </div>
            )}

            {/* ---- Progress bar (visible on any tab while importing) ---- */}
            {importBusy && (
                <div className="accimport-progress">
                    <div className="accimport-progress-title">Import u toku...</div>
                    <div className="accimport-progress-bar-track">
                        <div className="accimport-progress-bar-fill" style={{ width: "100%", animation: "pulse 1.5s ease-in-out infinite" }} />
                    </div>
                    <div className="accimport-progress-meta">
                        <span>Status: {trackedBatch?.status ?? runResult?.status ?? "running"}</span>
                        {runningBatchId !== null && <span>Batch #{runningBatchId}</span>}
                        {trackedBatch?.currentStep && <span>Korak: {trackedBatch.currentStep}</span>}
                        {trackedBatch?.currentTable && <span>Tabela: {trackedBatch.currentTable}</span>}
                        {typeof trackedBatch?.progressPercent === "number" && <span>Napredak: {trackedBatch.progressPercent.toLocaleString("sr-RS", { maximumFractionDigits: 0 })}%</span>}
                        <span>Proteklo: {importElapsed}s</span>
                        <button
                            type="button"
                            className="accimport-btn accimport-btn-danger"
                            onClick={() => void handleCancelImport()}
                            disabled={runningBatchId === null || cancellingImport}
                        >
                            {cancellingImport ? "Šaljem otkazivanje..." : "Otkaži import"}
                        </button>
                    </div>
                    {trackedBatchStale && (
                        <div className="accimport-error" role="status">
                            Import duže od 15 minuta ne javlja napredak. Moguće je da je server restartovan; server će ovakav batch označiti kao prekinut pri sledećem oporavku.
                        </div>
                    )}
                </div>
            )}

            {/* ============ TAB: Source ============ */}
            {activeTab === "source" && (
                <div className="accimport-filterbar">
                    {/* File source */}
                    <div className="accimport-field" style={{ gridColumn: "1 / -1" }}>
                        <span className="accimport-label">Izvor Access fajla (.accdb/.mdb)</span>
                        <label className="accimport-checkbox-row">
                            <input type="checkbox" checked={useRootFile} onChange={(e) => setUseRootFile(e.target.checked)} />
                            Koristi TRENDPLUS.accdb/.mdb automatski (root ili parent folder)
                        </label>
                    </div>

                    <div className={`accimport-field ${useRootFile ? "is-disabled" : ""}`}>
                        <span className="accimport-label">Ručni izbor fajla</span>
                        <span className="accimport-hint">Maksimalna veličina fajla: 100 MiB</span>
                        <input
                            ref={fileInputRef}
                            type="file"
                            accept=".accdb,.mdb"
                            style={{ display: "none" }}
                            onChange={(e) => {
                                const selected = e.target.files?.[0] ?? null;
                                if (selected && selected.size > ACCESS_IMPORT_MAX_UPLOAD_BYTES) {
                                    setError(accessImportFileTooLargeMessage(selected.size));
                                    setFile(null);
                                    e.target.value = "";
                                    return;
                                }
                                if (selected) setUseRootFile(false);
                                setFile(selected);
                            }}
                        />
                        <div className="accimport-file-row">
                            <button
                                type="button"
                                className="accimport-btn accimport-btn-secondary"
                                onClick={() => fileInputRef.current?.click()}
                                disabled={useRootFile}
                                title={useRootFile ? "Isključi automatski fajl da bi izabrao ručno." : "Izaberi .accdb ili .mdb fajl"}
                            >
                                Browse .accdb/.mdb
                            </button>
                            {file && (
                                <button type="button" className="accimport-btn accimport-btn-clear" onClick={() => { setFile(null); if (fileInputRef.current) fileInputRef.current.value = ""; }}>
                                    Obriši
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
                        <label className="accimport-label" htmlFor="access-source-namespace">Registrovani Access namespace</label>
                        <input
                            id="access-source-namespace"
                            className="accimport-input"
                            value={sourceNamespace}
                            onChange={(e) => setSourceNamespace(e.target.value)}
                            placeholder="npr. prodavnica-001 (mora biti prethodno registrovan)"
                            autoComplete="off"
                        />
                        <label className="accimport-checkbox-row">
                            <input type="checkbox" checked={includeAnalytics} onChange={(e) => setIncludeAnalytics(e.target.checked)} />
                            Ukljuci upis u Analytics
                        </label>
                        <div className="accimport-hint">Rutinski import je incremental i nikada ne briše istorijsku prodaju.</div>
                    </div>

                    {/* Actions */}
                    <div className="accimport-field">
                        <span className="accimport-label">Akcije</span>
                        <div className="accimport-actions">
                            <button className="accimport-btn accimport-btn-primary" onClick={() => void handlePreview()} disabled={busy || !sourceSelected || runtimeBlocksImport}>
                                {loadingPreview ? "Analiziram..." : "Analiza šeme"}
                            </button>
                            <button className="accimport-btn accimport-btn-success" onClick={() => void handleImport()} disabled={busy || !sourceSelected || !sourceNamespace.trim() || previewBlocksImport || runtimeBlocksImport}>
                                {importBusy ? "Import u toku..." : "Pokreni import"}
                            </button>
                        </div>
                        {runtimeBlocksImport && (
                            <div className="accimport-hint">
                                Import je blokiran dok server ne dobije ODBC runtime (`unixODBC` + `MDBTools`).
                            </div>
                        )}
                        {previewBlocksImport && (
                            <div className="accimport-hint">
                                Import je blokiran: analiza seme je vratila da fajl nije spreman za import.
                            </div>
                        )}
                        {!sourceSelected && (
                            <div className="accimport-hint">
                                Izaberi fajl ili uključi automatski TRENDPLUS.accdb/.mdb.
                            </div>
                        )}
                        {!sourceNamespace.trim() && (
                            <div className="accimport-hint">Unesi prethodno registrovani, nepromenljivi namespace logičke Access baze.</div>
                        )}
                    </div>

                    {preview && (
                        <div className="accimport-field" style={{ gridColumn: "1 / -1" }}>
                            <div className={preview.canImport ? "accimport-success" : "accimport-warning"} style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                                <span>
                                    Analiza šeme: <strong>{preview.canImport ? "OK" : "BLOKIRANO"}</strong> - Tabele:{" "}
                                    <strong>{preview.mappedAccessTables}/{preview.totalAccessTables}</strong> - Redovi coverage:{" "}
                                    <strong>{preview.rowCoveragePercent.toFixed(1)}%</strong> - Nemapirano tabela sa podacima:{" "}
                                    <strong>{preview.unmappedAccessTablesWithRows.length}</strong>
                                </span>
                                <button
                                    type="button"
                                    className="accimport-btn accimport-btn-secondary"
                                    onClick={() => setActiveTab("preview")}
                                    disabled={busy}
                                >
                                    Otvori detalje
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            )}

            {/* ============ TAB: Preview (Schema Analysis) ============ */}
            {activeTab === "preview" && (
                <>
                    <div className="accimport-filterbar accimport-filterbar-compact">
                        <div className="accimport-field" style={{ gridColumn: "1 / -1" }}>
                            <span className="accimport-label">Trenutni izvor</span>
                            <div className="accimport-source-row">
                                <span className={`accimport-file-name ${sourceSelected ? "has-file" : ""}`}>
                                    {useRootFile ? "TRENDPLUS.accdb/.mdb (automatski)" : file ? file.name : "Nije izabran fajl"}
                                </span>
                                <button className="accimport-btn accimport-btn-secondary" type="button" onClick={() => setActiveTab("source")} disabled={busy}>
                                    Promeni izvor
                                </button>
                            </div>
                        </div>
                        <div className="accimport-field">
                            <span className="accimport-label">Opcije importa</span>
                            <div className="accimport-hint">Namespace: {sourceNamespace.trim() || "nije unet"}</div>
                            <label className="accimport-checkbox-row">
                                <input type="checkbox" checked={includeAnalytics} onChange={(e) => setIncludeAnalytics(e.target.checked)} />
                                Ukljuci upis u Analytics
                            </label>
                        </div>
                        <div className="accimport-field">
                            <span className="accimport-label">Akcije</span>
                            <div className="accimport-actions">
                                <button className="accimport-btn accimport-btn-secondary" type="button" onClick={() => void handlePreview()} disabled={busy || !sourceSelected || runtimeBlocksImport}>
                                    {loadingPreview ? "Analiziram..." : "Ponovi analizu"}
                                </button>
                                <button className="accimport-btn accimport-btn-success" type="button" onClick={() => void handleImport()} disabled={busy || !sourceSelected || !sourceNamespace.trim() || previewBlocksImport || runtimeBlocksImport}>
                                    {importBusy ? "Import u toku..." : "Pokreni import"}
                                </button>
                            </div>
                            {runtimeBlocksImport && (
                                <div className="accimport-hint">
                                    Import je blokiran dok server ne dobije ODBC runtime (`unixODBC` + `MDBTools`).
                                </div>
                            )}
                            {previewBlocksImport && (
                                <div className="accimport-hint">
                                    Import je blokiran: analiza seme je vratila da fajl nije spreman za import.
                                </div>
                            )}
                            {!sourceSelected && (
                                <div className="accimport-hint">
                                    Izaberi fajl ili uključi automatski TRENDPLUS.accdb/.mdb.
                                </div>
                            )}
                        </div>
                    </div>

                    {!preview && !loadingPreview && (
                        <div className="accimport-empty-state">
                            Pokrenite analizu seme (ili izaberite izvor) da vidite rezultat.
                        </div>
                    )}
                    {loadingPreview && <div className="accimport-empty-state">Analiziram Access fajl (.accdb/.mdb)...</div>}
                    {preview && (
                        <div className="accimport-card">
                            <h3 className="accimport-card-title">
                                Rezultat analize - {preview.sourceFileName}
                                <span style={{ marginLeft: 10 }}>
                                    {preview.canImport
                                        ? <span className="accimport-status accimport-status-Completed">Moze se importovati</span>
                                        : <span className="accimport-status accimport-status-Failed">Nije moguce importovati</span>
                                    }
                                </span>
                            </h3>

                            {preview.warnings.length > 0 && (
                                <div className="accimport-warning">
                                    {preview.warnings.map((w, i) => <div key={i}>[!] {w}</div>)}
                                </div>
                            )}

                            {/* KPI summary */}
                            <div className="accimport-kpis">
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Pronadjene tabele</div>
                                    <div className="accimport-kpi-value">{preview.mappedAccessTables} / {preview.totalAccessTables}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Redovi u Access-u</div>
                                    <div className="accimport-kpi-value">{preview.totalAccessRows.toLocaleString("sr-RS")}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Mapirani redovi</div>
                                    <div className="accimport-kpi-value">{preview.mappedAccessRows.toLocaleString("sr-RS")}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Coverage redova</div>
                                    <div className="accimport-kpi-value">{preview.rowCoveragePercent.toFixed(1)}%</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Nemapirane tabele sa podacima</div>
                                    <div className="accimport-kpi-value">{preview.unmappedAccessTablesWithRows.length}</div>
                                </div>
                                <div className="accimport-kpi">
                                    <div className="accimport-kpi-label">Ukupno Access tabela</div>
                                    <div className="accimport-kpi-value">{preview.availableTables.length}</div>
                                </div>
                            </div>

                            {preview.unmappedAccessTablesWithRows.length > 0 && (
                                <div className="accimport-warning">
                                    <div className="accimport-label" style={{ marginBottom: 6 }}>Tabele sa podacima koje nisu mapirane</div>
                                    <div className="accimport-col-chips">
                                        {preview.unmappedAccessTablesWithRows.map((table) => (
                                            <span key={table} className="accimport-col-chip">{table}</span>
                                        ))}
                                    </div>
                                </div>
                            )}

                            {preview.unmappedAccessTablesWithRows.length === 0 &&
                                preview.tables.every((t) => !t.hasRows || t.requiredFieldsMissing.length === 0) && (
                                <div className="accimport-success">
                                    Coverage check: sve Access tabele sa podacima su mapirane i obavezna polja su prisutna.
                                </div>
                            )}

                            {/* Schema details */}
                            <div style={{ display: "grid", gap: 8 }}>
                                {preview.tables.map((t) => {
                                    const hits = matchedCount(t);
                                    const total = t.totalMappings ?? t.fieldMappings.length;
                                    const coverage = total > 0 ? (hits / total) * 100 : 100;
                                    return (
                                        <details key={t.key} className="accimport-schema-details">
                                            <summary className="accimport-schema-summary">
                                                <strong>{t.key}</strong>
                                                <span style={{ color: t.found ? "var(--text-primary)" : "var(--text-muted)" }}>{t.tableName ?? "-"}</span>
                                                <span style={{ fontSize: 12, color: "var(--text-muted)" }}>rows: {t.rowCount}</span>
                                                {t.found && (
                                                    <span style={{ fontSize: 12, color: "var(--text-muted)" }}>
                                                        match: {t.matchStrategy}
                                                    </span>
                                                )}
                                                {total > 0 && (
                                                    <span className={`accimport-schema-badge ${hits === total ? "all-matched" : "partial"}`}>
                                                        mapirano {hits}/{total} ({coverage.toFixed(0)}%)
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

                                            {t.requiredFieldsMissing.length > 0 && (
                                                <div style={{ marginTop: 8 }}>
                                                    <div className="accimport-label" style={{ marginBottom: 4 }}>Nedostaju obavezna polja</div>
                                                    <div className="accimport-col-chips">
                                                        {t.requiredFieldsMissing.map((field) => (
                                                            <span key={`${t.key}-missing-${field}`} className="accimport-col-chip" style={{ borderColor: "var(--error)", color: "var(--error)" }}>
                                                                {field}
                                                            </span>
                                                        ))}
                                                    </div>
                                                </div>
                                            )}

                                            {t.unmappedAccessColumns.length > 0 && (
                                                <div style={{ marginTop: 8 }}>
                                                    <div className="accimport-label" style={{ marginBottom: 4 }}>
                                                        Access kolone bez mapiranja ({t.unmappedAccessColumns.length})
                                                    </div>
                                                    <div className="accimport-col-chips">
                                                        {t.unmappedAccessColumns.map((column) => (
                                                            <span key={`${t.key}-unmapped-${column}`} className="accimport-col-chip">
                                                                {column}
                                                            </span>
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
                                                                    <td style={{ color: m.sourceColumn ? "var(--text-primary)" : "var(--text-muted)" }}>{m.sourceColumn ?? "-"}</td>
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
                    {!runResult && !importBusy && (
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
                                <h3 className="accimport-card-title">Rezultati importa - Trendplus entiteti</h3>
                                <div className="accimport-result-grid">
                                    <ResultLine entity="Artikli" inserted={runResult.artikliInserted} updated={runResult.artikliUpdated} />
                                    <ResultLine entity="Dobavljači" inserted={runResult.dobavljaciInserted} updated={runResult.dobavljaciUpdated} />
                                    <ResultLine entity="Sezone" inserted={runResult.sezoneInserted} updated={runResult.sezoneUpdated} />
                                    <ResultLine entity="Tipovi obuce" inserted={runResult.tipoviInserted} updated={runResult.tipoviUpdated} />
                                    <ResultLine entity="Prodaje" inserted={runResult.prodajaInserted} updated={runResult.prodajaUpdated} />
                                    <ResultLine entity="Stavke prodaje" inserted={runResult.prodajaStavkeInserted} updated={runResult.prodajaStavkeUpdated} />
                                    <ResultLine entity="Dnevnik promena" inserted={runResult.dnevnikInserted} updated={runResult.dnevnikUpdated} />
                                    <ResultLine entity="Povraćaji" inserted={runResult.povracajInserted} updated={runResult.povracajUpdated} />
                                    <ResultLine entity="Stavke povracaja" inserted={runResult.povracajStavkeInserted} updated={runResult.povracajStavkeUpdated} />
                                </div>
                            </div>

                            {runCoverageRows.length > 0 && (
                                <div className="accimport-card">
                                    <h3 className="accimport-card-title">Kontrola pokrivenosti (Access vs import)</h3>
                                    <div className="accimport-scroll" style={{ maxHeight: 320 }}>
                                        <table className="accimport-table">
                                            <thead>
                                                <tr>
                                                    <th>Tabela kljuc</th>
                                                    <th className="align-right">Source rows</th>
                                                    <th className="align-right">Accepted</th>
                                                    <th className="align-right">Skipped</th>
                                                    <th className="align-right">Target writes</th>
                                                    <th>Transformacija</th>
                                                    <th className="align-right">Coverage</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {runCoverageRows.map((row) => {
                                                    const tone = row.coverage >= 95 ? "var(--success)" : row.coverage >= 70 ? "var(--warning)" : "var(--error)";
                                                    return (
                                                        <tr key={row.key}>
                                                            <td>{row.key}</td>
                                                            <td className="align-right">{row.sourceRows.toLocaleString("sr-RS")}</td>
                                                            <td className="align-right">{row.acceptedRows.toLocaleString("sr-RS")}</td>
                                                            <td className="align-right">{row.skippedRows.toLocaleString("sr-RS")}</td>
                                                            <td className="align-right">{row.targetWrites.toLocaleString("sr-RS")}</td>
                                                            <td>{row.transformation}</td>
                                                            <td className="align-right" style={{ color: tone, fontWeight: 700 }}>
                                                                {row.coverage.toFixed(1)}%
                                                            </td>
                                                        </tr>
                                                    );
                                                })}
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                            )}

                            {runResult.includeAnalytics && (
                                <div className="accimport-card">
                                    <h3 className="accimport-card-title">Rezultati importa - Analytics dimenzije</h3>
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
                                    {runResult.warnings.map((w, i) => <div key={i}>[!] {w}</div>)}
                                </div>
                            )}
                        </>
                    )}
                </>
            )}

            {/* ============ TAB: Cleanup (delete non-Access rows) ============ */}
            {activeTab === "cleanup" && (
                <>
                    <div className="accimport-filterbar">
                        <div className="accimport-field" style={{ gridColumn: "1 / -1" }}>
                            <span className="accimport-label">Pregled zapisa koji NISU importovani iz Access-a</span>
                            <div className="accimport-actions">
                                <button className="accimport-btn accimport-btn-secondary" onClick={() => void handleCleanupPreview()} disabled={cleanupLoadingPreview || cleanupExecuting}>
                                    {cleanupLoadingPreview ? "Pregledavam..." : "Preview za brisanje"}
                                </button>
                                <button className="accimport-btn accimport-btn-danger" onClick={() => void handleCleanupExecute()} disabled={cleanupExecuting || cleanupLoadingPreview}>
                                    {cleanupExecuting ? "Brišem..." : "Obriši ne-Access zapise"}
                                </button>
                            </div>
                        </div>
                    </div>

                    {cleanupPreview === null && !cleanupLoadingPreview && (
                        <div className="accimport-empty-state">Pokreni preview da vidiš koliko zapisa bi bilo obrisano.</div>
                    )}

                    {cleanupLoadingPreview && <div className="accimport-empty-state">Prikupljam statistiku...</div>}

                    {cleanupPreview && (
                        <div className="accimport-card">
                            <h3 className="accimport-card-title">Preview brisanja</h3>
                            <div className="accimport-result-grid">
                                {Object.keys(cleanupPreview).map((k) => (
                                    <div key={k} className="accimport-kpi" style={{ minWidth: 140 }}>
                                        <div className="accimport-kpi-label">{k}</div>
                                        <div className="accimport-kpi-value">{cleanupPreview[k].toLocaleString("sr-RS")}</div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}

                    {cleanupResult && (
                        <div className="accimport-delete-banner">
                            <strong>Cleanup izvršen:</strong>
                            {Object.keys(cleanupResult.deleted).map((k) => (
                                <span key={k}> {k}: {cleanupResult.deleted[k]},</span>
                            ))}
                            <button className="accimport-delete-banner-close" onClick={() => setCleanupResult(null)}>&times;</button>
                        </div>
                    )}

                    <div className="accimport-card" style={{ marginTop: 12 }}>
                        <h3 className="accimport-card-title">Restore iz arhive</h3>
                        <div style={{ display: "grid", gap: 8 }}>
                            <div>
                                <div className="accimport-label">Archive IDs (comma-separated)</div>
                                <input className="accimport-input" value={archiveIdsInput} onChange={(e) => setArchiveIdsInput(e.target.value)} placeholder="npr. 12,13,14" />
                            </div>
                            <div style={{ display: "flex", gap: 8 }}>
                                <button className="accimport-btn accimport-btn-secondary" onClick={async () => {
                                    setRestoreScript(null);
                                    const raw = archiveIdsInput.split(',').map(s => s.trim()).filter(Boolean).map(s => Number(s)).filter(n => !Number.isNaN(n));
                                    if (raw.length === 0) { setError('Unesi bar jedan validan archive id.'); return; }
                                    const adminKey = adminKeyRef.current ?? promptAdminKey("Generisanje restore skripte");
                                    if (!adminKey) return;
                                    setError(null);
                                    setGeneratingRestoreScript(true);
                                    try {
                                        const script = await getRestoreScript(raw, adminKey);
                                        setRestoreScript(script);
                                    } catch (e: unknown) {
                                        if (isUnauthorizedError(e)) {
                                            clearAdminKey();
                                            setError("Generisanje restore skripte zahteva važeći admin key.");
                                            return;
                                        }
                                        setError(e instanceof Error ? e.message : 'Greška pri generisanju restore skripte.');
                                    } finally {
                                        setGeneratingRestoreScript(false);
                                    }
                                }} disabled={generatingRestoreScript}>{generatingRestoreScript ? 'Generišem...' : 'Generiši restore skriptu'}</button>
                                {restoreScript && (
                                    <button className="accimport-btn" onClick={() => {
                                        const blob = new Blob([restoreScript], { type: 'text/sql' });
                                        const url = URL.createObjectURL(blob);
                                        const a = document.createElement('a');
                                        a.href = url;
                                        a.download = 'restore_from_archive.sql';
                                        document.body.appendChild(a);
                                        a.click();
                                        a.remove();
                                        URL.revokeObjectURL(url);
                                    }}>Preuzmi skriptu</button>
                                )}
                            </div>

                            {restoreScript && (
                                <div>
                                    <div className="accimport-label">Generisana skripta</div>
                                    <textarea className="accimport-input" style={{ minHeight: 220, fontFamily: 'monospace', whiteSpace: 'pre' }} value={restoreScript} readOnly />
                                </div>
                            )}
                        </div>
                    </div>
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
                                <option value="completed">Completed</option>
                                <option value="running">Running</option>
                                <option value="pending">Pending</option>
                                <option value="failed">Failed</option>
                                <option value="cancelled">Cancelled</option>
                                <option value="interrupted">Interrupted</option>
                            </select>
                        </div>
                        <div className="accimport-field">
                            <span className="accimport-label">Opcije brisanja</span>
                            <label className="accimport-checkbox-row">
                                <input type="checkbox" checked={deleteIncludeAnalytics} onChange={(e) => setDeleteIncludeAnalytics(e.target.checked)} />
                                Obriši i Analytics podatke (DataOrigin=access)
                            </label>
                        </div>
                        <div className="accimport-field" style={{ alignItems: "flex-end" }}>
                            <button
                                className="accimport-btn accimport-btn-secondary"
                                onClick={() => {
                                    const key = adminKeyRef.current ?? promptAdminKey("Pregled import istorije");
                                    if (!key) return;
                                    void refreshBatches("manual-refresh", key);
                                }}
                            >
                                Osveži
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
                            Dobavljači: {deleteResult.dobavljaciDeleted},
                            Tipovi: {deleteResult.tipoviDeleted},
                            Dnevnik: {deleteResult.dnevnikDeleted},
                            Povraćaji: {deleteResult.povracajDeleted} ({deleteResult.povracajStavkeDeleted} stavki),
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
                                                    {deletingBatchId === b.id ? "Brišem..." : "Obriši"}
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
