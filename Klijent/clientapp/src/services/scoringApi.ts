const PYTHON_BASE = (import.meta.env.VITE_PYTHON_API_URL ?? "").replace(/\/+$/, "");

export async function fetchGlobalTop10(
    items: any[],
    shoeType?: string | null,
    topN = 10,
): Promise<any[]> {
    const url = PYTHON_BASE ? `${PYTHON_BASE}/api/global-top10` : "/api/global-top10";
    const resp = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ items, shoeType: shoeType ?? null, topN }),
    });
    if (!resp.ok) throw new Error(`Scorer ${resp.status}`);
    const data = await resp.json();
    return data.top10 ?? [];
}
