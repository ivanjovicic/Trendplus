import type {
  KreirajPovracajRequest,
  KreirajPovracajResponse,
  PovracajListResponse,
  PovracajDetaljno
} from "../types/povracaj";
import { apiUrl } from "../utils/apiUrl";

export async function kreirajPovracaj(request: KreirajPovracajRequest): Promise<KreirajPovracajResponse> {
  const res = await fetch(apiUrl("/api/povracaj"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });

  if (!res.ok) {
    const error = await res.text();
    throw new Error(error || "Greška pri kreiranju povraćaja");
  }

  return res.json();
}

export async function getPovracaji(
  pageNumber: number = 1,
  pageSize: number = 50,
  filters?: Record<string, string | number>
): Promise<PovracajListResponse> {
  const params = new URLSearchParams({
    pageNumber: String(pageNumber),
    pageSize: String(pageSize),
    ...Object.fromEntries(
      Object.entries(filters ?? {}).map(([k, v]) => [k, String(v)])
    )
  });

  const res = await fetch(apiUrl(`/api/povracaj?${params}`));

  if (!res.ok) {
    throw new Error("Greška pri učitavanju povraćaja");
  }

  return res.json();
}

export async function getPovracajDetalji(id: number): Promise<PovracajDetaljno> {
  const res = await fetch(apiUrl(`/api/povracaj/${id}`));

  if (!res.ok) {
    throw new Error("Greška pri učitavanju detalja povraćaja");
  }

  return res.json();
}
