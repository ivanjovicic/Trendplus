import { apiUrl } from "../utils/apiUrl";

async function parseError(res: Response): Promise<string> {
  try {
    const body = await res.json();
    return body?.error ?? body?.detail ?? body?.title ?? `HTTP ${res.status}`;
  } catch {
    return `HTTP ${res.status}`;
  }
}

export async function getRestoreScript(ids: number[]): Promise<string> {
  const res = await fetch(apiUrl("/api/access-import/cleanup/archive/restore-script"), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ids }),
  });

  if (!res.ok) throw new Error(await parseError(res));

  const data = await res.json();
  return data?.script ?? '';
}
