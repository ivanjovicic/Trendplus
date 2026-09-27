export type AnalyticsTableSortDir = "asc" | "desc";

type DefaultSortDir<T extends string> = AnalyticsTableSortDir | ((field: T) => AnalyticsTableSortDir);

function resolveDefaultDir<T extends string>(defaultDir: DefaultSortDir<T>, field: T): AnalyticsTableSortDir {
  return typeof defaultDir === "function" ? defaultDir(field) : defaultDir;
}

export function readAnalyticsTableSort<T extends string>(
  searchParams: URLSearchParams,
  fields: readonly T[],
  defaultField: T,
  defaultDir: DefaultSortDir<T>,
): { field: T; dir: AnalyticsTableSortDir } {
  const candidateField = searchParams.get("sort");
  const field = candidateField && fields.includes(candidateField as T)
    ? candidateField as T
    : defaultField;
  const candidateDir = searchParams.get("dir");
  const dir = candidateDir === "asc" || candidateDir === "desc"
    ? candidateDir
    : resolveDefaultDir(defaultDir, field);
  return { field, dir };
}

export function writeAnalyticsTableSort<T extends string>(
  current: URLSearchParams,
  field: T,
  dir: AnalyticsTableSortDir,
  defaults?: { field: T; dir: DefaultSortDir<T> },
): URLSearchParams {
  const next = new URLSearchParams(current);
  if (!defaults) {
    next.set("sort", field);
    next.set("dir", dir);
    return next;
  }
  if (field === defaults.field) next.delete("sort");
  else next.set("sort", field);
  if (dir === resolveDefaultDir(defaults.dir, field)) next.delete("dir");
  else next.set("dir", dir);
  return next;
}
