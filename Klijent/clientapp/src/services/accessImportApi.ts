const API = import.meta.env.VITE_API_BASE_URL ?? "";

export interface AccessImportTablePreview {
    key: string;
    tableName: string | null;
    rowCount: number;
    matchStrategy: string;
    accessColumns: string[];
    fieldMappings: AccessImportFieldMappingPreview[];
    matchedMappings: number;
    totalMappings: number;
    mappingCoveragePercent: number;
    requiredFieldsMissing: string[];
    unmappedAccessColumns: string[];
    found: boolean;
    hasRows: boolean;
}

export interface AccessImportFieldMappingPreview {
    targetField: string;
    sourceColumn: string | null;
    status: string;
}

export interface AccessImportPreviewResponse {
    canImport: boolean;
    sourceFileName: string;
    tables: AccessImportTablePreview[];
    availableTables: string[];
    totalAccessTables: number;
    accessTablesWithRows: number;
    mappedAccessTables: number;
    mappedAccessTablesWithRows: number;
    totalAccessRows: number;
    mappedAccessRows: number;
    rowCoveragePercent: number;
    unmappedAccessTablesWithRows: string[];
    warnings: string[];
}

export interface AccessImportRunResponse {
    batchId: number;
    status: string;
    sourceFileName: string;
    includeAnalytics: boolean;
    startedAtUtc: string;
    completedAtUtc: string;
    tipoviInserted: number;
    tipoviUpdated: number;
    dobavljaciInserted: number;
    dobavljaciUpdated: number;
    sezoneInserted: number;
    sezoneUpdated: number;
    artikliInserted: number;
    artikliUpdated: number;
    prodajaInserted: number;
    prodajaUpdated: number;
    prodajaStavkeInserted: number;
    prodajaStavkeUpdated: number;
    dnevnikInserted: number;
    dnevnikUpdated: number;
    povracajInserted: number;
    povracajUpdated: number;
    povracajStavkeInserted: number;
    povracajStavkeUpdated: number;
    productsDimInserted: number;
    productsDimUpdated: number;
    salesFactsInserted: number;
    salesFactsUpdated: number;
    salesLineFactsInserted: number;
    storesInserted: number;
    storesUpdated: number;
    sourceRowsByTable: Record<string, number>;
    importedRowsByTable: Record<string, number>;
    coverageByTable?: Record<string, AccessImportCoverageMetric>;
    warnings: string[];
}

export interface AccessImportCoverageMetric {
    sourceRows: number;
    acceptedRows: number;
    skippedRows: number;
    targetWrites: number;
    mergedRows: number;
    expandedTargetRows: number;
    coveragePercent: number;
    transformationType: string;
}

export interface AccessImportBatchDto {
    id: number;
    sourceSystem: string;
    sourceFileName: string;
    startedAtUtc: string;
    completedAtUtc: string | null;
    status: string;
    summaryJson: string | null;
    errorMessage: string | null;
    // Enhanced (migration 015)
    durationSeconds: number | null;
    totalImported: number;
    totalUpdated: number;
    totalErrors: number;
    dataOrigin: string;
}

export interface AccessImportLogDto {
    id: number;
    batchId: number;
    tableName: string;
    rowIndex: number;
    severity: string;
    message: string;
    sourceRowJson: string | null;
    createdAtUtc: string;
}

export interface BatchDetailDto {
    batch: AccessImportBatchDto;
    logs: AccessImportLogDto[];
    logCountBySeverity: Record<string, number>;
    logCountByTable: Record<string, number>;
}

async function parseError(res: Response): Promise<string> {
    try {
        const body = await res.json();
        return body?.error ?? body?.detail ?? body?.title ?? `HTTP ${res.status}`;
    } catch {
        return `HTTP ${res.status}`;
    }
}

function buildFormData(file: File | null, options?: { useRootFile?: boolean; includeAnalytics?: boolean; overwriteExisting?: boolean }): FormData {
    const fd = new FormData();
    if (file) fd.append("file", file);
    if (options?.useRootFile !== undefined) fd.append("useRootFile", String(options.useRootFile));
    if (options?.includeAnalytics !== undefined) fd.append("includeAnalytics", String(options.includeAnalytics));
    if (options?.overwriteExisting !== undefined) fd.append("overwriteExisting", String(options.overwriteExisting));
    return fd;
}

export async function previewAccessImport(file: File | null, useRootFile = false): Promise<AccessImportPreviewResponse> {
    const res = await fetch(`${API}/api/access-import/preview`, {
        method: "POST",
        body: buildFormData(file, { useRootFile }),
    });
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}

export async function runAccessImport(
    file: File | null,
    options?: { useRootFile?: boolean; includeAnalytics?: boolean; overwriteExisting?: boolean }
): Promise<AccessImportRunResponse> {
    const res = await fetch(`${API}/api/access-import/run`, {
        method: "POST",
        body: buildFormData(file, options),
    });
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}

export async function getAccessImportBatches(take = 20): Promise<AccessImportBatchDto[]> {
    const res = await fetch(`${API}/api/access-import/batches?take=${take}`);
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}

export async function getAccessImportBatchDetail(batchId: number, logTake = 200, severity?: string): Promise<BatchDetailDto> {
    const params = new URLSearchParams({ logTake: String(logTake) });
    if (severity) params.set("severity", severity);
    const res = await fetch(`${API}/api/access-import/batches/${batchId}?${params}`);
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}

export async function getAccessImportBatchLogs(
    batchId: number,
    opts?: { severity?: string; tableName?: string; skip?: number; take?: number }
): Promise<AccessImportLogDto[]> {
    const params = new URLSearchParams();
    if (opts?.severity) params.set("severity", opts.severity);
    if (opts?.tableName) params.set("tableName", opts.tableName);
    if (opts?.skip !== undefined) params.set("skip", String(opts.skip));
    if (opts?.take !== undefined) params.set("take", String(opts.take));
    const res = await fetch(`${API}/api/access-import/batches/${batchId}/logs?${params}`);
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}

export interface DeleteBatchResult {
    found: boolean;
    batchId: number;
    includeAnalytics: boolean;
    artikliDeleted: number;
    sezoneDeleted: number;
    tipoviDeleted: number;
    dobavljaciDeleted: number;
    prodajaDeleted: number;
    stavkeDeleted: number;
    productsDimDeleted: number;
    salesFactsDeleted: number;
    salesLineFactsDeleted: number;
    inventoryMovementsDeleted: number;
    suppliersDimDeleted: number;
    seasonsDimDeleted: number;
    footwearTypesDimDeleted: number;
    storesDimDeleted: number;
    cacheInvalidated: boolean;
    dnevnikDeleted: number;
    povracajDeleted: number;
    povracajStavkeDeleted: number;
}

export async function deleteAccessImportBatch(batchId: number, includeAnalytics = true): Promise<DeleteBatchResult> {
    const res = await fetch(`${API}/api/access-import/batches/${batchId}?includeAnalytics=${includeAnalytics}`, { method: "DELETE" });
    if (!res.ok) throw new Error(await parseError(res));
    return res.json();
}
