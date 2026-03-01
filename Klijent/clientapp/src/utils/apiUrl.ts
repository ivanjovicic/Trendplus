const API = import.meta.env.VITE_API_BASE_URL || "";

export function apiUrl(path: string): string {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return import.meta.env.DEV ? normalized : `${API}${normalized}`;
}
