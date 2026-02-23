export type DataScope = "all" | "existing" | "imported";

const STORAGE_KEY = "trendplus:dataScope";

export function normalizeDataScope(raw: string | null | undefined): DataScope {
    if (raw === "existing" || raw === "imported" || raw === "all") return raw;
    return "all";
}

export function getDataScope(): DataScope {
    return normalizeDataScope(localStorage.getItem(STORAGE_KEY));
}

export function setDataScope(scope: DataScope): void {
    localStorage.setItem(STORAGE_KEY, scope);
}

export function appendDataScopeToParams(params: URLSearchParams): void {
    params.set("dataScope", getDataScope());
}

export function getDataScopeStorageKey(): string {
    return STORAGE_KEY;
}
