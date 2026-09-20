export type AnalyticsTableSortDir = "asc" | "desc";

export function readAnalyticsTableSort<T extends string>(
  searchParams: URLSearchParams,
  fields: readonly T[],
  defaultField: T,
  defaultDir: AnalyticsTableSortDir,
): { field: T; dir: AnalyticsTableSortDir } {
  const candidateField = searchParams.get("sort");
  const field = candidateField && fields.includes(candidateField as T)
    ? candidateField as T
    : defaultField;
  const dir = searchParams.get("dir") === "asc" || searchParams.get("dir") === "desc"
    ? searchParams.get("dir") as AnalyticsTableSortDir
    : defaultDir;
  return { field, dir };
}

export function writeAnalyticsTableSort(
  current: URLSearchParams,
  field: string,
  dir: AnalyticsTableSortDir,
): URLSearchParams {
  const next = new URLSearchParams(current);
  next.set("sort", field);
  next.set("dir", dir);
  return next;
}
