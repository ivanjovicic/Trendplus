import { getActiveApiBaseUrl, getConfiguredPrimaryApiBaseUrl } from "./apiFailover";

export function apiUrl(path: string): string {
  const normalized = path.startsWith("/") ? path : `/${path}`;

  if (import.meta.env.DEV) {
    return normalized;
  }

  const activeBase = getActiveApiBaseUrl();
  const primaryBase = getConfiguredPrimaryApiBaseUrl();
  const base = activeBase || primaryBase;

  return base ? `${base}${normalized}` : normalized;
}
